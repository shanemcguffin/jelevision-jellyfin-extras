using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JelevisionExtras.Matching;

/// <summary>
/// Maps TheDiscDb item types and titles to Jellyfin extra types.
/// </summary>
public static class ExtraTypeMapper
{
    /// <summary>
    /// Maps a disc title to a Jellyfin extra type.
    /// </summary>
    /// <param name="discDbType">TheDiscDb type.</param>
    /// <param name="title">Human-readable title.</param>
    /// <returns>The mapped type, or null for unsupported disc content.</returns>
    public static ExtraType? Map(string? discDbType, string? title)
    {
        if (string.IsNullOrWhiteSpace(discDbType))
        {
            return null;
        }

        if (discDbType.Equals("MainMovie", StringComparison.OrdinalIgnoreCase)
            || discDbType.Equals("Episode", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Enum.TryParse<ExtraType>(discDbType, true, out var direct)
            && direct is not ExtraType.Unknown)
        {
            return direct;
        }

        var normalizedTitle = title ?? string.Empty;
        if (ContainsAny(normalizedTitle, "trailer", "teaser", "promo"))
        {
            return ExtraType.Trailer;
        }

        if (ContainsAny(normalizedTitle, "deleted scene", "alternate scene", "alternate ending"))
        {
            return ExtraType.DeletedScene;
        }

        if (ContainsAny(normalizedTitle, "interview", "conversation", "q&a", "q & a"))
        {
            return ExtraType.Interview;
        }

        if (ContainsAny(
                normalizedTitle,
                "behind the scenes",
                "making of",
                "the making",
                "inside ",
                "on set",
                "production diary"))
        {
            return ExtraType.BehindTheScenes;
        }

        if (ContainsAny(normalizedTitle, "scene breakdown", "anatomy of"))
        {
            return ExtraType.Scene;
        }

        if (ContainsAny(normalizedTitle, "short film", " short"))
        {
            return ExtraType.Short;
        }

        if (discDbType.Equals("Other", StringComparison.OrdinalIgnoreCase)
            || discDbType.Equals("Music", StringComparison.OrdinalIgnoreCase))
        {
            return ExtraType.Clip;
        }

        return ExtraType.Featurette;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
