/**
 * Shared helpers for the .NET TimeSpan strings the API returns for race times, e.g. "00:12:34",
 * "1.02:03:04" (day-prefixed), or with fractional seconds.
 */

export function parseTimeSpanToSeconds(timeSpan) {
  if (!timeSpan) return null;
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+(?:\.\d+)?)$/.exec(timeSpan);
  if (!match) return null;

  const [, days, hours, minutes, seconds] = match;
  return (
    (parseInt(days || '0', 10) * 86400) +
    (parseInt(hours, 10) * 3600) +
    (parseInt(minutes, 10) * 60) +
    parseFloat(seconds)
  );
}

export function formatTime(timeSpan) {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  if (parts.length === 3) {
    const h = parseInt(parts[0], 10);
    const m = parts[1];
    const s = parseFloat(parts[2]).toFixed(0).padStart(2, '0');
    if (h === 0) return `${m}:${s}`;
    return `${h}:${m}:${s}`;
  }
  return timeSpan;
}

// Seconds -> "1:02:03" / "12:34", for chart axes and tooltips that work in numeric seconds.
export function formatSecondsAsClock(totalSeconds) {
  if (totalSeconds == null) return '';
  const h = Math.floor(totalSeconds / 3600);
  const m = Math.floor((totalSeconds % 3600) / 60);
  const s = Math.round(totalSeconds % 60);
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`;
}

// Percent behind the winner (e.g. 12.34 -> "+12.3%"); 0 means this runner won.
export function formatPercentBehind(pct) {
  if (pct == null) return '—';
  return pct === 0 ? 'Winner' : `+${pct.toFixed(1)}%`;
}
