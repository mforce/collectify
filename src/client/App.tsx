import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './services/auth';
import Layout from './components/Layout';
import { Toaster } from './components/toaster';
import Dashboard from './pages/Dashboard';
import Setup from './pages/Setup';
import Login from './pages/Login';
import Register from './pages/Register';
import MoviesList from './pages/MoviesList';
import MusicList from './pages/MusicList';
import GamesList from './pages/GamesList';
import ImportSteam from './pages/ImportSteam';
import EditPage from './pages/EditPage';
import AddPage from './pages/AddPage';
import TagsPage from './pages/Tags';

export default function App() {
  const { data: auth, isLoading } = useAuth();

  if (isLoading) {
    return <div className="p-8 text-slate-400">Loading…</div>;
  }

  // Toaster is mounted outside the auth-state branches so success
  // toasts survive the navigate after login / logout / setup.
  if (auth?.needsSetup) {
    return (
      <>
        <Routes>
          <Route path="/setup" element={<Setup />} />
          <Route path="*" element={<Navigate to="/setup" replace />} />
        </Routes>
        <Toaster />
      </>
    );
  }

  if (!auth?.isAuthenticated) {
    return (
      <>
        <Routes>
          <Route path="/login" element={<Login />} />
          {auth?.allowRegistration && <Route path="/register" element={<Register />} />}
          <Route path="*" element={<Navigate to="/login" replace />} />
        </Routes>
        <Toaster />
      </>
    );
  }

  return (
    <Layout>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/movies" element={<MoviesList />} />
        <Route path="/movies/new" element={<AddPage type="movies" />} />
        <Route path="/movies/:id" element={<EditPage type="movies" />} />
        <Route path="/music" element={<MusicList />} />
        <Route path="/music/new" element={<AddPage type="music" />} />
        <Route path="/music/:id" element={<EditPage type="music" />} />
        <Route path="/games" element={<GamesList />} />
        <Route path="/import/steam" element={<ImportSteam />} />
        <Route path="/games/new" element={<AddPage type="games" />} />
        <Route path="/games/:id" element={<EditPage type="games" />} />
        <Route path="/tags" element={<TagsPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <Toaster />
    </Layout>
  );
}
