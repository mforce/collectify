import { useLocation, useNavigate } from 'react-router-dom';
import { useCreate } from '../services/collection';
import { useToast } from '../components/toaster';
import type { MediaType } from '../services/types';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';
import MediaIcon from '../components/MediaIcon';
import { MEDIA, type MediaResultMap } from '../services/mediaRegistry';

interface PrefillState<T extends MediaType> {
  prefill?: MediaResultMap[T];
  /**
   * Soft-fallback hint from the list-page scanner: a barcode the user
   * scanned that didn't match any provider. We seed only the barcode
   * field so the user can finish via the reliable title search without
   * retyping the UPC.
   */
  barcodeOnly?: string;
}

export default function AddPage<T extends MediaType>({ type }: { type: T }) {
  const createMovie = useCreate('movies');
  const createAlbum = useCreate('music');
  const createGame = useCreate('games');
  const nav = useNavigate();
  const location = useLocation();
  const movieState: PrefillState<'movies'> | null = location.state;
  const albumState: PrefillState<'music'> | null = location.state;
  const gameState: PrefillState<'games'> | null = location.state;
  const toast = useToast();

  const onSuccess = (id?: number) => {
    // Toast survives the navigate so the user gets a confirmation on
    // the detail page they land on.
    toast.success(MEDIA[type].addSuccess);
    if (id) nav(`/${type}/${id}`);
    else nav(`/${type}`);
  };

  const onFailure = (err: unknown) => {
    toast.error(`Failed to save: ${(err as Error).message ?? 'unknown error'}`);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-border bg-card shadow-sm">
          <MediaIcon type={type} className="h-7 w-7" />
        </span>
        <h1 className={`text-3xl font-extrabold tracking-tight ${MEDIA[type].theme.heading}`}>{MEDIA[type].addTitle}</h1>
      </div>

      <Card className={`theme-${type}`}>
        {type === 'movies' && (
          <MovieForm
            prefillLookup={movieState?.prefill}
            prefillBarcode={movieState?.barcodeOnly}
            submitting={createMovie.isPending}
            submitLabel="Create"
            onSubmit={(m) =>
              createMovie.mutate(m, {
                onSuccess: (created) => onSuccess(created?.id),
                onError: onFailure,
              })
            }
          />
        )}
        {type === 'music' && (
          <AlbumForm
            prefillLookup={albumState?.prefill}
            prefillBarcode={albumState?.barcodeOnly}
            submitting={createAlbum.isPending}
            submitLabel="Create"
            onSubmit={(a) =>
              createAlbum.mutate(a, {
                onSuccess: (created) => onSuccess(created?.id),
                onError: onFailure,
              })
            }
          />
        )}
        {type === 'games' && (
          <GameForm
            prefillLookup={gameState?.prefill}
            prefillBarcode={gameState?.barcodeOnly}
            submitting={createGame.isPending}
            submitLabel="Create"
            onSubmit={(g) =>
              createGame.mutate(g, {
                onSuccess: (created) => onSuccess(created?.id),
                onError: onFailure,
              })
            }
          />
        )}
      </Card>
    </div>
  );
}
