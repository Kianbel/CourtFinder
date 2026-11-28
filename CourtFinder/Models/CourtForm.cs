using System.ComponentModel.DataAnnotations;

namespace CourtFinder.Models;

/// <summary>A point picked on the map.</summary>
public record GeoPoint(double Lat, double Lng);

public class SportEntry
{
    public short SportId { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Selected { get; set; }
    public int CourtCount { get; set; } = 1;
    public decimal? Price { get; set; }
}

public class BookingEntry
{
    public string Kind { get; set; } = "url"; // url | messenger | phone | walk_in
    public string Value { get; set; } = "";
}

/// <summary>Everything the owner edits on one page.</summary>
public class CourtForm
{
    [Required(ErrorMessage = "Enter the court's name.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Use 3 to 100 characters.")]
    public string Name { get; set; } = "";

    [StringLength(2000, ErrorMessage = "Keep the description under 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Enter the street address.")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Enter the city.")]
    public string City { get; set; } = "";

    public string? Province { get; set; }

    public bool IsIndoor { get; set; }

    [StringLength(60, ErrorMessage = "Keep it under 60 characters.")]
    public string? Surface { get; set; }

    /// <summary>"HH:mm" (the format of an HTML time input).</summary>
    public string? OpeningTime { get; set; }
    public string? ClosingTime { get; set; }

    [Required(ErrorMessage = "Pick the location on the map.")]
    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Pick the location on the map.")]
    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }

    public List<SportEntry> Sports { get; set; } = new();
    public HashSet<short> AmenityIds { get; set; } = new();
    public List<BookingEntry> Bookings { get; set; } = new();
}

/// <summary>A court loaded for editing: its form plus the photos already uploaded.</summary>
public record EditableCourt(CourtForm Form, List<CourtPhoto> Photos);