import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useDelete, useItem, useUpdate } from '../services/collection';
import type { Album, Game, MediaType, Movie } from '../services/types';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';

const titleByType: Record<MediaType, string> = {
  movies: 'Edit movie',
  music: 'Edit album',
  games: 'Edit game',
};

export default function EditPage<T extends MediaType>({ type }: { type: T }) {
  const { id } = useParams<{ id: string }>();
  const idNum = id ? Number(id) : undefined;
  const item = useItem(type, idNum);
  const update = useUpdate(type);
  const del = useDelete(type);
  const nav = useNavigate();

  // Save feedback shared between success and error so the visuals
  // mirror AddPage. Success auto-clears; error sticks until the next
  // attempt.
  const [feedback, setFeedback] = useState<
    | { kind: 'idle' }
    | { kind: 'success'; message: string }
    | { kind: 'error'; message: string }
  >({ kind: 'idle' });

  useEffect(() => {
    if (update.error) {
      setFeedback({ kind: 'error', message: (update.error as Error).message });
    }
  }, [update.error]);

  // Auto-clear the success banner so it doesn't linger forever.
  useEffect(() => {
    if (feedback.kind !== 'success') return;
    const t = setTimeout(() => setFeedback({ kind: 'idle' }), 2000);
    return () => clearTimeout(t);
  }, [feedback.kind]);

  if (item.isLoading) return <p className="text-slate-400">Loading…</p>;
  if (item.error || !item.data) return <p className="text-rose-400">Not found.</p>;

  const onDelete = () => {
    if (!idNum) return;
    if (!confirm('Delete this entry?')) return;
    del.mutate(idNum, { onSuccess: () => nav(`/${type}`) });
  };

  const onSaved = () => setFeedback({ kind: 'success', message: 'Saved.' });

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-white">{titleByType[type]}</h1>

      {feedback.kind === 'success' && (
        <div
          role="status"
          aria-live="polite"
          className="rounded-md border border-emerald-500/40 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-200"
        >
          {feedback.message}
        </div>
      )}
      {feedback.kind === 'error' && (
        <div
          role="alert"
          aria-live="assertive"
          className="rounded-md border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-sm text-rose-200"
        >
          Failed to save: {feedback.message}
        </div>
      )}

      <Card>
        {type === 'movies' && (
          <MovieForm
            initial={item.data as Movie}
            submitting={update.isPending}
            onSubmit={(m) => update.mutate({ ...m, id: idNum! } as any, { onSuccess: onSaved })}
            onDelete={onDelete}
          />
        )}
        {type === 'music' && (
          <AlbumForm
            initial={item.data as Album}
            submitting={update.isPending}
            onSubmit={(a) => update.mutate({ ...a, id: idNum! } as any, { onSuccess: onSaved })}
            onDelete={onDelete}
          />
        )}
        {type === 'games' && (
          <GameForm
            initial={item.data as Game}
            submitting={update.isPending}
            onSubmit={(g) => update.mutate({ ...g, id: idNum! } as any, { onSuccess: onSaved })}
            onDelete={onDelete}
          />
        )}
      </Card>
    </div>
  );
}
