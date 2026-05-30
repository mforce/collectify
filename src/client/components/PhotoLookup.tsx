import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { lookupByImage } from '../services/lookup';
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
  | { kind: 'preview' }
  | { kind: 'confirm'; thumbnail: string; fullFrame: string }
  | { kind: 'searching' }
  | { kind: 'results'; results: object[]; configured: boolean; hint?: string }
  | { kind: 'error'; message: string };

const MAX_DIM = 1200;
const JPEG_QUALITY = 0.7;

/**
 * "Snap cover photo" affordance used at the top of each media form. Opens
 * a camera preview, lets the user snap + confirm, resizes the image
 * client-side, uploads it to /api/lookup/{type}/by-image, and renders
 * the candidate list inline. Picking a candidate fires `onPick` with the
 * same shape OnlineSearch uses.
 *
 * State machine: idle → preview → confirm → searching → results
 *
 * Escape key closes the modal at any step. The video stream is cleaned
 * up on unmount or when leaving preview mode.
 */
export default function PhotoLookup<T extends MediaType>({
  type,
  onPick,
  renderItem,
}: Props<T>) {
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);

  const startCamera = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' } },
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
    } catch {
      setPhase({
        kind: 'error',
        message:
          'Camera access requires a secure context (HTTPS or localhost). Make sure you are on https:// or localhost.',
      });
    }
  }, []);

  const stopCamera = useCallback(() => {
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
  }, []);

  // Start camera when entering preview
  useEffect(() => {
    if (phase.kind === 'preview') {
      startCamera();
    }
  }, [phase.kind, startCamera]);

  // Cleanup stream on unmount
  useEffect(() => {
    return () => stopCamera();
  }, [stopCamera]);

  // Cleanup stream when leaving preview
  useEffect(() => {
    if (phase.kind !== 'preview' && streamRef.current) {
      stopCamera();
    }
  }, [phase.kind, stopCamera]);

  // Escape key closes modal
  useEffect(() => {
    if (phase.kind === 'idle') return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        stopCamera();
        setPhase({ kind: 'idle' });
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [phase.kind, stopCamera]);

  const handleSnap = useCallback(() => {
    const video = videoRef.current;
    if (!video) return;

    const ctx = document.createElement('canvas').getContext('2d');
    if (!ctx) return;
    ctx.canvas.width = video.videoWidth;
    ctx.canvas.height = video.videoHeight;
    ctx.drawImage(video, 0, 0);
    stopCamera();

    // Full-resolution data URL for upload
    const fullFrame = ctx.canvas.toDataURL('image/jpeg', 0.9);

    // Thumbnail for the confirm preview (320px)
    const thumbCanvas = document.createElement('canvas');
    const thumbSize = 320;
    const ratio = Math.min(thumbSize / video.videoWidth, thumbSize / video.videoHeight);
    thumbCanvas.width = video.videoWidth * ratio;
    thumbCanvas.height = video.videoHeight * ratio;
    thumbCanvas.getContext('2d')!.drawImage(video, 0, 0, thumbCanvas.width, thumbCanvas.height);
    const thumbnail = thumbCanvas.toDataURL('image/jpeg', 0.6);

    setPhase({ kind: 'confirm', thumbnail, fullFrame });
  }, [stopCamera]);

  const handleSearch = useCallback(async () => {
    const p = phase;
    if (p.kind !== 'confirm') return;

    // Decode the full-frame capture, resize to 1200px, and upload
    const img = new Image();
    img.onload = async () => {
      const canvas = document.createElement('canvas');
      const scale = Math.min(MAX_DIM / img.width, MAX_DIM / img.height, 1);
      canvas.width = Math.round(img.width * scale);
      canvas.height = Math.round(img.height * scale);
      canvas.getContext('2d')!.drawImage(img, 0, 0, canvas.width, canvas.height);

      setPhase({ kind: 'searching' });

      canvas.toBlob(async (blob) => {
        if (!blob) {
          setPhase({ kind: 'error', message: 'Could not encode image.' });
          return;
        }
        try {
          const resp = await lookupByImage(type, blob);
          setPhase({
            kind: 'results',
            results: resp.results as object[],
            configured: resp.configured,
            hint: resp.hint,
          });
        } catch (err) {
          setPhase({ kind: 'error', message: (err as Error).message ?? 'Lookup failed.' });
        }
      }, 'image/jpeg', JPEG_QUALITY);
    };
    img.src = p.fullFrame;
  }, [phase, type]);

  const handleClose = useCallback(() => {
    stopCamera();
    setPhase({ kind: 'idle' });
  }, [stopCamera]);

  // ---------- Render ----------

  if (phase.kind === 'idle') {
    return (
      <Button type="button" variant="secondary" onClick={() => setPhase({ kind: 'preview' })}>
        Snap cover photo
      </Button>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/90">
      <div className="relative w-full max-w-lg mx-auto">
        {/* Close button */}
        <button
          type="button"
          onClick={handleClose}
          className="absolute -top-10 right-0 text-white/70 hover:text-white text-2xl leading-none"
          aria-label="Close"
        >
          &times;
        </button>

        {/* Preview */}
        {phase.kind === 'preview' && (
          <div className="space-y-4">
            <video
              ref={videoRef}
              autoPlay
              playsInline
              muted
              className="w-full rounded-lg bg-black"
            />
            <div className="flex gap-2 justify-center">
              <Button type="button" variant="secondary" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="button" onClick={handleSnap}>
                Snap
              </Button>
            </div>
          </div>
        )}

        {/* Confirm */}
        {phase.kind === 'confirm' && (
          <div className="space-y-4 text-center">
          <img
                src={phase.thumbnail}
                alt="Photo preview"
                className="w-full rounded-lg bg-black"
              />
              <p className="text-sm text-white/70">Does this look right?</p>
              <div className="flex gap-2 justify-center">
                <Button type="button" variant="secondary" onClick={handleClose}>
                  Retake
                </Button>
                <Button type="button" onClick={handleSearch}>
                  Search
                </Button>
              </div>
          </div>
        )}

        {/* Searching */}
        {phase.kind === 'searching' && (
          <div className="text-center text-white py-12 space-y-2">
            <p className="text-sm">Analysing photo…</p>
          </div>
        )}

        {/* Results */}
        {phase.kind === 'results' && !phase.configured && (
          <div className="text-center text-white py-12 space-y-2">
            <p className="text-sm">
              Photo lookup requires a Cloud Vision API key. Set it to enable image-based lookups.
            </p>
            <Button type="button" variant="secondary" onClick={handleClose}>
              Close
            </Button>
          </div>
        )}

        {/* Hint (configured but no results) */}
        {phase.kind === 'results' && phase.configured && phase.results.length === 0 && (
          <div className="text-center text-white py-12 space-y-2">
            <p className="text-sm">{phase.hint ?? 'No match found from this photo.'}</p>
            <div className="flex gap-2 justify-center">
              <Button type="button" variant="secondary" onClick={handleClose}>
                Close
              </Button>
            </div>
          </div>
        )}

        {/* Candidate list */}
        {phase.kind === 'results' && phase.results.length > 0 && (
          <div className="space-y-3">
            <p className="text-sm text-white/70 text-center">
              {phase.results.length} match{phase.results.length > 1 ? 'es' : ''} found
            </p>
            <div className="rounded-md bg-input-bg border border-border max-h-80 overflow-auto">
              {(phase.results as ResultMap[T][]).map((item, i) => {
                const view = renderItem?.(item) ?? defaultView(type, item);
                return (
                  <button
                    type="button"
                    key={`${(item as { providerKey?: string }).providerKey ?? i}`}
                    onClick={() => {
                      onPick(item);
                      handleClose();
                    }}
                    className="category-hover-soft flex w-full items-start gap-3 border-b border-border px-3 py-2 text-left transition-colors last:border-b-0"
                  >
                    {view.image && (
                      <img
                        src={view.image}
                        alt=""
                        className="w-10 h-14 object-cover rounded flex-none"
                      />
                    )}
                    <div className="min-w-0 flex-1">
                      <div className="text-sm text-text-primary truncate">{view.primary}</div>
                      {view.secondary && (
                        <div className="text-xs text-text-secondary truncate">
                          {view.secondary}
                        </div>
                      )}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* Error */}
        {phase.kind === 'error' && (
          <div className="text-center text-white py-12 space-y-3">
            <p className="text-sm text-error">{phase.message}</p>
            <Button type="button" variant="secondary" onClick={handleClose}>
              Close
            </Button>
          </div>
        )}
      </div>
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
