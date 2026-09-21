/**
 * Report Issue Page
 * Public form for reporting an inaccurate result or asking for help claiming a result that
 * didn't reach the 80% self-serve match threshold. Emails the club and is stored for admin review.
 */

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { AlertCircle, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Captcha } from '@/components/auth/Captcha';

const CAPTCHA_REQUIRED = !!import.meta.env.VITE_TURNSTILE_SITE_KEY;

export default function ReportIssue() {
  const [submitError, setSubmitError] = useState('');
  const [success, setSuccess] = useState(false);
  const [captchaToken, setCaptchaToken] = useState('');

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      runnerName: '',
      dateOfBirth: '',
      raceName: '',
      raceDate: '',
      description: '',
      reporterEmail: '',
    },
  });

  const onSubmit = async (data) => {
    setSubmitError('');

    try {
      const res = await fetch('/api/reports', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          runnerName: data.runnerName,
          dateOfBirth: data.dateOfBirth || null,
          raceName: data.raceName || null,
          raceDate: data.raceDate || null,
          description: data.description,
          reporterEmail: data.reporterEmail,
          captchaToken: captchaToken || 'no-captcha-configured',
        }),
      });

      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.message || 'Failed to submit report');
      }

      setSuccess(true);
    } catch (err) {
      setSubmitError(err.message);
    }
  };

  if (success) {
    return (
      <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
        <Card className="w-full max-w-md">
          <CardHeader className="text-center">
            <CardTitle className="text-2xl">Thanks!</CardTitle>
          </CardHeader>
          <CardContent>
            <Alert variant="success">
              <CheckCircle2 />
              <AlertDescription>
                Your report has been sent to the race committee. They'll follow up by email if
                needed.
              </AlertDescription>
            </Alert>
            <Link to="/" className="mt-4 block text-center text-sm font-medium text-primary hover:underline">
              ← Home
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex min-h-svh items-center justify-center bg-background px-4 py-12">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle className="text-2xl">Report an Issue</CardTitle>
          <CardDescription>
            Use this form to report an inaccurate result, or to ask for help claiming a result
            that isn't a close enough match to claim automatically.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-5">
          {submitError && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{submitError}</AlertDescription>
            </Alert>
          )}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
            <div className="flex flex-col gap-2">
              <Label htmlFor="runnerName">Racer's Name *</Label>
              <Input
                id="runnerName"
                disabled={isSubmitting}
                aria-invalid={!!errors.runnerName}
                {...register('runnerName', { required: 'Racer name is required' })}
              />
              {errors.runnerName && <p className="text-sm text-destructive">{errors.runnerName.message}</p>}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="dateOfBirth">Racer's Date of Birth</Label>
              <Input id="dateOfBirth" type="date" disabled={isSubmitting} {...register('dateOfBirth')} />
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="flex flex-col gap-2">
                <Label htmlFor="raceName">Race in Question</Label>
                <Input id="raceName" disabled={isSubmitting} {...register('raceName')} />
              </div>
              <div className="flex flex-col gap-2">
                <Label htmlFor="raceDate">Date of Race</Label>
                <Input id="raceDate" type="date" disabled={isSubmitting} {...register('raceDate')} />
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="description">What's wrong? *</Label>
              <Textarea
                id="description"
                rows={4}
                disabled={isSubmitting}
                aria-invalid={!!errors.description}
                {...register('description', { required: 'Please describe the issue' })}
              />
              {errors.description && <p className="text-sm text-destructive">{errors.description.message}</p>}
            </div>

            <div className="flex flex-col gap-2">
              <Label htmlFor="reporterEmail">Your Email *</Label>
              <Input
                id="reporterEmail"
                type="email"
                disabled={isSubmitting}
                aria-invalid={!!errors.reporterEmail}
                {...register('reporterEmail', {
                  required: 'Email is required',
                  pattern: { value: /\S+@\S+\.\S+/, message: 'Email is invalid' },
                })}
              />
              {errors.reporterEmail && <p className="text-sm text-destructive">{errors.reporterEmail.message}</p>}
            </div>

            <Captcha onVerify={setCaptchaToken} onExpire={() => setCaptchaToken('')} />

            <Button type="submit" disabled={isSubmitting || (CAPTCHA_REQUIRED && !captchaToken)}>
              {isSubmitting ? 'Submitting…' : 'Submit Report'}
            </Button>
          </form>

          <Link to="/" className="text-center text-sm text-muted-foreground hover:text-foreground">
            ← Home
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
