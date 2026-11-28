using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CourtFinder.Models;

/// <summary>
/// Insert-only shape of a review. The read model <see cref="Review"/> includes created_at,
/// and inserting it would send 0001-01-01 instead of letting the database default fill it in.
/// </summary>
[Table("reviews")]
public class ReviewWrite : BaseModel
{
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("rating")] public short Rating { get; set; }
    [Column("comment")] public string? Comment { get; set; }
}