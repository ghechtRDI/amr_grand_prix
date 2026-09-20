/**
 * Unauthorized Page
 * Shown when user doesn't have required role
 */

import { Link } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export const Unauthorized = () => {
  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader className="items-center justify-items-center text-center">
          <ShieldAlert className="size-10 text-destructive" />
          <CardTitle className="text-2xl">403 - Unauthorized</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col items-center gap-5 text-center">
          <p className="text-muted-foreground">You don&apos;t have permission to access this page.</p>
          <Button size="lg" nativeButton={false} render={<Link to="/">Go Home</Link>} />
        </CardContent>
      </Card>
    </div>
  );
};

export default Unauthorized;
