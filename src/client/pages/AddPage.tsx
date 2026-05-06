import { useNavigate } from 'react-router-dom';
import { useCreate } from '../services/collection';
import type { Album, Game, MediaType, Movie } from '../services/types';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';

export default function AddPage<T extends MediaType>({ type }: { type: T }) {
  const create = useCreate(type);
  const nav = useNavigate();

  const onSuccess = (id?: number) => {
    if (id) nav(`/${type}/${id}`);
    else nav(`/${type}`);
  };

  const titleByType: Record<MediaType, string> = {
    movies: 'Add a movie',
    music: 'Add an album',
    games: 'Add a game',
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-white">{titleByType[type]}</h1>
      <Card>
        {type === 'movies' && (
          <MovieForm
            submitting={create.isPending}
            submitLabel="Create"
            onSubmit={(m) =>
              create.mutate(m as Movie & Album & Game, {
                onSuccess: (created: any) => onSuccess(created?.id),
              })
            }
          />
        )}
        {type === 'music' && (
          <AlbumForm
            submitting={create.isPending}
            submitLabel="Create"
            onSubmit={(a) =>
              create.mutate(a as Movie & Album & Game, {
                onSuccess: (created: any) => onSuccess(created?.id),
              })
            }
          />
        )}
        {type === 'games' && (
          <GameForm
            submitting={create.isPending}
            submitLabel="Create"
            onSubmit={(g) =>
              create.mutate(g as Movie & Album & Game, {
                onSuccess: (created: any) => onSuccess(created?.id),
              })
            }
          />
        )}
        {create.error && <p className="mt-3 text-sm text-rose-400">{(create.error as Error).message}</p>}
      </Card>
    </div>
  );
}
