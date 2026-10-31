/**
 * Admin edit form for a runner record: legal name, gender, verified date of birth, email,
 * preferred name, and alternate names (the names results uploads are matched against).
 */

import { useEffect, useState } from 'react';
import { useForm, useFieldArray, Controller } from 'react-hook-form';
import { AlertCircle, Plus, X } from 'lucide-react';
import * as runnerService from '../../services/runnerService';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { GENDER_OPTIONS } from './format';

const MAX_ALTERNATE_NAMES = 20;

const toFormValues = (runner) => ({
  firstName: runner?.firstName ?? '',
  lastName: runner?.lastName ?? '',
  gender: runner?.gender ?? 'Male',
  dateOfBirth: runner?.dateOfBirth ?? '',
  email: runner?.email ?? '',
  preferredName: runner?.preferredName ?? '',
  alternateNames: (runner?.alternateNames ?? []).map((value) => ({ value })),
});

export default function EditRunnerDialog({ runner, open, onOpenChange, onSaved }) {
  const [error, setError] = useState(null);
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm({ defaultValues: toFormValues(runner) });
  const { fields, append, remove } = useFieldArray({ control, name: 'alternateNames' });

  useEffect(() => {
    if (open) {
      reset(toFormValues(runner));
      setError(null);
    }
  }, [open, runner, reset]);

  const onSubmit = async (data) => {
    setError(null);
    try {
      await runnerService.updateRunner(runner.runnerId, {
        firstName: data.firstName.trim(),
        lastName: data.lastName.trim(),
        gender: data.gender,
        dateOfBirth: data.dateOfBirth || null,
        email: data.email.trim() || null,
        preferredName: data.preferredName.trim(),
        alternateNames: data.alternateNames.map((n) => n.value.trim()).filter(Boolean),
      });
      onSaved?.();
      onOpenChange(false);
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(o) => !isSubmitting && onOpenChange(o)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit runner</DialogTitle>
          <DialogDescription>
            Alternate names are used to match this runner in future results uploads — add maiden
            names, nicknames, or other spellings they&rsquo;ve raced under.
          </DialogDescription>
        </DialogHeader>

        <form id="edit-runner-form" onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-4">
          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-first">First Name</Label>
              <Input
                id="runner-first"
                aria-invalid={!!errors.firstName}
                {...register('firstName', { validate: (v) => !!v.trim() || 'Required' })}
              />
              {errors.firstName && <p className="text-xs text-destructive">{errors.firstName.message}</p>}
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-last">Last Name</Label>
              <Input
                id="runner-last"
                aria-invalid={!!errors.lastName}
                {...register('lastName', { validate: (v) => !!v.trim() || 'Required' })}
              />
              {errors.lastName && <p className="text-xs text-destructive">{errors.lastName.message}</p>}
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-gender">Gender</Label>
              <Controller
                name="gender"
                control={control}
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="runner-gender" className="w-full">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {GENDER_OPTIONS.map((g) => (
                        <SelectItem key={g} value={g}>{g}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-dob">Date of Birth</Label>
              <Input id="runner-dob" type="date" {...register('dateOfBirth')} />
              {!runner?.dateOfBirth && runner?.estimatedBirthYear && (
                <p className="text-xs text-muted-foreground">
                  Currently estimated as ~{runner.estimatedBirthYear} from race ages.
                </p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-preferred">Preferred Name</Label>
              <Input id="runner-preferred" {...register('preferredName')} />
            </div>
            <div className="flex flex-col gap-2">
              <Label htmlFor="runner-email">Email</Label>
              <Input id="runner-email" type="email" {...register('email')} />
            </div>
          </div>

          <div className="flex flex-col gap-2">
            <Label>Alternate Names</Label>
            {fields.length === 0 && <p className="text-sm text-muted-foreground">No alternate names.</p>}
            {fields.map((field, index) => (
              <div key={field.id} className="flex gap-2">
                <Input
                  placeholder="Full name, e.g. Joshua Taylor"
                  aria-label={`Alternate name ${index + 1}`}
                  {...register(`alternateNames.${index}.value`)}
                />
                <Button type="button" variant="ghost" size="icon" onClick={() => remove(index)} aria-label="Remove alternate name">
                  <X />
                </Button>
              </div>
            ))}
            {fields.length < MAX_ALTERNATE_NAMES && (
              <Button type="button" variant="outline" size="sm" className="w-fit" onClick={() => append({ value: '' })}>
                <Plus /> Add alternate name
              </Button>
            )}
          </div>
        </form>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" form="edit-runner-form" disabled={isSubmitting}>
            {isSubmitting ? 'Saving…' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
