import { useNavigate, useParams } from 'react-router-dom';
import { useDelete, useItem, useUpdate } from '../services/collection';
import type { Album, Game, MediaType, Movie } from '../services/types';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';

export default function EditPage<T extends MediaType>({ type }: { type: T }) {
  const { id } = useParams<{ id: string }>();
  const idNum = id ? Number(id) : undefined;
  const item = useItem(type, idNum);
  const update = useUpdate(type);
  const del = useDelete(type);
  const nav = useNavigate();

  if (item.isLoading) return <p className="text-slate-400">Loading…</p>;
  if (item.error || !item.data) return <p className="text-rose-400">Not found.</p>;

  const onDelete = () => {
    if (!idNum) return;
    if (!confirm('Delete this entry?')) return;
    del.mutate(idNum, { onSuccess: () => nav(`/${type}`) });
  };

  const titleByType: Record<MediaType, string> = {
    movies: 'Edit movie',
    music: 'Edit album',
    games: 'Edit game',
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-white">{titleByType[type]}</h1>
      <Card>
        {type === 'movies' && (
          <MovieForm
            initial={item.data as Movie}
            submitting={update.isPending}
            onSubmit={(m) => update.mutate({ ...m, id: idNum! } as any)}
            onDelete={onDelete}
          />
        )}
        {type === 'music' && (
          <AlbumForm
            initial={item.data as Album}
            submitting={update.isPending}
            onSubmit={(a) => update.mutate({ ...a, id: idNum! } as any)}
            onDelete={onDelete}
          />
        )}
        {type === 'games' && (
          <GameForm
            initial={item.data as Game}
            submitting={update.isPending}
            onSubmit={(g) => update.mutate({ ...g, id: idNum! } as any)}
            onDelete={onDelete}
          />
        )}
        {update.error && <p className="mt-3 text-sm text-rose-400">{(update.error as Error).message}</p>}
      </Card>
    </div>
  );
}
