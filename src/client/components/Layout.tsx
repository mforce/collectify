import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { useAuth, useLogout } from '../services/auth';
import { useToast } from './toaster';
import DarkModeToggle from './DarkModeToggle';

type NavCategory = 'movies' | 'music' | 'games';

const navItems: { to: string; label: string; category: NavCategory | null }[] = [
  { to: '/movies', label: 'Movies', category: 'movies' },
  { to: '/music', label: 'Music', category: 'music' },
  { to: '/games', label: 'Games', category: 'games' },
  { to: '/tags', label: 'Tags', category: null },
];

const catColor = (cat: string | null) =>
  cat === 'movies' ? 'text-movies' : cat === 'music' ? 'text-music' : cat === 'games' ? 'text-games' : 'text-brand';
const catBorder = (cat: string | null) =>
  cat === 'movies' ? 'border-movies/30' : cat === 'music' ? 'border-music/30' : cat === 'games' ? 'border-games/30' : 'border-brand/20';

export default function Layout({ children }: { children: ReactNode }) {
  const { data: auth } = useAuth();
  const logout = useLogout();
  const toast = useToast();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const navRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setMenuOpen(false);
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [menuOpen]);

  // Close mobile menu on navigation
  useEffect(() => {
    setMenuOpen(false);
  }, [location.pathname]);

  const handleLogout = () => {
    logout.mutate(undefined, { onSuccess: () => toast.success('Signed out.') });
  };

  return (
    <div className="min-h-screen flex flex-col bg-surface">
      {/* Header */}
      <nav ref={navRef} className="sticky top-0 z-50 bg-card/80 backdrop-blur-sm border-b border-border">
        <div className="max-w-6xl mx-auto px-4 flex items-center justify-between h-14">
          {/* Brand */}
          <Link to="/" className="text-base font-medium text-text-primary tracking-tight hover:text-brand transition-colors">
            Collectify
          </Link>

          {/* Desktop nav */}
          <div className="hidden md:flex items-center gap-1">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `inline-flex items-center px-3 py-1.5 rounded text-sm transition-colors ${
                    isActive
                      ? `${catColor(item.category)} font-medium border-b-2 ${catBorder(item.category)}`
                      : 'text-text-secondary hover:text-text-primary'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
          </div>

          {/* Desktop user area */}
          <div className="hidden md:flex items-center gap-3">
            <DarkModeToggle />
            <span className="text-sm text-text-secondary">{auth?.userName}</span>
            <button
              onClick={handleLogout}
              className="inline-flex items-center px-3 py-1.5 rounded text-sm text-text-secondary hover:text-error transition-colors"
            >
              Sign out
            </button>
          </div>

          {/* Mobile menu button */}
          <button
            type="button"
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
            aria-expanded={menuOpen}
            onClick={() => setMenuOpen((v) => !v)}
            className="md:hidden inline-flex items-center justify-center w-10 h-10 rounded text-text-primary hover:bg-gray-50 dark:hover:bg-[#353840]"
          >
            <svg aria-hidden viewBox="0 0 24 24" className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
              {menuOpen ? (
                <>
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </>
              ) : (
                <>
                  <line x1="3" y1="7" x2="21" y2="7" />
                  <line x1="3" y1="12" x2="21" y2="12" />
                  <line x1="3" y1="17" x2="21" y2="17" />
                </>
              )}
            </svg>
          </button>
        </div>

        {/* Mobile nav */}
        {menuOpen && (
          <div className="md:hidden border-t border-border px-4 py-3 space-y-1">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  `block px-3 py-2 rounded text-sm transition-colors ${
                    isActive
                      ? `${catColor(item.category)} font-medium`
                      : 'text-text-secondary hover:text-text-primary hover:bg-gray-50 dark:hover:bg-[#353840]'
                  }`
                }
              >
                {item.label}
              </NavLink>
            ))}
            <div className="pt-3 mt-2 border-t border-border flex items-center justify-between">
              <div className="flex items-center gap-2">
                <DarkModeToggle />
                <span className="text-sm text-text-secondary">{auth?.userName}</span>
              </div>
              <button
                onClick={handleLogout}
                className="px-3 py-1.5 rounded text-sm text-text-secondary hover:text-error transition-colors"
              >
                Sign out
              </button>
            </div>
          </div>
        )}
      </nav>

      {/* Main content */}
      <main className="flex-1 max-w-6xl w-full mx-auto px-4 py-6">
        {children}
      </main>
    </div>
  );
}
