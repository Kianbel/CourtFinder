using CourtFinder.Models;
using static Supabase.Postgrest.Constants;

namespace CourtFinder.Services;

public class ReviewService(Supabase.Client client, SupabaseGate gate, AuthService auth)
{
    public const int MaxCommentLength = 1000;

    public async Task CreateAsync(Guid courtId, int rating, string? comment)
    {
        await gate.EnsureInitializedAsync();
        var userId = auth.UserId ?? throw new InvalidOperationException("You need to be signed in.");

        await client.From<ReviewWrite>().Insert(new ReviewWrite
        {
            CourtId = courtId,
            UserId = userId,
            Rating = (short)Math.Clamp(rating, 1, 5),
            Comment = Blank(comment)
        });
    }

    public async Task UpdateAsync(Guid reviewId, int rating, string? comment)
    {
        await gate.EnsureInitializedAsync();

        // Set(...) sends exactly these columns, including null, so the comment can be cleared
        await client.From<Review>()
            .Filter("id", Operator.Equals, reviewId.ToString())
            .Set(r => r.Rating, (short)Math.Clamp(rating, 1, 5))
            .Set(r => r.Comment!, Blank(comment))
            .Update();
    }

    public async Task DeleteAsync(Guid reviewId)
    {
        await gate.EnsureInitializedAsync();

        await client.From<Review>()
            .Filter("id", Operator.Equals, reviewId.ToString())
            .Delete();
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}