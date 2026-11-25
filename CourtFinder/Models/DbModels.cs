using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace CourtFinder.Models;

[Table("sports")]
public class Sport : BaseModel
{
    [PrimaryKey("id", false)] public short Id { get; set; }
    [Column("slug")] public string Slug { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
}

[Table("amenities")]
public class Amenity : BaseModel
{
    [PrimaryKey("id", false)] public short Id { get; set; }
    [Column("slug")] public string Slug { get; set; } = "";
    [Column("name")] public string Name { get; set; } = "";
}

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", true)] public Guid Id { get; set; }
    [Column("display_name")] public string DisplayName { get; set; } = "";
}

[Table("courts")]
public class Court : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("owner_id")] public Guid OwnerId { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("description")] public string? Description { get; set; }
    [Column("address")] public string Address { get; set; } = "";
    [Column("city")] public string City { get; set; } = "";
    [Column("province")] public string? Province { get; set; }
    [Column("latitude")] public double Latitude { get; set; }
    [Column("longitude")] public double Longitude { get; set; }
    [Column("is_indoor")] public bool IsIndoor { get; set; }
    [Column("surface")] public string? Surface { get; set; }
    [Column("opening_time")] public TimeSpan? OpeningTime { get; set; }
    [Column("closing_time")] public TimeSpan? ClosingTime { get; set; }
}

[Table("court_sports")]
public class CourtSport : BaseModel
{
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("sport_id")] public short SportId { get; set; }
    [Column("court_count")] public short CourtCount { get; set; } = 1;
    [Column("price_per_hour")] public decimal PricePerHour { get; set; }
}

[Table("court_amenities")]
public class CourtAmenity : BaseModel
{
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("amenity_id")] public short AmenityId { get; set; }
}

[Table("booking_links")]
public class BookingLink : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("kind")] public string Kind { get; set; } = "url"; // url | messenger | phone | walk_in
    [Column("value")] public string Value { get; set; } = "";
}

[Table("court_photos")]
public class CourtPhoto : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("storage_path")] public string StoragePath { get; set; } = "";
    [Column("sort_order")] public short SortOrder { get; set; }
}

[Table("reviews")]
public class Review : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("court_id")] public Guid CourtId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("rating")] public short Rating { get; set; }
    [Column("comment")] public string? Comment { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
}

// Read-only: maps to the court_cards view (list + map)
[Table("court_cards")]
public class CourtCard : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("name")] public string Name { get; set; } = "";
    [Column("address")] public string Address { get; set; } = "";
    [Column("city")] public string City { get; set; } = "";
    [Column("province")] public string? Province { get; set; }
    [Column("latitude")] public double Latitude { get; set; }
    [Column("longitude")] public double Longitude { get; set; }
    [Column("is_indoor")] public bool IsIndoor { get; set; }
    [Column("min_price")] public decimal? MinPrice { get; set; }
    [Column("sports")] public List<string> Sports { get; set; } = new();
    [Column("avg_rating")] public decimal? AvgRating { get; set; }
    [Column("review_count")] public int ReviewCount { get; set; }
    [Column("cover_path")] public string? CoverPath { get; set; }
}