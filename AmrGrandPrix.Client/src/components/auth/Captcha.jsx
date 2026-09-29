/**
 * Captcha
 * Wraps the Cloudflare Turnstile widget. Renders nothing (and lets the caller skip verification)
 * when no site key is configured, so local development works without Turnstile keys set up.
 */

import { useEffect, useId, useRef } from 'react';
import { useTheme } from '../../hooks/useTheme';

const TURNSTILE_SITE_KEY = import.meta.env.VITE_TURNSTILE_SITE_KEY;
const SCRIPT_SRC = 'https://challenges.cloudflare.com/turnstile/v0/api.js';

let scriptLoadingPromise = null;
const loadTurnstileScript = () => {
  if (window.turnstile) return Promise.resolve();
  if (scriptLoadingPromise) return scriptLoadingPromise;

  scriptLoadingPromise = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = SCRIPT_SRC;
    script.async = true;
    script.defer = true;
    script.onload = resolve;
    script.onerror = reject;
    document.head.appendChild(script);
  });

  return scriptLoadingPromise;
};

export const Captcha = ({ onVerify, onExpire }) => {
  const containerId = `turnstile-${useId().replace(/:/g, '')}`;
  const widgetIdRef = useRef(null);
  const { theme } = useTheme();

  useEffect(() => {
    if (!TURNSTILE_SITE_KEY) return undefined;

    let cancelled = false;

    loadTurnstileScript().then(() => {
      if (cancelled || !window.turnstile) return;
      widgetIdRef.current = window.turnstile.render(`#${containerId}`, {
        sitekey: TURNSTILE_SITE_KEY,
        theme: theme === 'dark' ? 'dark' : 'light',
        callback: onVerify,
        'expired-callback': onExpire,
        'error-callback': onExpire,
      });
    });

    return () => {
      cancelled = true;
      if (widgetIdRef.current && window.turnstile) {
        window.turnstile.remove(widgetIdRef.current);
        // A theme change tears down and re-renders the widget, invalidating any token already
        // collected from the old instance — clear it so a stale token can't be submitted.
        onExpire?.();
      }
    };
    // Re-render (rather than update) the widget when the app's light/dark theme changes;
    // onVerify/onExpire are stable enough in practice not to need re-rendering for.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [containerId, theme]);

  if (!TURNSTILE_SITE_KEY) {
    return (
      <p className="text-xs text-muted-foreground">
        Captcha is not configured for this environment.
      </p>
    );
  }

  return <div id={containerId} />;
};

export default Captcha;
