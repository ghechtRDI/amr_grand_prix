/**
 * Display components shared by the Racer Management page and its dialogs.
 */

import { Link } from 'react-router-dom';
import { Badge } from '@/components/ui/badge';
import { formatBirth, formatResults } from './format';

export function AlternateNameChips({ names, emptyText = 'None' }) {
  if (!names?.length) return <span className="text-muted-foreground">{emptyText}</span>;
  return (
    <span className="flex flex-wrap gap-1">
      {names.map((n) => (
        <Badge key={n} variant="outline" className="font-normal">{n}</Badge>
      ))}
    </span>
  );
}

/** Compact two-column summary of one runner record. */
export function RunnerSummary({ runner, showLink = true }) {
  return (
    <div className="flex min-w-0 flex-col gap-1 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        {showLink ? (
          <Link
            to={`/runners/${runner.runnerId}`}
            target="_blank"
            rel="noopener noreferrer"
            className="font-medium text-foreground hover:underline"
          >
            {runner.fullName}
          </Link>
        ) : (
          <span className="font-medium text-foreground">{runner.fullName}</span>
        )}
        {runner.linkedAccountEmail && <Badge variant="secondary">Linked account</Badge>}
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-muted-foreground">
        <dt>Results</dt>
        <dd className="text-foreground">{formatResults(runner)}</dd>
        <dt>Gender</dt>
        <dd className="text-foreground">{runner.gender}</dd>
        <dt>Born</dt>
        <dd className="text-foreground">{formatBirth(runner)}</dd>
        {runner.preferredName && (
          <>
            <dt>Preferred</dt>
            <dd className="text-foreground">{runner.preferredName}</dd>
          </>
        )}
        {runner.alternateNames?.length > 0 && (
          <>
            <dt>Also known as</dt>
            <dd><AlternateNameChips names={runner.alternateNames} /></dd>
          </>
        )}
      </dl>
    </div>
  );
}
