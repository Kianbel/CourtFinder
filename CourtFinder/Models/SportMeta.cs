namespace CourtFinder.Models;

public static class SportMeta
{
    public static string Emoji(string? slug) => slug switch
    {
        "pickleball" => "🏓",
        "badminton" => "🏸",
        "basketball" => "🏀",
        "volleyball" => "🏐",
        "tennis" => "🎾",
        "futsal" => "⚽",
        _ => "🏟️"
    };

    public static string Label(string slug) =>
        string.IsNullOrEmpty(slug) ? slug : char.ToUpper(slug[0]) + slug[1..];
}