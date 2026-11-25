using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// The built SPA's <c>index.html</c> (copied into wwwroot by <c>Dockerfile.production</c>), read
/// once and cached, with each page's title, description, canonical, robots, Open Graph/Twitter, and
/// JSON-LD tags filled in. A deploy restarts the container, so the cache never outlives the build
/// it came from. Without a build (tests, or the API run on its own in development, where Vite serves
/// the client) a bare app shell stands in.
/// </summary>
public partial class SpaIndexHtml
{
    internal const string FallbackShell =
        "<!doctype html><html lang=\"en\"><head><meta charset=\"UTF-8\" />" +
        "<title>Alaska Mountain Runners Race Results</title></head>" +
        "<body><div id=\"root\"></div></body></html>";

    // System.Text.Json's default encoder escapes <, >, and &, so no value can close the script tag
    private static readonly JsonSerializerOptions JsonLdOptions = new();

    [GeneratedRegex(@"<title>.*?</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"\s*<meta\s+name=""description""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionTag();

    private readonly Lazy<string> _html;

    public SpaIndexHtml(IWebHostEnvironment env)
    {
        _html = new Lazy<string>(() =>
        {
            var file = env.WebRootFileProvider.GetFileInfo("index.html");
            if (!file.Exists)
                return FallbackShell;

            using var reader = new StreamReader(file.CreateReadStream());
            return reader.ReadToEnd();
        });
    }

    public string Render(SpaPage page, string siteUrl)
    {
        var html = _html.Value;
        var title = Encode(page.FullTitle);
        var description = page.Description ?? SiteMeta.DefaultDescription;

        var titleTag = TitleTag();
        html = titleTag.IsMatch(html)
            ? titleTag.Replace(html, $"<title>{title}</title>", 1)
            : InsertBeforeHeadEnd(html, $"<title>{title}</title>");
        html = DescriptionTag().Replace(html, "", 1);

        var tags = new StringBuilder("\n");
        void Meta(string attr, string key, string value) =>
            tags.Append($"    <meta {attr}=\"{key}\" content=\"{Encode(value)}\" />\n");

        Meta("name", "description", description);
        if (page.NoIndex)
            Meta("name", "robots", "noindex");

        var canonical = page.CanonicalPath is null ? null : siteUrl + page.CanonicalPath;
        if (canonical is not null)
            tags.Append($"    <link rel=\"canonical\" href=\"{Encode(canonical)}\" />\n");

        var image = siteUrl + SiteMeta.OgImagePath;
        Meta("property", "og:site_name", SiteMeta.SiteName);
        Meta("property", "og:type", "website");
        Meta("property", "og:title", page.FullTitle);
        Meta("property", "og:description", description);
        if (canonical is not null)
            Meta("property", "og:url", canonical);
        Meta("property", "og:image", image);
        Meta("property", "og:image:width", SiteMeta.OgImageWidth.ToString());
        Meta("property", "og:image:height", SiteMeta.OgImageHeight.ToString());
        Meta("property", "og:image:alt", SiteMeta.OgImageAlt);
        Meta("name", "twitter:card", "summary_large_image");
        Meta("name", "twitter:title", page.FullTitle);
        Meta("name", "twitter:description", description);
        Meta("name", "twitter:image", image);

        foreach (var item in page.JsonLd)
            tags.Append($"    <script type=\"application/ld+json\">{JsonSerializer.Serialize(item, JsonLdOptions)}</script>\n");

        return InsertBeforeHeadEnd(html, tags.Append("  ").ToString());
    }

    // Leaves non-ASCII (e.g. the en dash in runner titles) readable; quotes and <>& are still encoded
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    private static string Encode(string value) => Encoder.Encode(value);

    private static string InsertBeforeHeadEnd(string html, string content)
    {
        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEnd < 0 ? html : html.Insert(headEnd, content);
    }
}
