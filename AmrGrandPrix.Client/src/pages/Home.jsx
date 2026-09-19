/**
 * Home Page
 * Landing page for authenticated users
 */

import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import './pages.css';

const CURRENT_YEAR = new Date().getFullYear();

export const Home = () => {
  const { user } = useAuth();
  const isAdminOrManager = user?.roles?.some((r) => r === 'Admin' || r === 'Manager');

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>Alaska Mountain Runners Grand Prix</h1>
        {user && <p className="page-subtitle">Welcome back, {user.firstName || user.email}.</p>}
      </div>

      <div className="page-content">
        <div className="nav-cards">
          <Link to={`/standings/${CURRENT_YEAR}`} className="nav-card">
            <div className="nav-card-icon">🏆</div>
            <div className="nav-card-title">GP Standings</div>
            <div className="nav-card-desc">View {CURRENT_YEAR} Grand Prix standings</div>
          </Link>

          {isAdminOrManager && (
            <>
              <Link to="/admin/results" className="nav-card">
                <div className="nav-card-icon">📋</div>
                <div className="nav-card-title">Results Management</div>
                <div className="nav-card-desc">Manage uploaded race results</div>
              </Link>
              <Link to="/admin/results/upload" className="nav-card">
                <div className="nav-card-icon">⬆</div>
                <div className="nav-card-title">Upload Results</div>
                <div className="nav-card-desc">Upload race results via file or paste</div>
              </Link>
            </>
          )}
        </div>

        {user && (
          <div className="info-card">
            <h3>Your Account</h3>
            <ul>
              <li><strong>Email:</strong> {user.email}</li>
              {user.firstName && <li><strong>Name:</strong> {user.firstName} {user.lastName}</li>}
              <li><strong>Role:</strong> {user.roles?.join(', ') || 'ReadOnly'}</li>
            </ul>
          </div>
        )}
      </div>
    </div>
  );
};

export default Home;
