namespace CourtFinder.Models;

/// <summary>Turns owner-entered booking values into safe, display-ready links.</summary>
public static class BookingUi
{
    public static string Icon(string kind) => kind switch
    {
        "url" => "🌐",
        "messenger" => "💬",
        "phone" => "📞",
        "walk_in" => "🚶",
        _ => "🔗"
    };

    public static string Label(string kind) => kind switch
    {
        "url" => "Book online",
        "messenger" => "Message on Messenger",
        "phone" => "Call to book",
        "walk_in" => "Walk-in",
        _ => "Book"
    };

    /// <summary>A safe href, or null when the value can't be turned into a valid link.</summary>
    public static string? Href(BookingLink link) => link.Kind switch
    {
        "url" => WebUrl(link.Value),
        "messenger" => WebUrl(link.Value, "https://m.me/"),
        "phone" => Tel(link.Value),
        _ => null
    };

    public static bool IsExternal(BookingLink link) => link.Kind is "url" or "messenger";

    /// <summary>Short text shown under the button label.</summary>
    public static string Display(BookingLink link)
    {
        if (link.Kind is "url" or "messenger")
        {
            var href = Href(link);
            if (href is not null && Uri.TryCreate(href, UriKind.Absolute, out var uri))
                return uri.Host;
        }
        return link.Value.Trim();
    }

    private static string? WebUrl(string value, string? bareHandlePrefix = null)
    {
        var v = value.Trim();
        if (v.Length == 0) return null;

        if (!v.Contains("://"))
        {
            // "mycourt" -> m.me/mycourt for Messenger; "site.com/book" -> https://site.com/book
            v = bareHandlePrefix is not null && !v.Contains('.') && !v.Contains('/')
                ? bareHandlePrefix + v
                : "https://" + v;
        }

        // Only real web links are allowed (blocks javascript:, data:, etc.)
        return Uri.TryCreate(v, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.AbsoluteUri
            : null;
    }

    private static string? Tel(string value)
    {
        var cleaned = new string(value.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
        return cleaned.Length >= 5 ? "tel:" + cleaned : null;
    }
}