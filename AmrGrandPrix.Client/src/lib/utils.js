export { cn } from "cn"

/**
 * Formats a date-only string (e.g. "2026-05-21", as returned by .NET's DateOnly)
 * for display without shifting days due to timezone conversion. `new Date("2026-05-21")`
 * parses as UTC midnight, so `toLocaleDateString()` in a timezone behind UTC (e.g. US)
 * renders the previous day.
 */
export function formatDateOnly(dateString, options = { year: "numeric", month: "long", day: "numeric" }) {
  if (!dateString) return ""
  const [year, month, day] = dateString.split("T")[0].split("-").map(Number)
  return new Date(year, month - 1, day).toLocaleDateString("en-US", options)
}
