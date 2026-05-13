import { useState, type ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { useAuth, useLogout } from '../services/auth';
import { useToast } from './toaster';

const navItem = ({ isActive }: { isActive: boolean }) =>
  `px-3 py-2 rounded-md text-sm font-medium ${
    isActive ? 'bg-slate-800 text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white'
  }`;

const mobileNavItem = ({ isActive }: { isActive: boolean }) =>
  `block px-3 py-3 rounded-md text-base font-medium ${
    isActive ? 'bg-slate-800 text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white'
  }`;

export default function Layout({ children }: { children: ReactNode }) {
  const { data: auth } = useAuth();
  const logout = useLogout();
  const toast = useToast();
  const [menuOpen, setMenuOpen] = useState(false);

  const closeMenu = () => setMenuOpen(false);

  return (
    <div className="min-h-full flex flex-col">
      <nav className="bg-slate-900 border-b border-slate-800">
        <div className="max-w-6xl mx-auto px-4 flex items-center justify-between h-14">
          <Link to="/" className="text-lg font-semibold text-white">Collectify</Link>
          <div className="hidden md:flex items-center gap-1">
            <NavLink to="/movies" className={navItem}>Movies</NavLink>
            <NavLink to="/music" className={navItem}>Music</NavLink>
            <NavLink to="/games" className={navItem}>Games</NavLink>
            <NavLink to="/tags" className={navItem}>Tags</NavLink>
          </div>
          <div className="hidden md:flex items-center gap-3 text-sm text-slate-400">
            <span>{auth?.userName}</span>
            <button
              onClick={() =>
                logout.mutate(undefined, { onSuccess: () => toast.success('Signed out.') })
              }
              className="text-slate-300 hover:text-white"
            >
              Sign out
            </button>
          </div>
          <button
            type="button"
            aria-label={menuOpen ? 'Close menu' : 'Open menu'}
            aria-expanded={menuOpen}
            aria-controls="mobile-nav"
            onClick={() => setMenuOpen((v) => !v)}
            className="md:hidden inline-flex items-center justify-center w-11 h-11 rounded-md text-slate-200 hover:bg-slate-800"
          >
            <svg aria-hidden viewBox="0 0 24 24" className="w-6 h-6" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              {menuOpen ? (
                <>
                  <line x1="18" y1="6" x2="6" y2="18" />
                  <line x1="6" y1="6" x2="18" y2="18" />
                </>
              ) : (
                <>
                  <line x1="3" y1="6" x2="21" y2="6" />
                  <line x1="3" y1="12" x2="21" y2="12" />
                  <line x1="3" y1="18" x2="21" y2="18" />
                </>
              )}
            </svg>
          </button>
        </div>
        {menuOpen && (
          <div id="mobile-nav" className="md:hidden border-t border-slate-800 px-4 py-3 space-y-1">
            <NavLink to="/movies" className={mobileNavItem} onClick={closeMenu}>Movies</NavLink>
            <NavLink to="/music" className={mobileNavItem} onClick={closeMenu}>Music</NavLink>
            <NavLink to="/games" className={mobileNavItem} onClick={closeMenu}>Games</NavLink>
            <NavLink to="/tags" className={mobileNavItem} onClick={closeMenu}>Tags</NavLink>
            <div className="pt-3 mt-2 border-t border-slate-800 flex items-center justify-between text-sm">
              <span className="text-slate-400">{auth?.userName}</span>
              <button
                onClick={() => {
                  closeMenu();
                  logout.mutate(undefined, { onSuccess: () => toast.success('Signed out.') });
                }}
                className="px-3 py-2 rounded-md text-slate-200 hover:bg-slate-800"
              >
                Sign out
              </button>
            </div>
          </div>
        )}
      </nav>
      <main className="flex-1 max-w-6xl w-full mx-auto px-4 py-6">{children}</main>
    </div>
  );
}
