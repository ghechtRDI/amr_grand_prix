/**
 * Main App Component
 * Sets up routing and authentication
 */

import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { ProtectedRoute } from './components/auth/ProtectedRoute';
import NavBar from './components/layout/NavBar';
import LoginForm from './components/auth/LoginForm';
import RegisterForm from './components/auth/RegisterForm';
import EmailConfirmation from './components/auth/EmailConfirmation';
import Home from './pages/Home';
import Standings from './pages/Standings';
import RaceResults from './pages/RaceResults';
import Unauthorized from './pages/Unauthorized';
import ResultsUpload from './pages/admin/ResultsUpload';
import ResultsManagement from './pages/admin/ResultsManagement';
import './App.css';

function App() {
  return (
    <Router>
      <AuthProvider>
        <NavBar />
        <Routes>
          {/* Public routes */}
          <Route path="/login" element={<LoginForm />} />
          <Route path="/register" element={<RegisterForm />} />
          <Route path="/confirm-email" element={<EmailConfirmation />} />
          <Route path="/unauthorized" element={<Unauthorized />} />

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
      </AuthProvider>
    </Router>
  );
}

export default App;
