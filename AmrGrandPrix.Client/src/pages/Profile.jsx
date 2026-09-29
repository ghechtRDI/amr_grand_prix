/**
 * Profile Page
 * Edit profile details (preferred/alternate names, hometown, DOB) and, if not already linked to a
 * Runner record, review suggested "is this you?" matches to claim.
 */

import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useForm, Controller } from 'react-hook-form';
import { AlertCircle, CheckCircle2, Clock } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import * as tokenService from '../services/tokenService';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';

const GENDER_OPTIONS = [
  { value: 'Male', label: 'Male' },
  { value: 'Female', label: 'Female' },
  { value: 'Nonbinary', label: 'Nonbinary' },
];

const authHeaders = () => ({
  'Content-Type': 'application/json',
  Authorization: `Bearer ${tokenService.getAccessToken()}`,
});

function ProfileForm() {
  const { user, updateProfile } = useAuth();
  const [status, setStatus] = useState(null); // 'saved' | 'error' | null

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm({
    defaultValues: {
      firstName: user?.firstName || '',
      lastName: user?.lastName || '',
      preferredName: user?.preferredName || '',
      hometown: user?.hometown || '',
      dateOfBirth: user?.dateOfBirth || '',
      gender: user?.gender || '',
      alternateName1: user?.alternateNames?.[0] || '',
      alternateName2: user?.alternateNames?.[1] || '',
      alternateName3: user?.alternateNames?.[2] || '',
    },
  });

  const onSubmit = async (data) => {
    setStatus(null);
    const alternateNames = [data.alternateName1, data.alternateName2, data.alternateName3]
      .map((n) => n?.trim())
      .filter(Boolean);

    const result = await updateProfile({
      firstName: data.firstName,
      lastName: data.lastName,
      preferredName: data.preferredName || null,
      hometown: data.hometown || null,
      dateOfBirth: data.dateOfBirth || null,
      gender: data.gender || null,
      alternateNames,
    });

    setStatus(result.success ? 'saved' : 'error');
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Your Profile</CardTitle>
        <CardDescription>
          Setting a verified date of birth, gender, and any alternate names helps us match you to
          your past race results.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {status === 'saved' && (
          <Alert variant="success">
            <CheckCircle2 />
            <AlertDescription>Profile updated.</AlertDescription>
          </Alert>
        )}
        {status === 'error' && (
          <Alert variant="destructive">
            <AlertCircle />
            <AlertDescription>Could not update your profile. Please try again.</AlertDescription>
          </Alert>
        )}

        <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="firstName">First Name</Label>
              <Input id="firstName" disabled={isSubmitting} {...register('firstName')} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="lastName">Last Name</Label>
              <Input id="lastName" disabled={isSubmitting} {...register('lastName')} />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="preferredName">Preferred Name</Label>
            <Input id="preferredName" disabled={isSubmitting} {...register('preferredName')} />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="dateOfBirth">Date of Birth</Label>
              <Input id="dateOfBirth" type="date" disabled={isSubmitting} {...register('dateOfBirth')} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="hometown">Hometown</Label>
              <Input id="hometown" disabled={isSubmitting} {...register('hometown')} />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label htmlFor="gender">Gender</Label>
            <Controller
              name="gender"
              control={control}
              render={({ field }) => (
                <Select
                  value={field.value || undefined}
                  onValueChange={field.onChange}
                  disabled={isSubmitting}
                >
                  <SelectTrigger id="gender" className="w-full">
                    <SelectValue placeholder="-- Select --" />
                  </SelectTrigger>
                  <SelectContent>
                    {GENDER_OPTIONS.map((g) => (
                      <SelectItem key={g.value} value={g.value}>{g.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>

          <div className="flex flex-col gap-2">
            <Label>Alternate names</Label>
            {[1, 2, 3].map((n) => (
              <Input key={n} placeholder={`Alternate name ${n}`} disabled={isSubmitting} {...register(`alternateName${n}`)} />
            ))}
          </div>

          {errors.root && <p className="text-sm text-destructive">{errors.root.message}</p>}

          <Button type="submit" disabled={isSubmitting} className="w-fit">
            {isSubmitting ? 'Saving…' : 'Save Profile'}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function ConfidenceBadge({ confidence }) {
  const pct = Math.round(confidence * 100);
  const variant = confidence >= 0.9 ? 'success' : 'secondary';
  return <Badge variant={variant}>{pct}% match</Badge>;
}

function ClaimSuggestions() {
  const { refreshUser } = useAuth();
  const [matches, setMatches] = useState(null);
  const [loading, setLoading] = useState(true);
  const [claimingId, setClaimingId] = useState(null);
  const [claimResults, setClaimResults] = useState({}); // runnerId -> 'approved' | 'pending' | 'error'

  useEffect(() => {
    fetch('/api/runner-claims/suggested-matches', { headers: authHeaders() })
      .then((r) => (r.ok ? r.json() : []))
      .then(setMatches)
      .catch(() => setMatches([]))
      .finally(() => setLoading(false));
  }, []);

  const handleClaim = async (runnerId) => {
    setClaimingId(runnerId);
    try {
      const res = await fetch('/api/runner-claims', {
        method: 'POST',
        headers: authHeaders(),
        body: JSON.stringify({ runnerId }),
      });

      setClaimResults((prev) => ({
        ...prev,
        [runnerId]: res.status === 200 ? 'approved' : res.status === 201 ? 'pending' : 'error',
      }));

      if (res.status === 200) {
        await refreshUser();
      }
    } catch {
      setClaimResults((prev) => ({ ...prev, [runnerId]: 'error' }));
    } finally {
      setClaimingId(null);
    }
  };

  if (loading) {
    return <Skeleton className="h-24 w-full" />;
  }

  if (!matches || matches.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Claim Your Results</CardTitle>
          <CardDescription>
            We didn't find any close matches to your profile yet. If you know you have results in
            the system,{' '}
            <Link to="/report" className="font-medium text-primary hover:underline">
              let us know
            </Link>
            .
          </CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Is This You?</CardTitle>
        <CardDescription>
          We found runners in our results that closely match your profile. Matches of 90% or
          higher can be claimed instantly; 80&ndash;89% will be reviewed by an admin.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {matches.map((m) => {
          const outcome = claimResults[m.runnerId];
          return (
            <div
              key={m.runnerId}
              className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border p-3"
            >
              <div>
                <p className="font-medium text-foreground">{m.fullName}</p>
                <div className="mt-1 flex items-center gap-2">
                  <ConfidenceBadge confidence={m.confidence} />
                  {m.ageCategory && <span className="text-xs text-muted-foreground">{m.ageCategory}</span>}
                </div>
              </div>

              {outcome === 'approved' && (
                <span className="flex items-center gap-1 text-sm font-medium text-success">
                  <CheckCircle2 className="size-4" /> Claimed
                </span>
              )}
              {outcome === 'pending' && (
                <span className="flex items-center gap-1 text-sm font-medium text-muted-foreground">
                  <Clock className="size-4" /> Submitted for review
                </span>
              )}
              {outcome === 'error' && <span className="text-sm text-destructive">Something went wrong</span>}
              {!outcome && (
                <Button
                  type="button"
                  size="sm"
                  disabled={claimingId === m.runnerId}
                  onClick={() => handleClaim(m.runnerId)}
                >
                  {claimingId === m.runnerId
                    ? 'Submitting…'
                    : m.confidence >= 0.9
                      ? 'Claim'
                      : 'Request Review'}
                </Button>
              )}
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

export default function Profile() {
  const { user } = useAuth();
  const navigate = useNavigate();

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto flex max-w-2xl flex-col gap-6">
        <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
          My Profile
        </h1>

        <ProfileForm />

        {user?.runnerId ? (
          <Card>
            <CardHeader>
              <CardTitle>Your Results</CardTitle>
              <CardDescription>
                Your account is linked to a runner profile. See every race you&rsquo;ve run, your
                personal records, and your finish-time trend across the years.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <Button onClick={() => navigate(`/runners/${user.runnerId}`)}>My Race Results</Button>
            </CardContent>
          </Card>
        ) : (
          <ClaimSuggestions />
        )}

        <div className="flex gap-6 border-t border-border pt-4 text-sm">
          <Link to="/" className="text-muted-foreground hover:text-foreground">← Home</Link>
        </div>
      </div>
    </div>
  );
}
