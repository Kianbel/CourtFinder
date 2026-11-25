namespace CourtFinder.Models;

/// <summary>Slim, serializable data for one map marker.</summary>
public record MapPin(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    string Emoji,
    string Summary)
{
    public static MapPin From(CourtCard c)
    {
        var rating = c.AvgRating is null ? "New" : $"★ {c.AvgRating:0.0} ({c.ReviewCount})";
        var price = c.MinPrice is null ? "" : $" · from ₱{c.MinPrice:N0}/hr";

        return new MapPin(
            c.Id,
            c.Name,
            $"{c.Address}, {c.City}",
            c.Latitude,
            c.Longitude,
            SportMeta.Emoji(c.Sports.FirstOrDefault()),
            rating + price);
    }
}