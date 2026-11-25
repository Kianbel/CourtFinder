using CourtFinder.Models;
using static Supabase.Postgrest.Constants;

namespace CourtFinder.Services;

public class CourtService(Supabase.Client client)
{
    private const string PhotoBucket = "court-photos";

    private List<Sport>? _sports;
    private List<Amenity>? _amenities;
    private bool _initialized;

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await client.InitializeAsync();
        _initialized = true;
    }

    public async Task<List<Sport>> GetSportsAsync()
    {
        await EnsureInitializedAsync();
        if (_sports is null)
        {
            var res = await client.From<Sport>().Order("name", Ordering.Ascending).Get();
            _sports = res.Models;
        }
        return _sports;
    }

    public async Task<List<Amenity>> GetAmenitiesAsync()
    {
        await EnsureInitializedAsync();
        if (_amenities is null)
        {
            var res = await client.From<Amenity>().Order("name", Ordering.Ascending).Get();
            _amenities = res.Models;
        }
        return _amenities;
    }

    /// <summary>Courts for the list and map, optionally filtered by sport slug and name search.</summary>
    public async Task<List<CourtCard>> GetCourtCardsAsync(string? sportSlug = null, string? search = null)
    {
        await EnsureInitializedAsync();

        var query = client.From<CourtCard>().Order("name", Ordering.Ascending);

        if (!string.IsNullOrWhiteSpace(sportSlug))
            query = query.Filter("sports", Operator.Contains, new List<string> { sportSlug });

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Filter("name", Operator.ILike, $"%{search.Trim()}%");

        var res = await query.Get();
        return res.Models;
    }

    public async Task<CourtDetails?> GetCourtDetailsAsync(Guid id)
    {
        await EnsureInitializedAsync();
        var idText = id.ToString();

        var court = await client.From<Court>()
            .Filter("id", Operator.Equals, idText)
            .Single();
        if (court is null) return null;

        var sports = await GetSportsAsync();
        var amenities = await GetAmenitiesAsync();

        var courtSports = (await client.From<CourtSport>()
            .Filter("court_id", Operator.Equals, idText).Get()).Models;
        var courtAmenities = (await client.From<CourtAmenity>()
            .Filter("court_id", Operator.Equals, idText).Get()).Models;
        var links = (await client.From<BookingLink>()
            .Filter("court_id", Operator.Equals, idText).Get()).Models;
        var photos = (await client.From<CourtPhoto>()
            .Filter("court_id", Operator.Equals, idText)
            .Order("sort_order", Ordering.Ascending).Get()).Models;
        var reviews = (await client.From<Review>()
            .Filter("court_id", Operator.Equals, idText)
            .Order("created_at", Ordering.Descending).Get()).Models;

        // Review authors
        var names = new Dictionary<Guid, string>();
        var userIds = reviews.Select(r => r.UserId.ToString()).Distinct().ToList();
        if (userIds.Count > 0)
        {
            var profiles = (await client.From<Profile>()
                .Filter("id", Operator.In, userIds).Get()).Models;
            names = profiles.ToDictionary(p => p.Id, p => p.DisplayName);
        }

        var sportInfos = courtSports
            .Join(sports, cs => cs.SportId, s => s.Id,
                  (cs, s) => new CourtSportInfo(s, cs.CourtCount, cs.PricePerHour))
            .OrderBy(x => x.Sport.Name)
            .ToList();

        var amenityList = courtAmenities
            .Join(amenities, ca => ca.AmenityId, a => a.Id, (_, a) => a)
            .OrderBy(a => a.Name)
            .ToList();

        var reviewViews = reviews
            .Select(r => new ReviewView(
                r.Id,
                names.GetValueOrDefault(r.UserId, "Player"),
                r.Rating,
                r.Comment,
                r.CreatedAt))
            .ToList();

        var photoUrls = photos.Select(p => GetPhotoUrl(p.StoragePath)!).ToList();

        return new CourtDetails(court, sportInfos, amenityList, links, photoUrls, reviewViews);
    }

    /// <summary>Public URL for a file in the court-photos bucket.</summary>
    public string? GetPhotoUrl(string? storagePath) =>
        string.IsNullOrEmpty(storagePath)
            ? null
            : client.Storage.From(PhotoBucket).GetPublicUrl(storagePath);
}