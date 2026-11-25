namespace AmrGrandPrix.API.Services.Seo;

/// <summary>
/// The built SPA's <c>index.html</c> (copied into wwwroot by <c>Dockerfile.production</c>), read
/// once and cached, with per-request tags filled in. A deploy restarts the container, so the cache
/// never outlives the build it came from. Without a build (tests, or the API run on its own in
/// development, where Vite serves the client) a bare app shell stands in.
/// </summary>
public class SpaIndexHtml
{
    internal const string FallbackShell =
        "<!doctype html><html lang=\"en\"><head><meta charset=\"UTF-8\" />" +
        "<title>Alaska Mountain Runners Race Results</title></head>" +
        "<body><div id=\"root\"></div></body></html>";

    private const string NoIndexTag = "<meta name=\"robots\" content=\"noindex\" />";

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

    public string Render(SpaPage page)
    {
        var html = _html.Value;
        if (!page.NoIndex)
            return html;

        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEnd < 0 ? html : html.Insert(headEnd, NoIndexTag);
    }
}
