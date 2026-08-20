import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { useAuth, useLogout } from '../services/auth';
import { useToast } from './toaster';
import DarkModeToggle from './DarkModeToggle';
import { MEDIA } from '../services/mediaRegistry';
import type { MediaType } from '../services/types';

type NavCategory = MediaType;

const navItems: { to: string; label: string; category: NavCategory | null; icon: ReactNode }[] = [
  { to: '/', label: 'Home', category: null, icon: <BrandIcon src="/brand/media-home.svg" alt="" /> },
  ...(['movies', 'music', 'games'] as MediaType[]).map((category) => ({
    to: MEDIA[category].paths.list, label: MEDIA[category].pluralLabel, category,
    icon: <BrandIcon src={MEDIA[category].iconSrc} alt="" />,
  })),
  { to: '/tags', label: 'Tags', category: null, icon: <TagIcon /> },
];

const navActiveDesktop = (category: NavCategory | null) => category
  ? MEDIA[category].theme.navActiveDesktop : 'bg-brand/10 text-brand border-brand/20 shadow-sm';
const navActiveMobile = (category: NavCategory | null) => category
  ? MEDIA[category].theme.navActiveMobile : 'bg-brand/10 text-brand border-brand/20';

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
      <nav ref={navRef} className="sticky top-0 z-50 border-b border-border bg-card/88 backdrop-blur-xl shadow-sm">
        <div className="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">
          {/* Brand */}
          <Link to="/" className="inline-flex items-center gap-3 text-text-primary transition-colors hover:text-brand">
            <img
              src="/brand/collectify-logo.png"
              alt=""
              className="h-10 w-10 rounded-xl shadow-sm"
            />
            <span className="hidden text-base font-extrabold tracking-tight sm:inline">Collectify</span>
          </Link>

          {/* Desktop nav */}
          <div className="hidden items-center gap-2 md:flex">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `inline-flex h-10 items-center gap-2 rounded-full border px-3 text-sm font-semibold transition-colors ${
                    isActive
                      ? navActiveDesktop(item.category)
                      : 'border-transparent text-text-secondary hover:bg-pill-bg hover:text-text-primary'
                  }`
                }
              >
                <span aria-hidden className="h-4 w-4">{item.icon}</span>
                {item.label}
              </NavLink>
            ))}
          </div>

          {/* Desktop user area */}
          <div className="hidden md:flex items-center gap-3">
            <DarkModeToggle />
            <span className="max-w-32 truncate rounded-full border border-border bg-pill-bg px-3 py-1.5 text-sm font-medium text-text-secondary">
              {auth?.userName}
            </span>
            <button
              onClick={handleLogout}
              className="inline-flex h-10 items-center rounded-full px-3 text-sm font-semibold text-text-secondary transition-colors hover:bg-error/10 hover:text-error"
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
            className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-border bg-card text-text-primary transition-colors hover:bg-pill-bg md:hidden"
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
          <div className="space-y-2 border-t border-border px-4 py-3 md:hidden">
            {navItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/'}
                className={({ isActive }) =>
                  `flex min-h-11 items-center gap-2 rounded-xl border px-3 text-sm font-semibold transition-colors ${
                    isActive
                      ? navActiveMobile(item.category)
                      : 'border-transparent text-text-secondary hover:bg-pill-bg hover:text-text-primary'
                  }`
                }
              >
                <span aria-hidden className="h-4 w-4">{item.icon}</span>
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
                className="rounded-full px-3 py-1.5 text-sm font-semibold text-text-secondary transition-colors hover:bg-error/10 hover:text-error"
              >
                Sign out
              </button>
            </div>
          </div>
        )}
      </nav>

      {/* Main content */}
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6 lg:px-8">
        {children}
      </main>
    </div>
  );
}

function BrandIcon({ src, alt }: { src: string; alt: string }) {
  return <img src={src} alt={alt} className="h-full w-full object-contain" aria-hidden={alt === '' || undefined} />;
}

function TagIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M20.6 13.4 13.4 20.6a2 2 0 0 1-2.8 0L3 13V3h10l7.6 7.6a2 2 0 0 1 0 2.8Z" />
      <circle cx="7.5" cy="7.5" r=".5" fill="currentColor" />
    </svg>
  );
}
