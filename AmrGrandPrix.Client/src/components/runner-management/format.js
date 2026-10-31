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

/** "12 results · 2019–2025" */
export function formatResults(runner) {
  const count = `${runner.resultCount} result${runner.resultCount === 1 ? '' : 's'}`;
  if (!runner.firstYear) return count;
  const years = runner.firstYear === runner.lastYear ? runner.firstYear : `${runner.firstYear}–${runner.lastYear}`;
  return `${count} · ${years}`;
}
