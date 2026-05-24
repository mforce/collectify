import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useDelete, useItem, useUpdate } from '../services/collection';
import { useToast } from '../components/toaster';
import DetailView from '../components/DetailView';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card, Button } from '../components/ui';
import type { Album, Game, MediaType, Movie } from '../services/types';

const titleByType: Record<MediaType, string> = {
  movies: 'Movie',
  music: 'Album',
  games: 'Game',
};

const deletedByType: Record<MediaType, string> = {
  movies: 'Movie deleted.',
  music: 'Album deleted.',
  games: 'Game deleted.',
};

export default function EditPage<T extends MediaType>({ type }: { type: T }) {
  const { id } = useParams<{ id: string }>();
  const idNum = id ? Number(id) : undefined;
  const item = useItem(type, idNum);
  const update = useUpdate(type);
  const del = useDelete(type);
  const nav = useNavigate();
  const toast = useToast();

  const [editing, setEditing] = useState(false);

  if (item.isLoading) return <p className="text-text-secondary">Loading…</p>;
  if (item.error || !item.data) return <p className="text-error">Not found.</p>;

  const onDelete = () => {
    if (!idNum) return;
    if (!confirm('Delete this entry?')) return;
    del.mutate(idNum, {
      onSuccess: () => {
        toast.success(deletedByType[type]);
        nav(`/${type}`);
      },
      onError: (err) => toast.error(`Failed to delete: ${(err as Error).message ?? 'unknown error'}`),
    });
  };

  const onSaved = () => {
    toast.success('Saved.');
    setEditing(false);
  };

  const onSaveFailure = (err: unknown) =>
    toast.error(`Failed to save: ${(err as Error).message ?? 'unknown error'}`);

  // Detail view (default)
  if (!editing) {
    return <DetailView item={item.data} type={type} onEdit={() => setEditing(true)} />;
  }

  // Edit mode
  const onCancel = () => setEditing(false);

  return (
    <div className="space-y-4">
      {/* Header with back and cancel */}
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-xl font-medium text-text-primary tracking-tight">Edit {titleByType[type]}</h1>
        <Button variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>

      <Card>
        {type === 'movies' && (
          <MovieForm
            initial={item.data as Movie}
            submitting={update.isPending}
            onSubmit={(m) => update.mutate({ ...m, id: idNum! } as any, { onSuccess: onSaved, onError: onSaveFailure })}
            onDelete={onDelete}
          />
        )}
        {type === 'music' && (
          <AlbumForm
            initial={item.data as Album}
            submitting={update.isPending}
            onSubmit={(a) => update.mutate({ ...a, id: idNum! } as any, { onSuccess: onSaved, onError: onSaveFailure })}
            onDelete={onDelete}
          />
        )}
        {type === 'games' && (
          <GameForm
            initial={item.data as Game}
            submitting={update.isPending}
            onSubmit={(g) => update.mutate({ ...g, id: idNum! } as any, { onSuccess: onSaved, onError: onSaveFailure })}
            onDelete={onDelete}
          />
        )}
      </Card>
    </div>
  );
}
