/**
 * Unauthorized Page
 * Shown when user doesn't have required role
 */

import { Link } from 'react-router-dom';
import './pages.css';

export const Unauthorized = () => {
  return (
    <div className="page-container">
      <div className="error-page">
        <h1>403 - Unauthorized</h1>
        <p>You don't have permission to access this page.</p>
        <div className="error-actions">
          <Link to="/" className="btn-primary">
            Go Home
          </Link>
        </div>
      </div>
    </div>
  );
};

export default Unauthorized;
