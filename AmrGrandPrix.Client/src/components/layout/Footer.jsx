/**
 * Footer
 * Site-wide footer: navigation links, copyright, and site credit.
 */

import { Link } from 'react-router-dom';
import { Mountain } from 'lucide-react';

const LINKS = [
  { to: '/race-results', label: 'Race Results' },
  { to: '/athletes', label: 'Athletes' },
  { to: '/standings', label: 'Grand Prix Standings' },
  { to: '/report', label: 'Report an Issue' },
];

const linkClass = 'text-muted-foreground transition-colors hover:text-foreground';

export default function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-border bg-card text-sm">
      <div className="mx-auto flex max-w-7xl flex-col gap-6 px-4 py-8 md:flex-row md:items-start md:justify-between">
        <div className="flex flex-col gap-2">
          <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
            <Mountain className="size-4 text-primary" aria-hidden="true" />
            Alaska Mountain Runners Race Results
          </Link>
          <p className="text-muted-foreground">© {year} Alaska Mountain Runners</p>
          <p className="text-muted-foreground">Designed and built by Galen Hecht</p>
        </div>

        <nav aria-label="Footer">
          <ul className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:gap-x-6">
            {LINKS.map(({ to, label }) => (
              <li key={to}>
                <Link to={to} className={linkClass}>{label}</Link>
              </li>
            ))}
            <li>
              <a href="https://alaskamountainrunners.org" className={linkClass} target="_blank" rel="noopener noreferrer">
                Alaska Mountain Runners ↗
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </footer>
  );
}
