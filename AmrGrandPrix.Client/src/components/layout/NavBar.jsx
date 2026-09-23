/**
 * NavBar
 * Top-level site navigation. Link visibility reflects the current user's
 * authentication state and roles.
 */

import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
import { ChevronDown, CircleUser, Menu, Moon, Mountain, Sun, X } from 'lucide-react';
import { useAuth } from '../../hooks/useAuth';
import { useTheme } from '../../hooks/useTheme';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

const CURRENT_YEAR = new Date().getFullYear();

const navLinkClass = ({ isActive }) =>
  cn(
    'rounded-md px-3 py-2 text-sm font-medium text-secondary-foreground/80 transition-colors hover:bg-white/10 hover:text-secondary-foreground',
    isActive && 'bg-white/15 text-secondary-foreground'
  );

const dropdownTriggerClass = (active) =>
  cn(
    'inline-flex items-center gap-1 rounded-md px-3 py-2 text-sm font-medium text-secondary-foreground/80 outline-none transition-colors hover:bg-white/10 hover:text-secondary-foreground data-[popup-open]:bg-white/15 data-[popup-open]:text-secondary-foreground',
    active && 'bg-white/15 text-secondary-foreground'
  );

const dropdownItemClass = 'text-sm';

export default function NavBar() {
  const { user, isAuthenticated, hasAnyRole, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const location = useLocation();
  const navigate = useNavigate();
  const [mobileOpen, setMobileOpen] = useState(false);
  const navRef = useRef(null);

  const authed = isAuthenticated();
  const isAdminOrManager = authed && hasAnyRole(['Admin', 'Manager']);
  const isAdminSectionActive = location.pathname.startsWith('/admin');

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

  const ThemeToggle = (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
      onClick={toggleTheme}
      className="text-secondary-foreground/80 hover:bg-white/10 hover:text-secondary-foreground"
    >
      {theme === 'dark' ? <Sun className="size-4" /> : <Moon className="size-4" />}
    </Button>
  );

  return (
    <header ref={navRef} className="sticky top-0 z-40 bg-secondary text-secondary-foreground shadow-sm">
      <div className="mx-auto flex h-14 max-w-7xl items-center justify-between gap-4 px-4 md:grid md:grid-cols-[1fr_auto_1fr]">
        <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
          <Mountain className="size-5" aria-hidden="true" />
          <span>AMR Grand Prix</span>
        </Link>

        <button
          type="button"
          className="flex items-center justify-center rounded-md p-2 text-secondary-foreground/80 hover:bg-white/10 hover:text-secondary-foreground md:hidden"
          aria-label="Toggle navigation menu"
          aria-expanded={mobileOpen}
          onClick={() => setMobileOpen((v) => !v)}
        >
          {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
        </button>

        {/* Desktop: centered nav */}
        <nav className="hidden items-center gap-1 md:flex md:justify-self-center">
          <NavLink to={`/standings/${CURRENT_YEAR}`} className={navLinkClass}>
            Grand Prix Standings
          </NavLink>

          <NavLink to="/race-results" className={navLinkClass}>
            Race Results
          </NavLink>

          {isAdminOrManager && (
            <DropdownMenu>
              <DropdownMenuTrigger className={dropdownTriggerClass(isAdminSectionActive)}>
                Admin
                <ChevronDown className="size-3.5" aria-hidden="true" />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="center" className="w-56">
                <DropdownMenuItem className={dropdownItemClass} render={<NavLink to="/admin/results">Results Management</NavLink>} />
                <DropdownMenuItem className={dropdownItemClass} render={<NavLink to="/admin/results/upload">Upload Results</NavLink>} />
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </nav>

        {/* Desktop: theme + user */}
        <div className="hidden items-center gap-2 md:flex md:justify-self-end">
          {ThemeToggle}

          {authed ? (
            <DropdownMenu>
              <DropdownMenuTrigger className={dropdownTriggerClass(location.pathname === '/profile')}>
                <CircleUser className="size-4" aria-hidden="true" />
                <span>{user?.firstName || user?.email}</span>
                {user?.roles?.length > 0 && (
                  <Badge variant="secondary" className="border border-white/20 bg-white/10 text-secondary-foreground">
                    {user.roles.join(', ')}
                  </Badge>
                )}
                <ChevronDown className="size-3.5" aria-hidden="true" />
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-40">
                <DropdownMenuItem className={dropdownItemClass} render={<NavLink to="/profile">Profile</NavLink>} />
                <DropdownMenuItem className={dropdownItemClass} onClick={handleLogout}>
                  Logout
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : (
            <>
              <NavLink to="/login" className={navLinkClass}>
                Login
              </NavLink>
              <Button size="sm" nativeButton={false} render={<Link to="/register">Register</Link>} />
            </>
          )}
        </div>

        {/* Mobile menu */}
        <div
          className={cn(
            'absolute inset-x-0 top-14 flex-col gap-1 border-t border-white/10 bg-secondary p-3 md:hidden',
            mobileOpen ? 'flex' : 'hidden'
          )}
        >
          <NavLink to={`/standings/${CURRENT_YEAR}`} className={navLinkClass}>
            Grand Prix Standings
          </NavLink>

          <NavLink to="/race-results" className={navLinkClass}>
            Race Results
          </NavLink>

          {isAdminOrManager && (
            <>
              <span className="mx-1 h-px bg-white/20" aria-hidden="true" />
              <span className="px-3 pt-2 text-xs font-semibold uppercase tracking-wide text-secondary-foreground/60">
                Admin
              </span>
              <NavLink to="/admin/results" className={navLinkClass}>
                Results Management
              </NavLink>
              <NavLink to="/admin/results/upload" className={navLinkClass}>
                Upload Results
              </NavLink>
            </>
          )}

          <div className="flex flex-col gap-2 border-t border-white/10 pt-3">
            <div className="flex items-center justify-between px-1">
              <span className="text-sm font-medium text-secondary-foreground/80">Theme</span>
              {ThemeToggle}
            </div>

            {authed ? (
              <>
                <div className="flex items-center gap-2 px-1">
                  <CircleUser className="size-4 text-secondary-foreground/80" aria-hidden="true" />
                  <span className="text-sm font-medium">{user?.firstName || user?.email}</span>
                  {user?.roles?.length > 0 && (
                    <Badge variant="secondary" className="border border-white/20 bg-white/10 text-secondary-foreground">
                      {user.roles.join(', ')}
                    </Badge>
                  )}
                </div>
                <NavLink to="/profile" className={navLinkClass}>
                  Profile
                </NavLink>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleLogout}
                  className="border-white/30 bg-transparent text-secondary-foreground hover:bg-white/10 hover:text-secondary-foreground"
                >
                  Logout
                </Button>
              </>
            ) : (
              <>
                <NavLink to="/login" className={navLinkClass}>
                  Login
                </NavLink>
                <Button size="sm" nativeButton={false} render={<Link to="/register">Register</Link>} />
              </>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
