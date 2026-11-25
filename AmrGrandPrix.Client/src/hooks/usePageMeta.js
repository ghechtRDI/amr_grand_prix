/**
 * usePageMeta - per-page <title>, meta description, canonical link, and robots noindex.
 *
 * Updates the tags already in <head> (the server fills in the same title, description, and
 * canonical for each page, plus Open Graph tags) rather than rendering new ones, so there is never
 * a duplicate tag for a crawler to pick the wrong one from. Unmounting restores the site defaults,
 * so a page that forgets to call this never inherits the previous page's title or noindex.
 *
 * The client's robots tag is its own (marked data-page-meta): it never removes a noindex the
 * server sent, e.g. for a runner under 18, whose age the client doesn't know.
 */

import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

export const SITE_NAME = 'Alaska Mountain Runners';
export const DEFAULT_TITLE = 'Alaska Mountain Runners Race Results';
export const DEFAULT_DESCRIPTION =
  "Results from Alaska's mountain and trail running races, browsable by race, year, and athlete, plus the AMR Grand Prix season standings.";

/** "Mount Marathon 2026 Results" -> "Mount Marathon 2026 Results | Alaska Mountain Runners" */
export function pageTitle(title) {
  return title ? `${title} | ${SITE_NAME}` : DEFAULT_TITLE;
}

function upsertHeadTag(selector, create) {
  let el = document.head.querySelector(selector);
  if (!el) {
    el = create();
    document.head.appendChild(el);
  }
  return el;
}

function setMeta(name, content) {
  if (content == null) {
    document.head.querySelector(`meta[name="${name}"]`)?.remove();
    return;
  }
  const el = upsertHeadTag(`meta[name="${name}"]`, () => {
    const m = document.createElement('meta');
    m.setAttribute('name', name);
    return m;
  });
  el.setAttribute('content', content);
}

function setNoIndex(noindex) {
  const selector = 'meta[name="robots"][data-page-meta]';
  if (!noindex) {
    document.head.querySelector(selector)?.remove();
    return;
  }
  upsertHeadTag(selector, () => {
    const m = document.createElement('meta');
    m.setAttribute('name', 'robots');
    m.setAttribute('content', 'noindex');
    m.setAttribute('data-page-meta', '');
    return m;
  });
}

function setCanonical(href) {
  if (href == null) {
    document.head.querySelector('link[rel="canonical"]')?.remove();
    return;
  }
  const el = upsertHeadTag('link[rel="canonical"]', () => {
    const l = document.createElement('link');
    l.setAttribute('rel', 'canonical');
    return l;
  });
  el.setAttribute('href', href);
}

/**
 * @param {object} meta
 * @param {string} [meta.title] Page title without the site suffix; omit for the site default.
 *   Pass `rawTitle` instead to use a title verbatim (the landing page).
 * @param {string} [meta.rawTitle]
 * @param {string} [meta.description] Defaults to the site description.
 * @param {string} [meta.canonical] Absolute URL or path; defaults to the current path without
 *   its query string. Ignored when `noindex` is set.
 * @param {boolean} [meta.noindex] Keep this page out of search engines.
 */
export function usePageMeta({ title, rawTitle, description, canonical, noindex = false } = {}) {
  const { pathname } = useLocation();

  useEffect(() => {
    document.title = rawTitle ?? pageTitle(title);
    setMeta('description', description ?? DEFAULT_DESCRIPTION);
    setNoIndex(noindex);
    setCanonical(noindex ? null : new URL(canonical ?? pathname, window.location.origin).href);
  }, [title, rawTitle, description, canonical, noindex, pathname]);

  // Restore defaults only on unmount, not between updates, so a title change doesn't flash.
  useEffect(
    () => () => {
      document.title = DEFAULT_TITLE;
      setMeta('description', DEFAULT_DESCRIPTION);
      setNoIndex(false);
      setCanonical(null);
    },
    []
  );
}
