import { AlertCircle } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';

/** Name, current age, and gender — the top of every runner page. */
export function RunnerHeader({ profile, children }) {
  return (
    <div>
      <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
        {profile.fullName}
      </h1>
      <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-muted-foreground">
        {profile.currentAge != null && (
          <span title={profile.isAgeEstimated ? 'Estimated from ages reported in race results' : undefined}>
            Age {profile.currentAge}
            {profile.isAgeEstimated && <span className="ml-1 text-xs">(est.)</span>}
          </span>
        )}
        <span>{profile.gender}</span>
        {children}
      </div>
    </div>
  );
}

/** Loading/error states shared by the runner pages, wrapped in the standard page shell. */
export function RunnerPageShell({ loading, error, children }) {
  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-6xl">
        {loading ? (
          <div className="space-y-4">
            <Skeleton className="h-8 w-64" />
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-64 w-full" />
          </div>
        ) : error ? (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : (
          children
        )}
      </div>
    </div>
  );
}
