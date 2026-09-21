/**
 * Main App Component
 * Sets up routing and authentication
 */

import { lazy, Suspense } from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { AuthProvider } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import { ProtectedRoute } from './components/auth/ProtectedRoute';
import NavBar from './components/layout/NavBar';

// Route-level code splitting: each page (and its dependencies, e.g. the
// upload wizard's LLM/dropzone code or TanStack Table) ships in its own
// chunk, loaded on navigation rather than in the initial bundle.
const LoginForm = lazy(() => import('./components/auth/LoginForm'));
const RegisterForm = lazy(() => import('./components/auth/RegisterForm'));
const EmailConfirmation = lazy(() => import('./components/auth/EmailConfirmation'));
const Home = lazy(() => import('./pages/Home'));
const Profile = lazy(() => import('./pages/Profile'));
const Standings = lazy(() => import('./pages/Standings'));
const RaceResults = lazy(() => import('./pages/RaceResults'));
const ResultsBrowser = lazy(() => import('./pages/ResultsBrowser'));
const RaceSeriesDetail = lazy(() => import('./pages/RaceSeriesDetail'));
const ReportIssue = lazy(() => import('./pages/ReportIssue'));
const Unauthorized = lazy(() => import('./pages/Unauthorized'));
const ResultsUpload = lazy(() => import('./pages/admin/ResultsUpload'));
const ResultsManagement = lazy(() => import('./pages/admin/ResultsManagement'));

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
              <Route path="/login" element={<LoginForm />} />
              <Route path="/register" element={<RegisterForm />} />
              <Route path="/confirm-email" element={<EmailConfirmation />} />
              <Route path="/unauthorized" element={<Unauthorized />} />
              <Route path="/report" element={<ReportIssue />} />

              {/* Profile - requires login */}
              <Route
                path="/profile"
                element={
                  <ProtectedRoute>
                    <Profile />
                  </ProtectedRoute>
                }
              />

              {/* Protected routes */}
              <Route
                path="/"
                element={
                  <ProtectedRoute>
                    <Home />
                  </ProtectedRoute>
                }
              />

              {/* Public routes - standings and race results */}
              <Route path="/standings" element={<Standings />} />
              <Route path="/standings/:year" element={<Standings />} />
              <Route path="/race-results" element={<ResultsBrowser />} />
              <Route path="/race-series/:seriesId" element={<RaceSeriesDetail />} />
              <Route path="/races/:raceId/results" element={<RaceResults />} />

              {/* Admin routes - requires Manager or Admin role */}
              <Route
                path="/admin/results"
                element={
                  <ProtectedRoute roles={['Admin', 'Manager']}>
                    <ResultsManagement />
                  </ProtectedRoute>
                }
              />
              <Route
                path="/admin/results/upload"
                element={
                  <ProtectedRoute roles={['Admin', 'Manager']}>
                    <ResultsUpload />
                  </ProtectedRoute>
                }
              />

              {/* Redirect unknown routes to home */}
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Suspense>
        </AuthProvider>
      </ThemeProvider>
    </Router>
  );
}

export default App;
