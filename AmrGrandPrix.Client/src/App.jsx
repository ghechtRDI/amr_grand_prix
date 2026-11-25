/**
 * Main App Component
 * Sets up routing and authentication
 */

import { lazy, Suspense } from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { AuthProvider } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import { ProtectedRoute } from './components/auth/ProtectedRoute';
import NavBar from './components/layout/NavBar';
import Footer from './components/layout/Footer';
import PageMeta from './components/layout/PageMeta';
import { DEFAULT_TITLE } from './hooks/usePageMeta';

// Route-level code splitting: each page (and its dependencies, e.g. the
// upload wizard's LLM/dropzone code or TanStack Table) ships in its own
// chunk, loaded on navigation rather than in the initial bundle.
const LoginForm = lazy(() => import('./components/auth/LoginForm'));
const RegisterForm = lazy(() => import('./components/auth/RegisterForm'));
const EmailConfirmation = lazy(() => import('./components/auth/EmailConfirmation'));
const ForgotPassword = lazy(() => import('./components/auth/ForgotPassword'));
const ResetPassword = lazy(() => import('./components/auth/ResetPassword'));
const Landing = lazy(() => import('./pages/Landing'));
const NotFound = lazy(() => import('./pages/NotFound'));
const Profile = lazy(() => import('./pages/Profile'));
const Standings = lazy(() => import('./pages/Standings'));
const RaceResults = lazy(() => import('./pages/RaceResults'));
const ResultsBrowser = lazy(() => import('./pages/ResultsBrowser'));
const Athletes = lazy(() => import('./pages/Athletes'));
const RaceSeriesDetail = lazy(() => import('./pages/RaceSeriesDetail'));
const RunnerProfile = lazy(() => import('./pages/RunnerProfile'));
const RunnerSeries = lazy(() => import('./pages/RunnerSeries'));
const ReportIssue = lazy(() => import('./pages/ReportIssue'));
const Unauthorized = lazy(() => import('./pages/Unauthorized'));
const ResultsUpload = lazy(() => import('./pages/admin/ResultsUpload'));
const ResultsManagement = lazy(() => import('./pages/admin/ResultsManagement'));
const RunnerManagement = lazy(() => import('./pages/admin/RunnerManagement'));
const UserManagement = lazy(() => import('./pages/admin/UserManagement'));

// Same loading treatment ProtectedRoute already uses for its auth check, so
// a route-chunk fetch and an auth-check look identical to the user.
const RouteFallback = () => (
  <div className="flex min-h-svh flex-col items-center justify-center gap-3 bg-background text-muted-foreground">
    <Loader2 className="size-6 animate-spin" />
    <p>Loading...</p>
  </div>
);

function App() {
  return (
    <Router>
      <ThemeProvider>
        <AuthProvider>
          <NavBar />
          <Suspense fallback={<RouteFallback />}>
            <Routes>
              {/* Public routes - auth flows (migrated to shadcn/Tailwind) */}
              <Route path="/login" element={<PageMeta title="Login" noindex><LoginForm /></PageMeta>} />
              <Route path="/register" element={<PageMeta title="Register" noindex><RegisterForm /></PageMeta>} />
              <Route path="/confirm-email" element={<PageMeta title="Confirm Email" noindex><EmailConfirmation /></PageMeta>} />
              <Route path="/forgot-password" element={<PageMeta title="Forgot Password" noindex><ForgotPassword /></PageMeta>} />
              <Route path="/reset-password" element={<PageMeta title="Reset Password" noindex><ResetPassword /></PageMeta>} />
              <Route path="/unauthorized" element={<PageMeta title="Unauthorized" noindex><Unauthorized /></PageMeta>} />
              <Route
                path="/report"
                element={
                  <PageMeta title="Report an Issue" description="Report a mistake in Alaska Mountain Runners race results or Grand Prix standings.">
                    <ReportIssue />
                  </PageMeta>
                }
              />

              {/* Profile - requires login */}
              <Route
                path="/profile"
                element={
                  <ProtectedRoute>
                    <PageMeta title="Your Profile" noindex>
                      <Profile />
                    </PageMeta>
                  </ProtectedRoute>
                }
              />

              {/* Public routes - landing, standings, race results, and athletes */}
              <Route path="/" element={<PageMeta rawTitle={DEFAULT_TITLE}><Landing /></PageMeta>} />
              <Route path="/standings" element={<Standings />} />
              <Route path="/standings/:year" element={<Standings />} />
              <Route
                path="/race-results"
                element={
                  <PageMeta
                    title="Race Results"
                    description="Browse results from Alaska mountain and trail races by year or by race series, back through every season on record."
                  >
                    <ResultsBrowser />
                  </PageMeta>
                }
              />
              <Route
                path="/athletes"
                element={
                  <PageMeta
                    title="Athletes"
                    description="Search every runner in the Alaska Mountain Runners results archive and see their race history and best Grand Prix season."
                  >
                    <Athletes />
                  </PageMeta>
                }
              />
              <Route path="/race-series/:seriesId" element={<RaceSeriesDetail />} />
              <Route path="/races/:raceId/results" element={<RaceResults />} />
              <Route path="/runners/:runnerId" element={<RunnerProfile />} />
              <Route path="/runners/:runnerId/series/:seriesId" element={<RunnerSeries />} />

              {/* Admin routes - requires Manager or Admin role */}
              <Route
                path="/admin/results"
                element={
                  <ProtectedRoute roles={['Admin', 'Manager']}>
                    <PageMeta title="Results Management" noindex>
                      <ResultsManagement />
                    </PageMeta>
                  </ProtectedRoute>
                }
              />
              <Route
                path="/admin/results/upload"
                element={
                  <ProtectedRoute roles={['Admin', 'Manager']}>
                    <PageMeta title="Upload Results" noindex>
                      <ResultsUpload />
                    </PageMeta>
                  </ProtectedRoute>
                }
              />
              <Route
                path="/admin/runners"
                element={
                  <ProtectedRoute roles={['Admin', 'Manager']}>
                    <PageMeta title="Racer Management" noindex>
                      <RunnerManagement />
                    </PageMeta>
                  </ProtectedRoute>
                }
              />
              {/* Admin only - the /api/users endpoints require the Admin role */}
              <Route
                path="/admin/users"
                element={
                  <ProtectedRoute roles={['Admin']}>
                    <PageMeta title="User Management" noindex>
                      <UserManagement />
                    </PageMeta>
                  </ProtectedRoute>
                }
              />

              <Route path="*" element={<PageMeta title="Page Not Found" noindex><NotFound /></PageMeta>} />
            </Routes>
          </Suspense>
          <Footer />
        </AuthProvider>
      </ThemeProvider>
    </Router>
  );
}

export default App;
