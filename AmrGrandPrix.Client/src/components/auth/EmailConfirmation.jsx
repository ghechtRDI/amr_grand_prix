/**
 * EmailConfirmation Component
 * Email verification success/error page
 */

import { useState, useEffect } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { AlertCircle, CheckCircle2, Loader2 } from 'lucide-react';
import * as authService from '../../services/authService';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';

export const EmailConfirmation = () => {
  const [searchParams] = useSearchParams();
  const [status, setStatus] = useState('loading'); // loading, success, error
  const [message, setMessage] = useState('');

  useEffect(() => {
    const confirmEmail = async () => {
      const userId = searchParams.get('userId');
      const token = searchParams.get('token');

      if (!userId || !token) {
        setStatus('error');
        setMessage('Invalid confirmation link. Please check your email and try again.');
        return;
      }

      try {
        await authService.confirmEmail(userId, token);
        setStatus('success');
        setMessage('Your email has been confirmed successfully! You can now login to your account.');
      } catch (error) {
        setStatus('error');
        setMessage(error.message || 'Email confirmation failed. The link may have expired or is invalid.');
      }
    };

    confirmEmail();
  }, [searchParams]);

  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">
            {status === 'loading' && 'Confirming Email...'}
            {status === 'success' && 'Email Confirmed!'}
            {status === 'error' && 'Confirmation Failed'}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col items-center gap-5">
          {status === 'loading' && (
            <div className="flex flex-col items-center gap-3 text-muted-foreground">
              <Loader2 className="size-6 animate-spin" />
              <p>Please wait while we confirm your email address.</p>
            </div>
          )}

          {status === 'success' && (
            <>
              <Alert variant="success" className="w-full">
                <CheckCircle2 />
                <AlertDescription>{message}</AlertDescription>
              </Alert>
              <Button
                className="w-full"
                size="lg"
                nativeButton={false}
                render={<Link to="/login">Go to Login</Link>}
              />
            </>
          )}

          {status === 'error' && (
            <>
              <Alert variant="destructive" className="w-full">
                <AlertCircle />
                <AlertDescription>{message}</AlertDescription>
              </Alert>
              <p className="text-center text-sm text-muted-foreground">
                <Link to="/register" className="font-medium text-primary hover:underline">
                  Register Again
                </Link>
                {' or '}
                <Link to="/login" className="font-medium text-primary hover:underline">
                  Login
                </Link>
              </p>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
};

export default EmailConfirmation;
