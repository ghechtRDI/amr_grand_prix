namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// Site-wide metadata. The titles and descriptions match the client's <c>hooks/usePageMeta.js</c>,
/// so a page's tags don't change once the app takes over in the browser.
/// </summary>
public static class SiteMeta
{
    public const string SiteName = "Alaska Mountain Runners";
    public const string DefaultTitle = "Alaska Mountain Runners Race Results";
    public const string DefaultDescription =
        "Results from Alaska's mountain and trail running races, browsable by race, year, and athlete, plus the AMR Grand Prix season standings.";

    public const string ClubUrl = "https://alaskamountainrunners.org";

    /// <summary>Default link-preview image, from <c>AmrGrandPrix.Client/public</c>.</summary>
    public const string OgImagePath = "/og-image.png";
    public const int OgImageWidth = 1200;
    public const int OgImageHeight = 630;
    public const string OgImageAlt = "Alaska Mountain Runners race results, over a mountain ridgeline";

    /// <summary>
    /// The public origin, from <c>Site:PublicUrl</c> rather than the request's Host header so a
    /// spoofed Host can't end up in cached or shared output. Falls back to the request when unset.
    /// </summary>
    public static string SiteUrl(IConfiguration config, HttpRequest request) =>
        (config["Site:PublicUrl"] is { Length: > 0 } configured
            ? configured
            : $"{request.Scheme}://{request.Host}").TrimEnd('/');
}
