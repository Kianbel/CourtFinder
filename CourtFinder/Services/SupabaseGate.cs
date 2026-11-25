namespace CourtFinder.Services;

/// <summary>Initializes the per-circuit Supabase client exactly once.</summary>
public class SupabaseGate(Supabase.Client client)
{
    private Task? init;

    public Supabase.Client Client => client;

    public Task EnsureInitializedAsync() => init ??= client.InitializeAsync();
}