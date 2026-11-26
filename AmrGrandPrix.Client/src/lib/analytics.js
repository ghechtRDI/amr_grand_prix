/**
 * Google Analytics 4.
 *
 * Loads only when VITE_GA_MEASUREMENT_ID is set at build time (production; unset in dev and the
 * sandbox), and only for visitors who aren't Admins/Managers, so volunteers uploading results don't
 * skew the numbers. Page views are sent by components/layout/RouteTracker.jsx, never automatically.
 *
 * Privacy: Google signals and ad personalization are off, and events never carry names or search
 * text. See pages/Privacy.jsx, which must stay in step with what is sent here.
 */

const MEASUREMENT_ID = import.meta.env.VITE_GA_MEASUREMENT_ID;

export const analyticsEnabled = !!MEASUREMENT_ID;

let loaded = false;
let internalTraffic = false;

/** Admin pages are never tracked, whoever is viewing them. */
const isExcludedPath = (pathname) => /^\/admin(\/|$)/i.test(pathname);

/** Injects gtag.js once. A no-op without a measurement ID. */
export function initAnalytics() {
  if (!MEASUREMENT_ID || loaded) return;
  loaded = true;

  window.dataLayer = window.dataLayer || [];
  window.gtag = function gtag() {
    // gtag.js expects the arguments object itself, not an array
    window.dataLayer.push(arguments);
  };
  window.gtag('js', new Date());
  window.gtag('config', MEASUREMENT_ID, {
    send_page_view: false,
    allow_google_signals: false,
    allow_ad_personalization_signals: false,
  });

  const script = document.createElement('script');
  script.async = true;
  script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(MEASUREMENT_ID)}`;
  document.head.appendChild(script);
}

/** Set while an Admin/Manager is signed in; nothing is sent meanwhile. */
export function setInternalTraffic(isInternal) {
  internalTraffic = isInternal;
}

function canSend(pathname = window.location.pathname) {
  return loaded && !internalTraffic && !isExcludedPath(pathname);
}

/**
 * A page view. Pass the URL captured when the page was shown: by the time a delayed page view
 * is sent, the browser may already be on the next page.
 * @param {URL} url
 * @param {string} title
 */
export function trackPageView(url, title) {
  if (!canSend(url.pathname)) return;
  window.gtag('event', 'page_view', {
    page_location: url.href,
    page_title: title,
  });
}

/**
 * A custom event. Keep params free of personal data: no names, emails, or search text.
 * @param {string} name snake_case event name, e.g. "standings_view_change"
 * @param {Record<string, string | number>} [params]
 */
export function trackEvent(name, params = {}) {
  if (!canSend()) return;
  window.gtag('event', name, params);
}
