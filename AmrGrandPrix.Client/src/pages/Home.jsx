/**
 * Home Page
 * Landing page for authenticated users
 */

import { Link } from 'react-router-dom';
import { ClipboardList, ListChecks, Trophy, Upload, UserCircle } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';

const CURRENT_YEAR = new Date().getFullYear();

const NAV_CARDS = (isAdminOrManager) => [
  {
    to: `/standings/${CURRENT_YEAR}`,
    icon: Trophy,
    title: 'GP Standings',
    desc: `View ${CURRENT_YEAR} Grand Prix standings`,
  },
  {
    to: '/race-results',
    icon: ListChecks,
    title: 'Race Results',
    desc: 'Browse historical race results',
  },
  {
    to: '/profile',
    icon: UserCircle,
    title: 'My Profile',
    desc: 'Edit your profile and claim your results',
  },
  ...(isAdminOrManager
    ? [
        {
          to: '/admin/results',
          icon: ClipboardList,
          title: 'Results Management',
          desc: 'Manage uploaded race results',
        },
        {
          to: '/admin/results/upload',
          icon: Upload,
          title: 'Upload Results',
          desc: 'Upload race results via file or paste',
        },
      ]
    : []),
];

export const Home = () => {
  const { user } = useAuth();
  const isAdminOrManager = user?.roles?.some((r) => r === 'Admin' || r === 'Manager');

  return (
    <div className="min-h-svh bg-background px-4 py-8 md:py-12">
      <div className="mx-auto max-w-5xl">
        <div className="mb-8">
          <h1 className="bg-gradient-to-r from-primary to-secondary bg-clip-text text-2xl font-semibold tracking-tight text-transparent md:text-3xl">
            Alaska Mountain Runners Grand Prix
          </h1>
          {user && (
            <p className="mt-2 text-muted-foreground">
              Welcome back, {user.firstName || user.email}.
            </p>
          )}
        </div>

        <div className="mb-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {NAV_CARDS(isAdminOrManager).map((navCard) => (
            <Link key={navCard.to} to={navCard.to} className="group block h-full">
              <Card className="h-full border-2 border-transparent transition-all group-hover:-translate-y-0.5 group-hover:border-primary group-hover:shadow-md">
                <CardHeader className="items-center text-center">
                  <navCard.icon className="mb-1 size-8 text-primary" />
                  <CardTitle>{navCard.title}</CardTitle>
                  <CardDescription>{navCard.desc}</CardDescription>
                </CardHeader>
              </Card>
            </Link>
          ))}
        </div>

        {user && (
          <Card>
            <CardHeader>
              <CardTitle>Your Account</CardTitle>
            </CardHeader>
            <CardContent>
              <dl className="divide-y divide-border text-sm">
                <div className="flex items-center justify-between py-2">
                  <dt className="text-muted-foreground">Email</dt>
                  <dd className="font-medium text-foreground">{user.email}</dd>
                </div>
                {user.firstName && (
                  <div className="flex items-center justify-between py-2">
                    <dt className="text-muted-foreground">Name</dt>
                    <dd className="font-medium text-foreground">{user.firstName} {user.lastName}</dd>
                  </div>
                )}
                <div className="flex items-center justify-between py-2">
                  <dt className="text-muted-foreground">Role</dt>
                  <dd className="font-medium text-foreground">{user.roles?.join(', ') || 'ReadOnly'}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>
        )}
      </div>
    </div>
  );
};

export default Home;
