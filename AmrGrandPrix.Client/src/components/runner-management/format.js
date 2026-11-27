/**
 * Formatting helpers and constants shared by the Racer Management page and its dialogs.
 */

import { formatDateOnly } from '@/lib/utils';

export const GENDER_OPTIONS = ['Male', 'Female', 'Nonbinary'];

const SHORT_DATE = { year: 'numeric', month: 'short', day: 'numeric' };

/** "Mar 14, 1990", "~1990 (est.)", or an em dash. */
export function formatBirth(runner) {
  if (runner?.dateOfBirth) return formatDateOnly(runner.dateOfBirth, SHORT_DATE);
  if (runner?.estimatedBirthYear) return `~${runner.estimatedBirthYear} (est.)`;
  return '—';
}

const MONTH_YEAR = { year: 'numeric', month: 'short' };

/**
 * The runner's estimated birth date range in words: "born Dec 1985 – Jun 1986 (~1985)",
 * "born Jun 1976 – Jun 1986", "born before Jun 1936", or null when nothing is estimated.
 */
export function describeEstimatedBirth(runner) {
  const earliest = runner?.estimatedDobEarliest;
  const latest = runner?.estimatedDobLatest;
  if (!earliest && !latest) return null;
  const year = runner.estimatedBirthYear ? ` (~${runner.estimatedBirthYear})` : '';
  if (earliest && latest) {
    return `born ${formatDateOnly(earliest, MONTH_YEAR)} – ${formatDateOnly(latest, MONTH_YEAR)}${year}`;
  }
  return earliest
    ? `born after ${formatDateOnly(earliest, MONTH_YEAR)}`
    : `born before ${formatDateOnly(latest, MONTH_YEAR)}`;
}

/** "12 results · 2019–2025" */
export function formatResults(runner) {
  const count = `${runner.resultCount} result${runner.resultCount === 1 ? '' : 's'}`;
  if (!runner.firstYear) return count;
  const years = runner.firstYear === runner.lastYear ? runner.firstYear : `${runner.firstYear}–${runner.lastYear}`;
  return `${count} · ${years}`;
}
