using CourtFinder.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Supabase.Gotrue;

namespace CourtFinder.Services;

public record AuthResult(bool Ok, string? Error = null, bool NeedsConfirmation = false);

public class AuthService(SupabaseGate gate, ProtectedLocalStorage storage, ILogger<AuthService> logger)
{
    private const string StorageKey = "courtfinder.session";

    private record StoredSession(string AccessToken, string RefreshToken);

    private bool started;

    /// <summary>Raised whenever the signed-in state or name changes.</summary>
    public event Action? Changed;

    /// <summary>True once the saved session (if any) has been restored.</summary>
    public bool IsInitialized { get; private set; }

    public bool IsSignedIn => gate.Client.Auth.CurrentUser is not null;

    public string? DisplayName { get; private set; }

    public Guid? UserId =>
        Guid.TryParse(gate.Client.Auth.CurrentUser?.Id, out var id) ? id : null;

    /// <summary>Restores a saved session. Called once by the layout after the first render.</summary>
    public async Task InitializeAsync()
    {
        if (started) return;
        started = true;

        try
        {
            await gate.EnsureInitializedAsync();
            var auth = gate.Client.Auth;

            // Keep the saved tokens current: Supabase rotates refresh tokens
            auth.AddStateChangedListener((sender, state) =>
            {
                _ = HandleStateAsync(state);
            });

            var saved = await storage.GetAsync<StoredSession>(StorageKey);
            if (saved.Success && saved.Value is { } s)
            {
                await auth.SetSession(s.AccessToken, s.RefreshToken);
                await LoadProfileAsync();
            }
        }
        catch (Exception)
        {
            // Expired or invalid saved session: start signed out
            await ClearStoredAsync();
        }

        IsInitialized = true;
        Changed?.Invoke();
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        try
        {
            await gate.EnsureInitializedAsync();
            var session = await gate.Client.Auth.SignIn(email.Trim(), password);
            if (session?.User is null)
                return new AuthResult(false, "We couldn't sign you in. Please try again.");

            await PersistAsync();
            await LoadProfileAsync();
            Changed?.Invoke();
            return new AuthResult(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auth request failed");
            return new AuthResult(false, FriendlyError(ex));
        }
    }

    public async Task<AuthResult> SignUpAsync(string displayName, string email, string password)
    {
        try
        {
            await gate.EnsureInitializedAsync();

            var options = new SignUpOptions
            {
                // Picked up by the database trigger that creates the profile row
                Data = new Dictionary<string, object> { ["display_name"] = displayName.Trim() }
            };

            #pragma warning disable CS0618 // Obsolete SignUp ignores FlowType/Channel, which we don't use. Revisit before upgrading to Gotrue v9.
            var session = await gate.Client.Auth.SignUp(email.Trim(), password, options);
            #pragma warning restore CS0618

            // No session means the project requires email confirmation first
            if (session?.AccessToken is null)
                return new AuthResult(true, NeedsConfirmation: true);

            await PersistAsync();
            await LoadProfileAsync();
            Changed?.Invoke();
            return new AuthResult(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Auth request failed");
            return new AuthResult(false, FriendlyError(ex));
        }
    }

    public async Task SignOutAsync()
    {
        try { await gate.Client.Auth.SignOut(); }
        catch (Exception) { /* sign out locally regardless */ }

        DisplayName = null;
        await ClearStoredAsync();
        Changed?.Invoke();
    }

    private async Task LoadProfileAsync()
    {
        var user = gate.Client.Auth.CurrentUser;
        if (user?.Id is null)
        {
            DisplayName = null;
            return;
        }

        try
        {
            var profile = await gate.Client.From<Profile>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.Id)
                .Single();
            DisplayName = profile?.DisplayName;
        }
        catch (Exception)
        {
            DisplayName = null;
        }

        DisplayName ??= user.Email?.Split('@')[0];
    }

    private async Task HandleStateAsync(Supabase.Gotrue.Constants.AuthState state)
    {
        switch (state)
        {
            case Supabase.Gotrue.Constants.AuthState.SignedIn:
            case Supabase.Gotrue.Constants.AuthState.TokenRefreshed:
                await PersistAsync();
                break;
            case Supabase.Gotrue.Constants.AuthState.SignedOut:
                await ClearStoredAsync();
                break;
        }
    }

    private async Task PersistAsync()
    {
        var session = gate.Client.Auth.CurrentSession;
        if (session?.AccessToken is null || session.RefreshToken is null) return;

        try { await storage.SetAsync(StorageKey, new StoredSession(session.AccessToken, session.RefreshToken)); }
        catch (Exception) { /* browser storage unavailable: user just has to sign in again later */ }
    }

    private async Task ClearStoredAsync()
    {
        try { await storage.DeleteAsync(StorageKey); }
        catch (Exception) { }
    }

    private static string FriendlyError(Exception ex)
    {
        var m = ex.Message.ToLowerInvariant();

        if (m.Contains("invalid login credentials") || m.Contains("invalid_credentials"))
            return "Incorrect email or password.";
        if (m.Contains("email not confirmed") || m.Contains("email_not_confirmed"))
            return "Please confirm your email first. Check your inbox for the link.";
        if (m.Contains("already registered") || m.Contains("user_already_exists"))
            return "An account with this email already exists. Try signing in instead.";
        if (m.Contains("rate limit") || m.Contains("too many"))
            return "Too many attempts. Please wait a moment and try again.";
        if (m.Contains("email_provider_disabled") || m.Contains("signup_disabled") || m.Contains("signups are disabled"))
            return "Sign-ups are currently turned off. Please try again later.";
        return "Something went wrong. Please try again.";
    }
}