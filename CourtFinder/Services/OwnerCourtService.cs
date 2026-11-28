using System.Globalization;
using CourtFinder.Models;
using static Supabase.Postgrest.Constants;

namespace CourtFinder.Services;

public class OwnerCourtService(Supabase.Client client, SupabaseGate gate, AuthService auth, CourtService courtApi)
{
    private const string PhotoBucket = "court-photos";

    public const int MaxPhotos = 8;
    public const int MaxBookings = 6;
    public const long MaxPhotoBytes = 5 * 1024 * 1024;

    /// <summary>Allowed photo content types and the file extension used for each.</summary>
    public static readonly IReadOnlyDictionary<string, string> PhotoTypes = new Dictionary<string, string>
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    private Guid RequireUser() =>
        auth.UserId ?? throw new InvalidOperationException("You need to be signed in.");

    // ---------- Reading ----------

    /// <summary>A blank form with every sport listed (none selected).</summary>
    public async Task<CourtForm> NewFormAsync()
    {
        var sports = await courtApi.GetSportsAsync();
        return new CourtForm
        {
            OpeningTime = "08:00",
            ClosingTime = "22:00",
            Sports = sports
                .Select(s => new SportEntry { SportId = s.Id, Slug = s.Slug, Name = s.Name })
                .ToList()
        };
    }

    public async Task<List<CourtCard>> GetMyCourtsAsync()
    {
        await gate.EnsureInitializedAsync();
        var userId = RequireUser();

        var owned = (await client.From<Court>()
            .Filter("owner_id", Operator.Equals, userId.ToString()).Get()).Models;
        if (owned.Count == 0) return new List<CourtCard>();

        var ids = owned.Select(c => c.Id.ToString()).ToList();
        var cards = await client.From<CourtCard>()
            .Filter("id", Operator.In, ids)
            .Order("name", Ordering.Ascending)
            .Get();
        return cards.Models;
    }

    /// <summary>Loads a court for editing. Returns null if it doesn't exist or isn't yours.</summary>
    public async Task<EditableCourt?> GetForEditAsync(Guid id)
    {
        await gate.EnsureInitializedAsync();
        var userId = RequireUser();
        var idText = id.ToString();

        var court = await client.From<Court>().Filter("id", Operator.Equals, idText).Single();
        if (court is null || court.OwnerId != userId) return null;

        var allSports = await courtApi.GetSportsAsync();
        var courtSports = (await client.From<CourtSport>()
            .Filter("court_id", Operator.Equals, idText).Get()).Models;
        var amenityRows = (await client.From<CourtAmenity>()
            .Filter("court_id", Operator.Equals, idText).Get()).Models;
        var links = (await client.From<BookingLink>()
            .Filter("court_id", Operator.Equals, idText)
            .Order("created_at", Ordering.Ascending).Get()).Models;
        var photos = await GetPhotosAsync(id);

        var form = new CourtForm
        {
            Name = court.Name,
            Description = court.Description,
            Address = court.Address,
            City = court.City,
            Province = court.Province,
            IsIndoor = court.IsIndoor,
            Surface = court.Surface,
            OpeningTime = FormatTime(court.OpeningTime),
            ClosingTime = FormatTime(court.ClosingTime),
            Latitude = court.Latitude,
            Longitude = court.Longitude,
            Sports = allSports.Select(s =>
            {
                var cs = courtSports.FirstOrDefault(x => x.SportId == s.Id);
                return new SportEntry
                {
                    SportId = s.Id,
                    Slug = s.Slug,
                    Name = s.Name,
                    Selected = cs is not null,
                    CourtCount = cs?.CourtCount ?? 1,
                    Price = cs?.PricePerHour
                };
            }).ToList(),
            AmenityIds = amenityRows.Select(a => a.AmenityId).ToHashSet(),
            Bookings = links.Select(l => new BookingEntry { Kind = l.Kind, Value = l.Value }).ToList()
        };

        return new EditableCourt(form, photos);
    }

    public async Task<List<CourtPhoto>> GetPhotosAsync(Guid courtId)
    {
        await gate.EnsureInitializedAsync();
        var res = await client.From<CourtPhoto>()
            .Filter("court_id", Operator.Equals, courtId.ToString())
            .Order("sort_order", Ordering.Ascending)
            .Get();
        return res.Models;
    }

    // ---------- Create / update / delete ----------

    public async Task<Guid> CreateAsync(CourtForm form)
    {
        await gate.EnsureInitializedAsync();
        var userId = RequireUser();

        var court = new Court
        {
            OwnerId = userId,
            Name = form.Name.Trim(),
            Description = Blank(form.Description),
            Address = form.Address.Trim(),
            City = form.City.Trim(),
            Province = Blank(form.Province),
            Latitude = form.Latitude!.Value,
            Longitude = form.Longitude!.Value,
            IsIndoor = form.IsIndoor,
            Surface = Blank(form.Surface),
            OpeningTime = ParseTime(form.OpeningTime),
            ClosingTime = ParseTime(form.ClosingTime)
        };

        var res = await client.From<Court>().Insert(court);
        var id = res.Models.First().Id;

        try
        {
            await InsertChildrenAsync(id, form);
        }
        catch
        {
            // Don't leave a half-built court behind
            try { await client.From<Court>().Filter("id", Operator.Equals, id.ToString()).Delete(); }
            catch (Exception) { /* nothing more we can do */ }
            throw;
        }

        return id;
    }

    public async Task UpdateAsync(Guid id, CourtForm form)
    {
        await gate.EnsureInitializedAsync();
        RequireUser();
        var idText = id.ToString();

        // Set(...) sends exactly these columns, including nulls, so optional fields can be cleared
        await client.From<Court>()
            .Filter("id", Operator.Equals, idText)
            .Set(c => c.Name, form.Name.Trim())
            .Set(c => c.Description, Blank(form.Description))
            .Set(c => c.Address, form.Address.Trim())
            .Set(c => c.City, form.City.Trim())
            .Set(c => c.Province, Blank(form.Province))
            .Set(c => c.Latitude, form.Latitude!.Value)
            .Set(c => c.Longitude, form.Longitude!.Value)
            .Set(c => c.IsIndoor, form.IsIndoor)
            .Set(c => c.Surface, Blank(form.Surface))
            .Set(c => c.OpeningTime, ParseTime(form.OpeningTime))
            .Set(c => c.ClosingTime, ParseTime(form.ClosingTime))
            .Update();

        // Replace the child rows
        await client.From<CourtSport>().Filter("court_id", Operator.Equals, idText).Delete();
        await client.From<CourtAmenity>().Filter("court_id", Operator.Equals, idText).Delete();
        await client.From<BookingLink>().Filter("court_id", Operator.Equals, idText).Delete();
        await InsertChildrenAsync(id, form);
    }

    public async Task DeleteAsync(Guid id)
    {
        await gate.EnsureInitializedAsync();
        RequireUser();
        var idText = id.ToString();

        // Storage files first (the court_photos rows disappear with the court)
        var photos = await GetPhotosAsync(id);
        if (photos.Count > 0)
            await client.Storage.From(PhotoBucket).Remove(photos.Select(p => p.StoragePath).ToList());

        await client.From<Court>().Filter("id", Operator.Equals, idText).Delete();
    }

    private async Task InsertChildrenAsync(Guid courtId, CourtForm form)
    {
        var sports = form.Sports
            .Where(s => s.Selected)
            .Select(s => new CourtSport
            {
                CourtId = courtId,
                SportId = s.SportId,
                CourtCount = (short)s.CourtCount,
                PricePerHour = s.Price ?? 0
            })
            .ToList();
        if (sports.Count > 0) await client.From<CourtSport>().Insert(sports);

        var amenities = form.AmenityIds
            .Select(a => new CourtAmenity { CourtId = courtId, AmenityId = a })
            .ToList();
        if (amenities.Count > 0) await client.From<CourtAmenity>().Insert(amenities);

        var bookings = form.Bookings
            .Where(b => !string.IsNullOrWhiteSpace(b.Value))
            .Take(MaxBookings)
            .Select(b => new BookingLink { CourtId = courtId, Kind = b.Kind, Value = b.Value.Trim() })
            .ToList();
        if (bookings.Count > 0) await client.From<BookingLink>().Insert(bookings);
    }

    // ---------- Photos ----------

    public async Task<CourtPhoto> AddPhotoAsync(Guid courtId, byte[] data, string contentType)
    {
        await gate.EnsureInitializedAsync();
        RequireUser();

        if (!PhotoTypes.TryGetValue(contentType, out var ext))
            throw new ArgumentException("Only JPG, PNG and WebP photos are allowed.");
        if (data.Length > MaxPhotoBytes)
            throw new ArgumentException("Photos must be 5 MB or smaller.");

        var path = $"{courtId}/{Guid.NewGuid():N}.{ext}";
        var bucket = client.Storage.From(PhotoBucket);

        await bucket.Upload(data, path, new Supabase.Storage.FileOptions
        {
            ContentType = contentType,
            Upsert = false
        });

        try
        {
            var last = (await client.From<CourtPhoto>()
                .Filter("court_id", Operator.Equals, courtId.ToString())
                .Order("sort_order", Ordering.Descending)
                .Limit(1)
                .Get()).Models.FirstOrDefault();

            var photo = new CourtPhoto
            {
                CourtId = courtId,
                StoragePath = path,
                SortOrder = (short)((last?.SortOrder ?? -1) + 1)
            };

            var res = await client.From<CourtPhoto>().Insert(photo);
            return res.Models.First();
        }
        catch
        {
            // Don't leave an orphaned file in the bucket
            try { await bucket.Remove(new List<string> { path }); }
            catch (Exception) { }
            throw;
        }
    }

    public async Task RemovePhotoAsync(CourtPhoto photo)
    {
        await gate.EnsureInitializedAsync();
        RequireUser();

        await client.Storage.From(PhotoBucket).Remove(new List<string> { photo.StoragePath });
        await client.From<CourtPhoto>()
            .Filter("id", Operator.Equals, photo.Id.ToString())
            .Delete();
    }

    /// <summary>Makes a photo the cover by sorting it before all the others.</summary>
    public async Task MakeCoverAsync(CourtPhoto photo)
    {
        await gate.EnsureInitializedAsync();
        RequireUser();

        var first = (await client.From<CourtPhoto>()
            .Filter("court_id", Operator.Equals, photo.CourtId.ToString())
            .Order("sort_order", Ordering.Ascending)
            .Limit(1)
            .Get()).Models.FirstOrDefault();

        if (first is null || first.Id == photo.Id) return;

        await client.From<CourtPhoto>()
            .Filter("id", Operator.Equals, photo.Id.ToString())
            .Set(p => p.SortOrder, (short)(first.SortOrder - 1))
            .Update();
    }

    // ---------- Helpers ----------

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeSpan? ParseTime(string? value) =>
        TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var t) ? t : null;

    private static string? FormatTime(TimeSpan? t) => t?.ToString(@"hh\:mm");
}