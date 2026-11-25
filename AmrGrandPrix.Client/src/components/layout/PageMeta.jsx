import { usePageMeta } from '@/hooks/usePageMeta';

/**
 * Route-level wrapper for pages whose metadata doesn't depend on their data (auth forms, admin
 * pages, static listings). Pages with data-driven titles call usePageMeta themselves instead.
 */
export default function PageMeta({ children, ...meta }) {
  usePageMeta(meta);
  return children;
}
