/**
 * NotFound Page
 * Shown for unknown routes instead of silently redirecting home.
 */

import { Link } from 'react-router-dom';
import { MountainSnow } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export default function NotFound() {
  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader className="items-center justify-items-center text-center">
          <MountainSnow className="size-10 text-primary" aria-hidden="true" />
          <CardTitle className="text-2xl">Off the trail</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col items-center gap-5 text-center">
          <p className="text-muted-foreground">We couldn&apos;t find the page you were looking for.</p>
          <div className="flex flex-wrap justify-center gap-3">
            <Button size="lg" nativeButton={false} render={<Link to="/">Go Home</Link>} />
            <Button size="lg" variant="outline" nativeButton={false} render={<Link to="/race-results">Race Results</Link>} />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
