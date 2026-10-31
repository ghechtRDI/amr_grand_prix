import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { GENDERS, genderLabel } from './constants';

const RANK_TINTS = {
  1: 'border-amber-400/50 bg-amber-400/20 text-amber-700 dark:text-amber-400',
  2: 'border-slate-400/50 bg-slate-400/20 text-slate-600 dark:text-slate-300',
  3: 'border-orange-700/40 bg-orange-700/15 text-orange-700 dark:text-orange-400',
};

export function RankBadge({ rank }) {
  return (
    <span
      className={cn(
        'inline-flex size-7 items-center justify-center rounded-full border text-sm font-bold tabular-nums',
        RANK_TINTS[rank] || 'border-transparent bg-muted text-muted-foreground'
      )}
    >
      {rank}
    </span>
  );
}

export function GenderToggle({ value, onChange }) {
  return (
    <div className="inline-flex overflow-hidden rounded-lg border border-border">
      {GENDERS.map((g, i) => (
        <Button
          key={g}
          type="button"
          size="sm"
          variant={value === g ? 'default' : 'ghost'}
          className={cn('rounded-none', i > 0 && 'border-l border-border')}
          onClick={() => onChange(g)}
        >
          {genderLabel(g)}
        </Button>
      ))}
    </div>
  );
}
