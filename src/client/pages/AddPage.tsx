import { useLocation, useNavigate } from 'react-router-dom';
import { useCreate } from '../services/collection';
import { useToast } from '../components/toaster';
import type { Album, Game, MediaType, Movie } from '../services/types';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import { Card } from '../components/ui';
import MediaIcon from '../components/MediaIcon';
import { MEDIA } from '../services/mediaRegistry';

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

export default function AddPage<T extends MediaType>({ type }: { type: T }) {
  const create = useCreate(type);
  const nav = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const navState = location.state as PrefillState | null;
  const prefill = navState?.prefill;
  const prefillBarcode = navState?.barcodeOnly;

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
            prefillLookup={prefill as MovieLookupResult | undefined}
            prefillBarcode={prefillBarcode}
            submitting={create.isPending}
            submitLabel="Create"
            onSubmit={(m) =>
              create.mutate(m as Movie & Album & Game, {
                onSuccess: (created: any) => onSuccess(created?.id),
                onError: onFailure,
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
                onError: onFailure,
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
                onError: onFailure,
              })
            }
          />
        )}
      </Card>
    </div>
  );
}
