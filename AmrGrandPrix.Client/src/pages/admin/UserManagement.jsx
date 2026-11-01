/**
 * User Management (Admin only)
 * Site accounts: search, change role, lock/unlock, and delete. Admins can't change, lock, or
 * delete their own account (the API enforces this too), so there's always a way back in.
 */

import { useCallback, useEffect, useState } from 'react';
import { AlertCircle, CheckCircle2, ChevronLeft, ChevronRight, Lock, LockOpen, RefreshCw, Trash2, X } from 'lucide-react';
import * as userService from '../../services/userService';
import { useAuth } from '../../hooks/useAuth';
import { Alert, AlertAction, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { SearchInput } from '@/components/ui/search-input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';

const PAGE_SIZE = 25;
const COLUMN_COUNT = 5;

const displayName = (u) => [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email;

const formatJoined = (iso) =>
  new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });

export default function UserManagement() {
  const { user: currentUser } = useAuth();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState({ users: [], totalUsers: 0, totalPages: 1 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [busyId, setBusyId] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await userService.getUsers({ page, pageSize: PAGE_SIZE, search }));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [page, search]);

  useEffect(() => {
    load();
  }, [load]);

  const handleSearch = (value) => {
    setSearch(value);
    setPage(1);
  };

  const runAction = async (target, action, successText) => {
    setBusyId(target.id);
    setMessage(null);
    try {
      await action();
      setMessage({ type: 'success', text: successText });
      await load();
    } catch (err) {
      setMessage({ type: 'error', text: err.message });
    } finally {
      setBusyId(null);
    }
  };

  const handleRoleChange = (target, roleName) => {
    if (target.roles.includes(roleName)) return;
    runAction(target, () => userService.assignRole(target.id, roleName), `${displayName(target)} is now ${roleName}.`);
  };

  const handleToggleLock = (target) =>
    runAction(
      target,
      () => userService.setLocked(target.id, !target.isLocked),
      `${displayName(target)} ${target.isLocked ? 'unlocked' : 'locked'}.`
    );

  const handleDelete = (target) => {
    setDeleteTarget(null);
    runAction(target, () => userService.deleteUser(target.id), `Deleted ${displayName(target)}.`);
  };

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-6xl">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            User Management
          </h1>
          <Button variant="outline" onClick={load}>
            <RefreshCw /> Refresh
          </Button>
        </div>

        {message && (
          <Alert variant={message.type === 'success' ? 'success' : 'destructive'} className="mb-4">
            {message.type === 'success' ? <CheckCircle2 /> : <AlertCircle />}
            <AlertDescription className={message.type === 'success' ? 'text-success' : undefined}>
              {message.text}
            </AlertDescription>
            <AlertAction>
              <Button variant="ghost" size="icon-sm" onClick={() => setMessage(null)} aria-label="Dismiss">
                <X />
              </Button>
            </AlertAction>
          </Alert>
        )}

        <div className="flex flex-col gap-4">
          <SearchInput
            value={search}
            onChange={handleSearch}
            placeholder="Search email or name…"
            className="max-w-sm"
          />

          {error && (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <Card className="py-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>User</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Joined</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading &&
                  [1, 2, 3, 4, 5].map((n) => (
                    <TableRow key={n}>
                      <TableCell colSpan={COLUMN_COUNT}><Skeleton className="h-5 w-full" /></TableCell>
                    </TableRow>
                  ))}
                {!loading && data.users.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={COLUMN_COUNT} className="py-8 text-center text-muted-foreground">
                      No users found.
                    </TableCell>
                  </TableRow>
                )}
                {!loading &&
                  data.users.map((u) => {
                    const isSelf = u.id === currentUser?.id;
                    const busy = busyId === u.id;
                    const role = u.roles[0] ?? '';
                    return (
                      <TableRow key={u.id}>
                        <TableCell>
                          <span className="font-medium">{displayName(u)}</span>
                          {isSelf && <Badge variant="secondary" className="ml-2">You</Badge>}
                          <span className="block text-xs text-muted-foreground">{u.email}</span>
                        </TableCell>
                        <TableCell>
                          <Select
                            value={role || undefined}
                            onValueChange={(value) => handleRoleChange(u, value)}
                            disabled={isSelf || busy}
                          >
                            <SelectTrigger className="w-32" aria-label={`Role for ${u.email}`}>
                              <SelectValue placeholder="No role" />
                            </SelectTrigger>
                            <SelectContent>
                              {userService.ROLES.map((r) => (
                                <SelectItem key={r} value={r}>{r}</SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-wrap gap-1">
                            {u.isLocked && <Badge variant="destructive">Locked</Badge>}
                            {u.emailConfirmed ? (
                              <Badge variant="success">Confirmed</Badge>
                            ) : (
                              <Badge variant="outline">Unconfirmed</Badge>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>{formatJoined(u.createdAt)}</TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="sm"
                              disabled={isSelf || busy}
                              onClick={() => handleToggleLock(u)}
                            >
                              {u.isLocked ? <><LockOpen /> Unlock</> : <><Lock /> Lock</>}
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-destructive"
                              disabled={isSelf || busy}
                              onClick={() => setDeleteTarget(u)}
                            >
                              <Trash2 /> Delete
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
              </TableBody>
            </Table>
          </Card>

          {!loading && data.totalUsers > 0 && (
            <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
              <span>
                {data.totalUsers} user{data.totalUsers === 1 ? '' : 's'}
              </span>
              {data.totalPages > 1 && (
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                    <ChevronLeft /> Previous
                  </Button>
                  <span>Page {page} of {data.totalPages}</span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page >= data.totalPages}
                    onClick={() => setPage((p) => p + 1)}
                  >
                    Next <ChevronRight />
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this account?</AlertDialogTitle>
            <AlertDialogDescription>
              Delete the account for {deleteTarget && displayName(deleteTarget)} ({deleteTarget?.email})? Their race
              results stay, but the account and its sign-in are removed. This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction variant="destructive" onClick={() => handleDelete(deleteTarget)}>
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
