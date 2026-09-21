import { useEffect, useState } from 'react';
import { Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { cn } from 'cn';

/**
 * A debounced search box. Controlled by `value`/`onChange` like a normal input, but only calls
 * `onChange` `debounceMs` after the user stops typing, so callers can filter a table on each
 * change without re-filtering on every keystroke.
 */
export function SearchInput({ value = '', onChange, placeholder = 'Search…', debounceMs = 200, className }) {
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  useEffect(() => {
    const handle = setTimeout(() => {
      if (draft !== value) onChange(draft);
    }, debounceMs);
    return () => clearTimeout(handle);
    // Only re-run when the draft changes; `value`/`onChange` are read for comparison, not deps.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draft, debounceMs]);

  return (
    <div className={cn('relative', className)}>
      <Search className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
      <Input
        type="search"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        placeholder={placeholder}
        className="pl-8"
      />
    </div>
  );
}

export default SearchInput;
