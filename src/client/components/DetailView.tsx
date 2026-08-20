import { Link } from 'react-router-dom';
import type { Album, Game, Movie } from '../services/types';
import MovieDetail from './MovieDetail';
import MusicDetail from './MusicDetail';
import GameDetail from './GameDetail';
import { detailTheme } from './detailShared';

interface Props<T> {
  item: T;
  type: 'movies' | 'music' | 'games';
  onEdit: () => void;
}

export default function DetailView<T extends Movie | Album | Game>({
  item,
  type,
  onEdit,
}: Props<T>) {
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <Link
          to={`/${type}`}
          className={`flex items-center gap-1 text-sm font-semibold ${detailTheme(type).title}`}
        >
          ← Back to {type}
        </Link>
        <button
          type="button"
          onClick={onEdit}
          className={`inline-flex min-h-[40px] items-center rounded-xl border bg-card px-4 py-1.5 text-sm font-bold ${detailTheme(type).button}`}
        >
          Edit
        </button>
      </div>
      {type === 'movies' && <MovieDetail item={item as Movie} />}{' '}
      {type === 'music' && <MusicDetail item={item as Album} />}{' '}
      {type === 'games' && <GameDetail item={item as Game} />}
    </div>
  );
}
