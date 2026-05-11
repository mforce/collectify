import type { ReactNode } from 'react';
import { Link, NavLink } from 'react-router-dom';
import { useAuth, useLogout } from '../services/auth';
import { useToast } from './toaster';

const navItem = ({ isActive }: { isActive: boolean }) =>
  `px-3 py-2 rounded-md text-sm font-medium ${
    isActive ? 'bg-slate-800 text-white' : 'text-slate-300 hover:bg-slate-800 hover:text-white'
  }`;

export default function Layout({ children }: { children: ReactNode }) {
  const { data: auth } = useAuth();
  const logout = useLogout();
  const toast = useToast();

  return (
    <div className="min-h-full flex flex-col">
      <nav className="bg-slate-900 border-b border-slate-800">
        <div className="max-w-6xl mx-auto px-4 flex items-center justify-between h-14">
          <Link to="/" className="text-lg font-semibold text-white">Collectify</Link>
          <div className="flex items-center gap-1">
            <NavLink to="/movies" className={navItem}>Movies</NavLink>
            <NavLink to="/music" className={navItem}>Music</NavLink>
            <NavLink to="/games" className={navItem}>Games</NavLink>
            <NavLink to="/tags" className={navItem}>Tags</NavLink>
          </div>
          <div className="flex items-center gap-3 text-sm text-slate-400">
            <span className="hidden sm:inline">{auth?.userName}</span>
            <button
              onClick={() =>
                logout.mutate(undefined, { onSuccess: () => toast.success('Signed out.') })
              }
              className="text-slate-300 hover:text-white"
            >
              Sign out
            </button>
          </div>
        </div>
      </nav>
      <main className="flex-1 max-w-6xl w-full mx-auto px-4 py-6">{children}</main>
    </div>
  );
}
