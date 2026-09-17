/**
 * NavBar
 * Top-level site navigation. Link visibility reflects the current user's
 * authentication state and roles.
 */

import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import './NavBar.css';

const CURRENT_YEAR = new Date().getFullYear();

const linkClass = ({ isActive }) => `navbar-link${isActive ? ' active' : ''}`;

export default function NavBar() {
  const { user, isAuthenticated, hasAnyRole, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);
  const navRef = useRef(null);

  const authed = isAuthenticated();
  const isAdminOrManager = authed && hasAnyRole(['Admin', 'Manager']);

  // Close the mobile menu on route change
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  // Close the mobile menu on outside click
  useEffect(() => {
    if (!mobileOpen) return;
    const handleClick = (e) => {
      if (navRef.current && !navRef.current.contains(e.target)) {
        setMobileOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [mobileOpen]);

  const handleLogout = async () => {
    await logout();
    navigate('/login');
  };

  return (
    <header className="navbar" ref={navRef}>
      <div className="navbar-inner">
        <Link to="/" className="navbar-brand">
          <span className="navbar-logo" aria-hidden="true">🏔️</span>
          <span>AMR Grand Prix</span>
        </Link>

        <button
          type="button"
          className="navbar-toggle"
          aria-label="Toggle navigation menu"
          aria-expanded={mobileOpen}
          onClick={() => setMobileOpen((v) => !v)}
        >
          <span />
          <span />
          <span />
        </button>

        <nav className={`navbar-menu ${mobileOpen ? 'open' : ''}`}>
          <div className="navbar-links">
            <NavLink to={`/standings/${CURRENT_YEAR}`} className={linkClass}>
              Standings
            </NavLink>

            {authed && (
              <NavLink to="/" end className={linkClass}>
                Home
              </NavLink>
            )}

            {isAdminOrManager && (
              <>
                <span className="navbar-divider" aria-hidden="true" />
                <span className="navbar-section-label">Admin</span>
                <NavLink to="/admin/results" className={linkClass}>
                  Results Management
                </NavLink>
                <NavLink to="/admin/results/upload" className={linkClass}>
                  Upload Results
                </NavLink>
              </>
            )}
          </div>

          <div className="navbar-auth">
            {authed ? (
              <>
                <div className="navbar-user">
                  <span className="navbar-user-name">
                    {user?.firstName || user?.email}
                  </span>
                  {user?.roles?.length > 0 && (
                    <span className="navbar-role-pill">{user.roles.join(', ')}</span>
                  )}
                </div>
                <button type="button" className="btn-secondary btn-sm" onClick={handleLogout}>
                  Logout
                </button>
              </>
            ) : (
              <>
                <NavLink to="/login" className={linkClass}>
                  Login
                </NavLink>
                <Link to="/register" className="btn-primary btn-sm">
                  Register
                </Link>
              </>
            )}
          </div>
        </nav>
      </div>
    </header>
  );
}
