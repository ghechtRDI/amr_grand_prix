/**
 * Home Page
 * Landing page for authenticated users
 */

import { useAuth } from '../hooks/useAuth';
import './pages.css';

export const Home = () => {
  const { user, logout } = useAuth();

  const handleLogout = async () => {
    await logout();
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <h1>Alaska Mountain Runners Grand Prix</h1>
        {user && (
          <div className="user-info">
            <p>
              Welcome, {user.firstName || user.email}!
            </p>
            <p className="user-roles">
              Roles: {user.roles?.join(', ') || 'ReadOnly'}
            </p>
            <button onClick={handleLogout} className="btn-secondary">
              Logout
            </button>
          </div>
        )}
      </div>

      <div className="page-content">
        <div className="welcome-card">
          <h2>Welcome to the Race Results Platform</h2>
          <p>
            This application manages race results, standings, and statistics
            for the Alaska Mountain Runners Grand Prix.
          </p>
          <p className="coming-soon">
            Race results and standings features coming soon!
          </p>
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
