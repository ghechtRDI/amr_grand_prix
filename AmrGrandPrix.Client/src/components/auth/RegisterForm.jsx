/**
 * RegisterForm Component
 * User registration form with validation
 */

import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Captcha } from './Captcha';

const CAPTCHA_REQUIRED = !!import.meta.env.VITE_TURNSTILE_SITE_KEY;

export const RegisterForm = () => {
  const [registerError, setRegisterError] = useState('');
  const [success, setSuccess] = useState(false);
  const [captchaToken, setCaptchaToken] = useState('');

  const { register: registerUser } = useAuth();
  const navigate = useNavigate();

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      email: '',
      password: '',
      confirmPassword: '',
      firstName: '',
      lastName: '',
      preferredName: '',
      hometown: '',
      dateOfBirth: '',
      alternateName1: '',
      alternateName2: '',
      alternateName3: '',
    },
  });

  const onSubmit = async (data) => {
    setRegisterError('');

    const alternateNames = [data.alternateName1, data.alternateName2, data.alternateName3]
      .map((name) => name?.trim())
      .filter(Boolean);

    const payload = {
      email: data.email,
      password: data.password,
      confirmPassword: data.confirmPassword,
      firstName: data.firstName,
      lastName: data.lastName,
      preferredName: data.preferredName || null,
      hometown: data.hometown || null,
      dateOfBirth: data.dateOfBirth || null,
      alternateNames,
      captchaToken: captchaToken || 'no-captcha-configured',
    };

    try {
      const result = await registerUser(payload);

      if (result.success) {
        setSuccess(true);
        // Redirect to login after 3 seconds
        setTimeout(() => {
          navigate('/login');
        }, 3000);
      } else {
        setRegisterError(result.error || 'Registration failed. Please try again.');
      }
    } catch {
      setRegisterError('An error occurred. Please try again.');
    }
  };

  if (success) {
    return (
      <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <CardTitle className="text-2xl">Registration Successful!</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="success">
              <CheckCircle2 />
              <AlertDescription className="text-success">
                <p>Your account has been created successfully.</p>
                <p>Please check your email to confirm your account before logging in.</p>
                <p>Redirecting to login page...</p>
              </AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <CardTitle className="text-2xl">Register</CardTitle>
          <CardDescription>Create a new account</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-5">
          {registerError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{registerError}</AlertDescription>
            </Alert>
          )}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="email">Email *</Label>
              <Input
                id="email"
                type="email"
                autoComplete="email"
                aria-invalid={!!errors.email}
                disabled={isSubmitting}
                {...register('email', {
                  required: 'Email is required',
                  pattern: { value: /\S+@\S+\.\S+/, message: 'Email is invalid' },
                })}
              />
              {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label htmlFor="firstName">First Name</Label>
                <Input
                  id="firstName"
                  type="text"
                  autoComplete="given-name"
                  aria-invalid={!!errors.firstName}
                  disabled={isSubmitting}
                  {...register('firstName', {
                    maxLength: { value: 50, message: 'First name is too long' },
                  })}
                />
                {errors.firstName && <p className="text-sm text-destructive">{errors.firstName.message}</p>}
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="lastName">Last Name</Label>
                <Input
                  id="lastName"
                  type="text"
                  autoComplete="family-name"
                  aria-invalid={!!errors.lastName}
                  disabled={isSubmitting}
                  {...register('lastName', {
                    maxLength: { value: 50, message: 'Last name is too long' },
                  })}
                />
                {errors.lastName && <p className="text-sm text-destructive">{errors.lastName.message}</p>}
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="preferredName">Preferred Name</Label>
              <Input
                id="preferredName"
                type="text"
                autoComplete="nickname"
                disabled={isSubmitting}
                {...register('preferredName', {
                  maxLength: { value: 100, message: 'Preferred name is too long' },
                })}
              />
              {errors.preferredName && (
                <p className="text-sm text-destructive">{errors.preferredName.message}</p>
              )}
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label htmlFor="dateOfBirth">Date of Birth</Label>
                <Input
                  id="dateOfBirth"
                  type="date"
                  disabled={isSubmitting}
                  {...register('dateOfBirth')}
                />
              </div>

              <div className="flex flex-col gap-2">
                <Label htmlFor="hometown">Hometown</Label>
                <Input
                  id="hometown"
                  type="text"
                  autoComplete="address-level2"
                  disabled={isSubmitting}
                  {...register('hometown', {
                    maxLength: { value: 200, message: 'Hometown is too long' },
                  })}
                />
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <Label>Alternate names</Label>
              <p className="text-xs text-muted-foreground">
                Raced under a different name (maiden name, a nickname, a different spelling)? List
                up to 3 so we can match your past results.
              </p>
              {[1, 2, 3].map((n) => (
                <Input
                  key={n}
                  type="text"
                  placeholder={`Alternate name ${n}`}
                  disabled={isSubmitting}
                  {...register(`alternateName${n}`, {
                    maxLength: { value: 200, message: 'Name is too long' },
                  })}
                />
              ))}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="password">Password *</Label>
              <Input
                id="password"
                type="password"
                autoComplete="new-password"
                aria-invalid={!!errors.password}
                disabled={isSubmitting}
                {...register('password', {
                  required: 'Password is required',
                  minLength: { value: 8, message: 'Password must be at least 8 characters' },
                  pattern: {
                    value: /(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])/,
                    message: 'Password must contain uppercase, lowercase, number, and special character',
                  },
                })}
              />
              {errors.password ? (
                <p className="text-sm text-destructive">{errors.password.message}</p>
              ) : (
                <p className="text-xs text-muted-foreground">
                  Must be at least 8 characters with uppercase, lowercase, number, and special character
                </p>
              )}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="confirmPassword">Confirm Password *</Label>
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                aria-invalid={!!errors.confirmPassword}
                disabled={isSubmitting}
                {...register('confirmPassword', {
                  required: 'Please confirm your password',
                  validate: (value) => value === watch('password') || 'Passwords do not match',
                })}
              />
              {errors.confirmPassword && (
                <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>
              )}
            </div>

            <Captcha onVerify={setCaptchaToken} onExpire={() => setCaptchaToken('')} />

            <Button
              type="submit"
              className="mt-2 w-full"
              size="lg"
              disabled={isSubmitting || (CAPTCHA_REQUIRED && !captchaToken)}
            >
              {isSubmitting ? 'Registering...' : 'Register'}
            </Button>
          </form>

          <div className="text-center text-sm text-muted-foreground">
            <p>
              Already have an account?{' '}
              <Link to="/login" className="font-medium text-primary hover:underline">
                Login
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default RegisterForm;
