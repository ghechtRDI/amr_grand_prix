/**
 * RouteTracker
 * Sends a GA4 page_view on every client-side route change (see lib/analytics.js). Renders nothing.
 *
 * Waits for the auth check so Admins/Managers are never tracked, then gives each page a moment
 * for its route chunk and data to load, so the page_view carries the page's real title (set by
 * usePageMeta) rather than the loading title. Leaving sooner sends it right away, with the URL
 * it was scheduled for and the best title available by then.
 */

import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '@/hooks/useAuth';
import { analyticsEnabled, initAnalytics, setInternalTraffic, trackPageView } from '@/lib/analytics';

const TITLE_SETTLE_MS = 1000;

export default function RouteTracker() {
  const { pathname, search } = useLocation();
  const { loading, hasAnyRole } = useAuth();
  const internal = hasAnyRole(['Admin', 'Manager']);

  useEffect(() => {
    setInternalTraffic(internal);
  }, [internal]);

  useEffect(() => {
    if (!analyticsEnabled || loading || internal) return undefined;
    initAnalytics();

    const url = new URL(window.location.href);
    let sent = false;
    const send = () => {
      if (sent) return;
      sent = true;
      trackPageView(url, document.title);
    };
    const timer = setTimeout(send, TITLE_SETTLE_MS);
    return () => {
      clearTimeout(timer);
      send();
    };
  }, [pathname, search, loading, internal]);

  return null;
}
