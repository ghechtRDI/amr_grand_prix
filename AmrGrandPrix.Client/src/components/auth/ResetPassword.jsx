/**
 * ResetPassword Component
 * Target of the emailed reset link (/reset-password?email=...&token=...): sets a new password.
 */

import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { resetPassword } from '../../services/authService';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';

// The API's error body is either { message, errors: [descriptions] } from Identity or a
// validation problem { title, errors: { Field: [messages] } }.
function errorMessages(err) {
  const errors = err.data?.errors;
  const list = Array.isArray(errors) ? errors : errors ? Object.values(errors).flat() : [];
  // Identity's "Invalid token." covers expired, already-used and mangled links alike.
  if (list.some((e) => /invalid token/i.test(e))) {
    return ['This reset link is invalid or has expired. Request a new one below.'];
  }
  return list.length > 0 ? list : [err.message || 'Something went wrong. Please try again.'];
}

export const ResetPassword = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const email = searchParams.get('email') || '';
  const token = searchParams.get('token') || '';

  const [done, setDone] = useState(false);
  const [errorList, setErrorList] = useState([]);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({ defaultValues: { newPassword: '', confirmPassword: '' } });

  const onSubmit = async (data) => {
    setErrorList([]);
    try {
      await resetPassword({ email, token, ...data });
      setDone(true);
    } catch (err) {
      setErrorList(errorMessages(err));
    }
  };

  const linkIsIncomplete = !email || !token;

  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">Reset Password</CardTitle>
          {!done && !linkIsIncomplete && <CardDescription>Choose a new password for {email}</CardDescription>}
        </CardHeader>
        <CardContent className="flex flex-col gap-5">
          {done ? (
            <>
              <Alert>
                <CheckCircle2 />
                <AlertDescription>Your password has been reset. You can now log in with it.</AlertDescription>
              </Alert>
              <Button type="button" size="lg" className="w-full" onClick={() => navigate('/login')}>
                Go to Login
              </Button>
            </>
          ) : linkIsIncomplete ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>
                This reset link is incomplete. Open the link from your email again, or request a new one.
              </AlertDescription>
            </Alert>
          ) : (
            <>
              {errorList.length > 0 && (
                <Alert variant="destructive">
                  <AlertCircle />
                  <AlertDescription>
                    {errorList.length === 1 ? (
                      errorList[0]
                    ) : (
                      <ul className="list-disc pl-4">
                        {errorList.map((e) => <li key={e}>{e}</li>)}
                      </ul>
                    )}
                  </AlertDescription>
                </Alert>
              )}

              <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
                {/* Lets password managers save the new password against the right account. */}
                <input type="email" name="username" autoComplete="username" value={email} readOnly hidden />

                <div className="flex flex-col gap-2">
                  <Label htmlFor="newPassword">New Password</Label>
                  <Input
                    id="newPassword"
                    type="password"
                    autoComplete="new-password"
                    autoFocus
                    aria-invalid={!!errors.newPassword}
                    disabled={isSubmitting}
                    {...register('newPassword', {
                      required: 'Password is required',
                      minLength: { value: 8, message: 'Password must be at least 8 characters' },
                      // Mirrors the API's Identity password options (any non-alphanumeric counts).
                      pattern: {
                        value: /(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9])/,
                        message: 'Password must contain uppercase, lowercase, number, and special character',
                      },
                    })}
                  />
                  {errors.newPassword ? (
                    <p className="text-sm text-destructive">{errors.newPassword.message}</p>
                  ) : (
                    <p className="text-xs text-muted-foreground">
                      Must be at least 8 characters with uppercase, lowercase, number, and special character
                    </p>
                  )}
                </div>

                <div className="flex flex-col gap-2">
                  <Label htmlFor="confirmPassword">Confirm New Password</Label>
                  <Input
                    id="confirmPassword"
                    type="password"
                    autoComplete="new-password"
                    aria-invalid={!!errors.confirmPassword}
                    disabled={isSubmitting}
                    {...register('confirmPassword', {
                      required: 'Please confirm your password',
                      validate: (value) => value === watch('newPassword') || 'Passwords do not match',
                    })}
                  />
                  {errors.confirmPassword && (
                    <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>
                  )}
                </div>

                <Button type="submit" className="mt-2 w-full" size="lg" disabled={isSubmitting}>
                  {isSubmitting ? 'Resetting...' : 'Reset Password'}
                </Button>
              </form>
            </>
          )}

          {!done && (
            <div className="flex flex-col items-center gap-2 text-sm text-muted-foreground">
              <Link to="/forgot-password" className="font-medium text-primary hover:underline">
                Request a new reset link
              </Link>
              <Link to="/login" className="font-medium text-primary hover:underline">
                Back to login
              </Link>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
};

export default ResetPassword;
