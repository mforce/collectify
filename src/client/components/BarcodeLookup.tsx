import { lazy, Suspense, useState, type ReactNode } from 'react';
import { lookupByBarcode } from '../services/lookup';

// Lazy-load the scanner so the ~450 KB @zxing/browser bundle only ships
// when a user actually clicks "Scan barcode" -- keeps the initial load
// snappy for users who never scan (most of them, on the desktop list
// pages).
const BarcodeScanner = lazy(() => import('./BarcodeScanner'));
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';
import type { MediaType } from '../services/types';
import { Button } from './ui';

type ResultMap = {
  movies: MovieLookupResult;
  music: MusicLookupResult;
  games: GameLookupResult;
};

interface Props<T extends MediaType> {
  type: T;
  onPick: (item: ResultMap[T]) => void;
  /** Optional row renderer; mirrors OnlineSearch so callers can share the same fn. */
  renderItem?: (item: ResultMap[T]) => { primary: string; secondary?: ReactNode; image?: string | null };
}

type Phase =
  | { kind: 'idle' }
  | { kind: 'searching'; code: string }
  | { kind: 'results'; code: string; results: object[]; configured: boolean }
  | { kind: 'error'; message: string };

/**
 * "Scan barcode" affordance used at the top of each media form. Opens
 * BarcodeScanner, calls /api/lookup/{type}/by-barcode/{code} once a code
 * is detected, and renders the candidate list inline. Picking a candidate
 * fires `onPick` with the same shape OnlineSearch uses, so the form's
 * existing import flow handles both entry points uniformly.
 *
 * Multiple candidates are common when a UPC is shared across editions
 * (Blu-ray + UHD + box-set), so we always render a list rather than
 * auto-importing the first hit.
 */
export default function BarcodeLookup<T extends MediaType>({ type, onPick, renderItem }: Props<T>) {
  const [scannerOpen, setScannerOpen] = useState(false);
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });

  const handleDetected = async (code: string) => {
    setScannerOpen(false);
    setPhase({ kind: 'searching', code });
    try {
      const resp = await lookupByBarcode(type, code);
      setPhase({
        kind: 'results',
        code,
        results: resp.results as object[],
        configured: resp.configured,
      });
    } catch (err) {
      setPhase({ kind: 'error', message: (err as Error).message ?? 'Lookup failed.' });
    }
  };

  return (
    <div className="space-y-2">
      <Button type="button" variant="secondary" onClick={() => setScannerOpen(true)}>
        Scan barcode
      </Button>

      {scannerOpen && (
        <Suspense fallback={null}>
          <BarcodeScanner
            open={scannerOpen}
            onDetected={handleDetected}
            onClose={() => setScannerOpen(false)}
          />
        </Suspense>
      )}

      {phase.kind === 'searching' && (
        <p className="text-xs text-slate-400">Looking up {phase.code}…</p>
      )}
      {phase.kind === 'error' && <p className="text-xs text-rose-300">{phase.message}</p>}
      {phase.kind === 'results' && !phase.configured && (
        <p className="text-xs text-slate-400">
          Online lookup not configured. Set the provider key to enable barcode matches.
        </p>
      )}
      {phase.kind === 'results' && phase.configured && phase.results.length === 0 && (
        <p className="text-xs text-slate-400">No match for {phase.code}.</p>
      )}
      {phase.kind === 'results' && phase.results.length > 0 && (
        <div className="rounded-md bg-slate-900 border border-slate-700 max-h-80 overflow-auto">
          {(phase.results as ResultMap[T][]).map((item, i) => {
            const view = renderItem?.(item) ?? defaultView(type, item);
            return (
              <button
                type="button"
                key={`${(item as { providerKey?: string }).providerKey ?? i}`}
                onClick={() => {
                  onPick(item);
                  setPhase({ kind: 'idle' });
                }}
                className="w-full text-left flex gap-3 items-start px-3 py-2 hover:bg-slate-800 border-b border-slate-800 last:border-b-0"
              >
                {view.image && (
                  <img src={view.image} alt="" className="w-10 h-14 object-cover rounded flex-none" />
                )}
                <div className="min-w-0 flex-1">
                  <div className="text-sm text-slate-100 truncate">{view.primary}</div>
                  {view.secondary && (
                    <div className="text-xs text-slate-400 truncate">{view.secondary}</div>
                  )}
                </div>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

function defaultView<T extends MediaType>(
  _type: T,
  item: ResultMap[T],
): { primary: string; secondary?: string; image?: string | null } {
  const r = item as Partial<MovieLookupResult & MusicLookupResult & GameLookupResult>;
  const primary = (r.title ?? '') + (r.year ? ` (${r.year})` : '');
  const gameBits = [(r as GameLookupResult).developer, (r as GameLookupResult).platform]
    .filter(Boolean)
    .join(' · ');
  const secondary =
    (r as MusicLookupResult).artistName ??
    (gameBits || r.description?.slice(0, 120) || undefined);
  return { primary, secondary, image: r.imageUrl ?? null };
}
