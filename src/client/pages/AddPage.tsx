import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useCreate } from '../services/collection';
import type { Album, Game, MediaType, Movie } from '../services/types';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';

interface PrefillState {
  prefill?: MovieLookupResult | MusicLookupResult | GameLookupResult;
  /**
   * Soft-fallback hint from the list-page scanner: a barcode the user
   * scanned that didn't match any provider. We seed only the barcode
   * field so the user can finish via the reliable title search without
   * retyping the UPC.
   */
  barcodeOnly?: string;
}

const titleByType: Record<MediaType, string> = {
  movies: 'Add a movie',
  music: 'Add an album',
  games: 'Add a game',
};

export default function AddPage<T extends MediaType>({ type }: { type: T }) {
  const create = useCreate(type);
  const nav = useNavigate();
  const location = useLocation();
  const navState = location.state as PrefillState | null;
  const prefill = navState?.prefill;
  const prefillBarcode = navState?.barcodeOnly;

  // Persisted-feedback so the success banner can stay on screen briefly
  // before we redirect to the detail page.
  const [feedback, setFeedback] = useState<
    | { kind: 'idle' }
    | { kind: 'success'; message: string }
    | { kind: 'error'; message: string }
  >({ kind: 'idle' });

  // Surface server / network errors as a banner instead of just a small
  // line under the form. The mutation reports the latest error via
  // create.error, but it's clearer to mirror it into our feedback state
  // so the success / error visuals share one slot.
  useEffect(() => {
    if (create.error) {
      setFeedback({ kind: 'error', message: (create.error as Error).message });
    }
  }, [create.error]);

  const onSuccess = (id?: number) => {
    setFeedback({ kind: 'success', message: 'Saved! Redirecting…' });
    // Brief hold so the user sees the banner; the page navigates after.
    setTimeout(() => {
      if (id) nav(`/${type}/${id}`);
      else nav(`/${type}`);
    }, 600);
  };

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
            prefillLookup={prefill as MovieLookupResult | undefined}
            prefillBarcode={prefillBarcode}
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
            prefillLookup={prefill as MusicLookupResult | undefined}
            prefillBarcode={prefillBarcode}
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
            prefillLookup={prefill as GameLookupResult | undefined}
            prefillBarcode={prefillBarcode}
            submitting={create.isPending}
            submitLabel="Create"
            onSubmit={(g) =>
              create.mutate(g as Movie & Album & Game, {
                onSuccess: (created: any) => onSuccess(created?.id),
              })
            }
          />
        )}
      </Card>
    </div>
  );
}
