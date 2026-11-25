namespace CourtFinder.Models;

public record CourtSportInfo(Sport Sport, int CourtCount, decimal PricePerHour);

public record ReviewView(Guid Id, string AuthorName, int Rating, string? Comment, DateTime CreatedAt);

public record CourtDetails(
    Court Court,
    IReadOnlyList<CourtSportInfo> Sports,
    IReadOnlyList<Amenity> Amenities,
    IReadOnlyList<BookingLink> BookingLinks,
    IReadOnlyList<string> PhotoUrls,
    IReadOnlyList<ReviewView> Reviews)
{
    public double? AverageRating => Reviews.Count == 0 ? null : Reviews.Average(r => r.Rating);
}