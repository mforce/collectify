import { Link } from 'react-router-dom';
import { useList } from '../services/collection';
import { Card } from '../components/ui';

export default function Dashboard() {
  const movies = useList('movies', '');
  const music = useList('music', '');
  const games = useList('games', '');

  const tiles = [
    { to: '/movies', label: 'Movies', count: movies.data?.length ?? '…' },
    { to: '/music', label: 'Music', count: music.data?.length ?? '…' },
    { to: '/games', label: 'Games', count: games.data?.length ?? '…' },
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-white">Your collection</h1>
      <div className="grid sm:grid-cols-3 gap-4">
        {tiles.map((t) => (
          <Link key={t.to} to={t.to} className="block">
            <Card className="hover:border-indigo-500 transition-colors">
              <div className="text-slate-400 text-sm">{t.label}</div>
              <div className="text-3xl font-semibold text-white mt-1">{t.count}</div>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
