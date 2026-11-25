# Public Landing Page, SEO, and Google Analytics Plan

## Goals

1. Replace the login screen at `/` with an attractive public landing page.
2. Make the site discoverable in search and produce good link previews when shared.
3. Add Google Analytics 4 to understand how the site is used.

## Current State

- **`/` sends visitors to `/login`.** The route is wrapped in `ProtectedRoute` (`AmrGrandPrix.Client/src/App.jsx`). The catch-all `*` route also redirects to `/`, so every mistyped URL ends up on the login screen.
- **No per-page metadata.** `index.html` has a single `<title>AMR Grand Prix</title>` and no description, Open Graph tags, or canonical link. No page sets its own title.
- **No `robots.txt` or `sitemap.xml`.** `Program.cs` uses `MapFallbackToFile("index.html")`, so every unknown path returns 200 with the app shell, which Google treats as a "soft 404".
- **The logged-in `Home.jsx` page duplicates the NavBar.** The NavBar already links to Standings, Race Results, Athletes, and all admin pages.

---

## Phase 1: Public Landing Page

### Routing

- Make `/` public and render a new `pages/Landing.jsx` in place of the protected `Home`.
- Delete `pages/Home.jsx`.
- After login, `LoginForm` returns the user to `location.state.from` if present, otherwise `/`. `ProtectedRoute` passes `state={{ from: location }}` when redirecting.
- Add a real `pages/NotFound.jsx` for the catch-all route instead of redirecting to `/`.
- The NavBar logo and the existing "← Home" links already point at `/` and need no change.

### Page Content (top to bottom)

**1. Site hero: what this site is**

The page first identifies the site, before anything Grand Prix specific:

- Headline: **Alaska Mountain Runners Race Results**
- Subhead: the home of past results from Alaska's mountain and trail running races, browsable by race, year, and athlete.
- Primary actions: **Browse Race Results** (`/race-results`) and **Find an Athlete** (`/athletes`).
- At-a-glance site totals, e.g. "142 races · 15 years · 3,800 athletes":
  - New anonymous endpoint `GET /api/stats/summary` returning `{ raceCount, yearCount, firstYear, latestYear, athleteCount }`.
  - Count only races that have at least one result, and only runners with at least one result, so empty race shells and orphaned runner records don't inflate the numbers.
  - Cache the response briefly (in-memory, a few minutes) since it is on the most-visited page.
  - Render as a single muted line under the subhead, with numbers formatted with thousands separators; hide the line if the request fails rather than showing zeros.
  - Covered by an integration test that checks the counts and that the endpoint is anonymous.
- Mountain illustration (see *Visual Design* below).

**2. Grand Prix feature section (prominent)**

A visually distinct band or card directly below the hero, the most prominent block after it:

- Heading: **The AMR Grand Prix**. Short intro: a season-long series scored across Alaska's classic mountain races.
- **The basics** as 3–4 compact rule tiles with icons, taken from `DOCS/GRAND_PRIX_RULES.md` / `GrandPrixConstants`:
  - **Open Division:** top 20 finishers per gender score 100 / 90 / 85 … 1; +10 for a course record.
  - **Age Division:** top 5 in each age category score 5 / 4 / 3 / 2 / 1.
  - **Best 4 races count** toward your season total.
  - **Run the Gamut:** finish 7+ of the year's Grand Prix events.
- **Standings snapshot:** the top 3–5 men's, women's, and nonbinary leaders (Open Division) for the **latest year with standings data**, with a "Full standings →" link to `/standings/{year}`. Use the latest year with data, not the calendar year, so the page isn't empty in January. `/api/standings/years` already returns years newest first. Label the snapshot "Final" or "Tentative" based on `GrandPrixSeason.IsFinalized`.
- Optionally, the season's Grand Prix race list with dates and links to each race's results.

**3. Welcome line (logged-in users only)**

- A small "Welcome back, {name}" line with a link to their results. Admins and Managers keep using the NavBar admin menu.

### Site-wide Footer

Add a `components/layout/Footer.jsx` rendered on every page from the app layout in `App.jsx`, not just the landing page:

- Links: Race Results, Athletes, Standings, Report an Issue, Privacy, and the Alaska Mountain Runners main site.
- Copyright: **© {current year} Alaska Mountain Runners**. Compute the year at render time so it never goes stale.
- Credit: **Designed and built by Galen Hecht**.
- Styling: a muted, compact bar using theme tokens, stacking to one column at phone width. A faint mountain ridge line along its top edge can echo the hero artwork.

### Visual Design: Minimal Mountain Iconography

The aim is something eye-catching but simple, using the existing theme tokens so it works in light and dark mode without extra assets.

- **Hero ridgeline:** an inline SVG of 2–3 layered mountain silhouettes (flat polygons, no detail) spanning the bottom of the hero. The back layers are lower-opacity tints of `--primary`/`--secondary`, and the front layer matches the page background so the section "rests" on the ridge.
  - Optional touches: a small sun or moon circle, and a thin dashed trail line climbing to a peak with a flag.
  - Build it as a `components/landing/MountainRidge.jsx` component with `currentColor` / `fill-primary/20`-style classes so it re-colors with the theme.
- **Section accents:** a small mountain glyph (lucide `Mountain` / `MountainSnow`) on the Grand Prix heading and rule tiles. Optionally, a thin peak-shaped divider between hero and Grand Prix section.
- **Motion (optional, subtle):** a slow parallax or fade-in on the ridge layers, disabled under `prefers-reduced-motion`.
- **Reuse:** export the ridge so the same mark can serve as the Open Graph image (Phase 2) and an updated `favicon.svg`, giving the site one consistent visual identity.
- **Constraints:** inline SVG only (no image downloads or new dependencies), responsive at phone width, and decorative SVGs get `aria-hidden="true"`.

---

## Phase 2: SEO

### Client-side basics

- Add a `usePageMeta({ title, description, canonical, noindex })` hook. React 19 renders `<title>` and `<meta>` from components, so no helmet library is needed. Give every page a unique title, e.g.:
  - Landing: "Alaska Mountain Runners Race Results"
  - "2026 Grand Prix Standings | Alaska Mountain Runners"
  - "Mount Marathon 2026 Results | Alaska Mountain Runners"
  - "Jane Doe – Race Results | Alaska Mountain Runners"
- `noindex` on `/login`, `/register`, `/confirm-email`, `/forgot-password`, `/reset-password`, `/profile`, `/admin/*`, `/unauthorized`, and NotFound.
- Check each public page has one meaningful `<h1>` and descriptive link text.

### Server-side metadata (biggest win)

Replace `MapFallbackToFile("index.html")` with a small endpoint that caches `index.html` and fills in `<title>`, meta description, canonical, and Open Graph/Twitter tags before serving:

| Route | Metadata source |
|---|---|
| `/` | Static site description |
| `/standings/{year}` | Year, leader names |
| `/races/{id}/results` | Race display name, date, finisher count |
| `/race-series/{id}` | Series name, variants |
| `/runners/{id}` | Runner display name, result count (respecting the junior policy below) |
| `/race-results`, `/athletes` | Static descriptions |

Google renders JavaScript, but Facebook, Slack, iMessage, and much of Bing do not, so filling in tags on the server gives correct link previews and solid indexing without SSR or prerendering.

- **Real 404s:** unknown paths and nonexistent IDs return HTTP 404 (still rendering the SPA NotFound page).
- **`/sitemap.xml`:** generated from the database: landing, standings years, series, races, and indexable runners, with `lastmod` taken from the latest result upload. Cache it for an hour or so.
- **`/robots.txt`:** disallow `/api/`, `/admin/`, `/login`, `/profile`; reference the sitemap.
- **Structured data (JSON-LD):** `Organization` and `WebSite` on the landing page, `SportsEvent` on race results pages.
- **Default Open Graph image:** 1200×630 PNG in `public/` built from the mountain ridge artwork and site name.

### Off-site

- Verify the domain in **Google Search Console** via a DNS TXT record in the Cloudflare Terraform module (`terraform/modules/vps_app/dns.tf`), then submit the sitemap. Import into Bing Webmaster Tools from Search Console.
- Ask alaskamountainrunners.org and the individual race sites to link to the results site; links from those sites will matter more than anything on-page.

### Decision: keep runners under 18 out of search engines

Approved: runners under 18 are not indexed.

- **Runner profiles (`/runners/{id}` and `/runners/{id}/series/{seriesId}`):** runners whose current age is under 18 (from DOB, or estimated from birth year as the Athletes directory already does) get `noindex`. Put it in the server-filled meta *and* send an `X-Robots-Tag: noindex` header so it works even for crawlers that don't run JavaScript. Leave these runners out of `sitemap.xml`.
- **Server-filled metadata:** for those runners, use a generic title/description ("Runner Profile | Alaska Mountain Runners") rather than their name, so their name doesn't appear in link previews either.
- **Unknown age:** a runner with no DOB and no birth-year estimate counts as a minor (fail closed).
- **Re-evaluated on every request,** so a runner becomes indexable on their 18th birthday with no extra work.
- **Out of scope:** their names still appear on race results and standings pages, which are indexed. Hiding minors' names there would change the public results, so it's a separate decision.
- **Later, optional:** a `Runner.ExcludeFromSearch` flag that admins can set on request so adults can opt out too.

---

## Phase 3: Google Analytics 4

- **Account:** the shared Alaska Mountain Runners Gmail account owns the GA4 property and the Search Console verification. Add individual maintainers as users rather than making a personal account the owner.
- **Configuration:** create a GA4 property; pass the measurement ID as `VITE_GA_MEASUREMENT_ID` through `Dockerfile.production` and CI, the same way `VITE_TURNSTILE_SITE_KEY` flows today. Unset (dev, sandbox) means GA never loads.
- **Loader:** `lib/analytics.js` injects `gtag.js` with `send_page_view: false` and exposes `trackPageView` / `trackEvent`.
- **SPA page views:** a `RouteTracker` component listens for route changes and sends `page_view` with the page path and title.
- **Exclude internal traffic:** skip `/admin/*` routes and logged-in Admin/Manager users so volunteers uploading results don't skew the numbers.
- **Custom events:**
  - standings tab, year, and gender changes
  - athlete search used (send that a search happened, never the search text, so no names go to Google)
  - result claim
  - registration
  - landing-page call-to-action clicks
- **Privacy:**
  - Add a short `/privacy` page (linked in the footer) that names Google Analytics.
  - Turn off Google signals and ad personalization in the GA property.
  - No consent banner is legally required for a US audience, but this can be revisited.
- **Content-Security-Policy:** none exists today. If one is added later, allow `www.googletagmanager.com` and `*.google-analytics.com`.

---

## Delivery Order

| PR | Scope | Size |
|---|---|---|
| 1 | Public landing page (site hero with totals endpoint, Grand Prix section, mountain SVG), site-wide footer, NotFound, return-to after login | M |
| 2 | `usePageMeta`, noindex rules, `robots.txt`, `sitemap.xml`, real 404s | M |
| 3 | Server-side meta and Open Graph filling, JSON-LD, OG image | M |
| 4 | GA4 loader, RouteTracker, custom events, privacy page | S |
| 5 | Search Console TXT record in Terraform; submit sitemap | XS |

PR 1 doesn't depend on the others and gives the most visible result right away.

## Decisions

1. **Junior indexing:** runners under 18 are kept out of search engines (see Phase 2).
2. **Google account:** the shared Alaska Mountain Runners Gmail account owns GA4 and Search Console.
3. **Site totals:** the landing-page hero shows race / year / athlete counts via `GET /api/stats/summary` (see Phase 1).
