using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using AmrGrandPrix.API.Services.Seo;

namespace AmrGrandPrix.API.Controllers;

/// <summary>
/// <c>/robots.txt</c> and <c>/sitemap.xml</c>, served from the site root for search engines.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class SeoController : ControllerBase
{
    internal const string SitemapCacheKey = "seo:sitemap";
    internal static readonly TimeSpan SitemapCacheDuration = TimeSpan.FromHours(1);

    private readonly SitemapBuilder _sitemap;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;

    public SeoController(SitemapBuilder sitemap, IMemoryCache cache, IConfiguration config)
    {
        _sitemap = sitemap;
        _cache = cache;
        _config = config;
    }

    [HttpGet("/robots.txt")]
    public ContentResult Robots() => Content(
        $"""
        User-agent: *
        Disallow: /api/
        Disallow: /admin/
        Disallow: /login
        Disallow: /profile

        Sitemap: {SiteUrl()}/sitemap.xml

        """,
        "text/plain; charset=utf-8");

    [HttpGet("/sitemap.xml")]
    public async Task<ContentResult> Sitemap(CancellationToken ct)
    {
        var xml = await _cache.GetOrCreateAsync(SitemapCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SitemapCacheDuration;
            return _sitemap.BuildAsync(SiteUrl(), SearchIndexing.Today, ct);
        });

        return Content(xml!, "application/xml; charset=utf-8");
    }

    /// <summary>
    /// The public origin, from <c>Site:PublicUrl</c> rather than the request's Host header so a
    /// spoofed Host can't end up in the cached sitemap. Falls back to the request when unset.
    /// </summary>
    private string SiteUrl() =>
        (_config["Site:PublicUrl"] is { Length: > 0 } configured
            ? configured
            : $"{Request.Scheme}://{Request.Host}").TrimEnd('/');
}
