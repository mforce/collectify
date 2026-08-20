import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { lookupByImage } from '../services/lookup';
import type { MediaResultMap } from '../services/mediaRegistry';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';
import type { MediaType } from '../services/types';
import { Button } from './ui';

interface Props<T extends MediaType> {
  type: T;
  onPick: (item: MediaResultMap[T]) => void;
  /** Optional row renderer; mirrors OnlineSearch so callers can share the same fn. */
  renderItem?: (item: MediaResultMap[T]) => { primary: string; secondary?: ReactNode; image?: string | null };
}

type Phase =
  | { kind: 'idle' }
  | { kind: 'preview' }
  | { kind: 'confirm'; thumbnail: string }
  | { kind: 'searching' }
  | { kind: 'results'; results: object[]; configured: boolean; hint?: string }
  | { kind: 'error'; message: string };

const MAX_DIM = 2000;
const UPLOAD_QUALITY = 0.95;

/**
 * "Snap cover photo" affordance used at the top of each media form. Two
 * entry points:
 *
 * 1. Camera — live preview → snap → confirm → upload
 * 2. File picker — choose image → confirm → upload
 *
 * Both paths resize client-side and POST to /api/lookup/{type}/by-image.
 * The confirm step and everything after are shared.
 *
 * State machine: idle → preview | upload-file → confirm → searching → results
 *
 * Escape key closes the modal at any step. The video stream (camera path)
 * is cleaned up on unmount or when leaving preview mode.
 */
export default function PhotoLookup<T extends MediaType>({
  type,
  onPick,
  renderItem,
}: Props<T>) {
  const [phase, setPhase] = useState<Phase>({ kind: 'idle' });
  const [filterText, setFilterText] = useState('');
  const videoRef = useRef<HTMLVideoElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  // For camera path: keep the raw canvas frame so upload encodes from
  // lossless pixels, not from a decoded-and-re-encoded JPEG.
  const frameCanvasRef = useRef<HTMLCanvasElement | null>(null);
  // For file path: keep the loaded Image element so we can resize on
  // the confirm → search transition.
  const fileImageRef = useRef<HTMLImageElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const startCamera = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
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
        fileImageRef.current = null;
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [phase.kind, stopCamera]);

  // ---------- Camera path ----------

  const handleSnap = useCallback(() => {
    const video = videoRef.current;
    if (!video) return;

    // Draw the full frame onto a reusable canvas — lossless pixels.
    const frame = document.createElement('canvas');
    frame.width = video.videoWidth;
    frame.height = video.videoHeight;
    const ctx = frame.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(video, 0, 0);
    frameCanvasRef.current = frame;
    stopCamera();

    // Thumbnail for the confirm preview (320px).
    const thumbCanvas = document.createElement('canvas');
    const thumbSize = 320;
    const ratio = Math.min(thumbSize / frame.width, thumbSize / frame.height);
    thumbCanvas.width = Math.round(frame.width * ratio);
    thumbCanvas.height = Math.round(frame.height * ratio);
    const thumbCtx = thumbCanvas.getContext('2d')!;
    thumbCtx.imageSmoothingEnabled = true;
    thumbCtx.imageSmoothingQuality = 'high';
    thumbCtx.drawImage(frame, 0, 0, thumbCanvas.width, thumbCanvas.height);
    const thumbnail = thumbCanvas.toDataURL('image/jpeg', 0.6);

    setPhase({ kind: 'confirm', thumbnail });
  }, [stopCamera]);

  // ---------- File upload path ----------

  const handleFileChange = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;

      // Reset input so re-selecting the same file still fires onChange
      e.target.value = '';

      try {
        const img = new Image();
        img.onload = () => {
          fileImageRef.current = img;

          // Thumbnail for confirm preview (320px).
          const thumbCanvas = document.createElement('canvas');
          const thumbSize = 320;
          const ratio = Math.min(thumbSize / img.width, thumbSize / img.height);
          thumbCanvas.width = Math.round(img.width * ratio);
          thumbCanvas.height = Math.round(img.height * ratio);
          const thumbCtx = thumbCanvas.getContext('2d')!;
          thumbCtx.imageSmoothingEnabled = true;
          thumbCtx.imageSmoothingQuality = 'high';
          thumbCtx.drawImage(img, 0, 0, thumbCanvas.width, thumbCanvas.height);
          const thumbnail = thumbCanvas.toDataURL('image/jpeg', 0.6);

          setPhase({ kind: 'confirm', thumbnail });
        };
        img.onerror = () => {
          setPhase({ kind: 'error', message: 'Could not load image.' });
        };
        img.src = URL.createObjectURL(file);
      } catch {
        setPhase({ kind: 'error', message: 'Could not read file.' });
      }
    },
    [],
  );

  // ---------- Shared: resize + upload ----------

  const handleSearch = useCallback(async () => {
    const p = phase;
    if (p.kind !== 'confirm') return;

    const source = frameCanvasRef.current ?? fileImageRef.current;
    if (!source) {
      setPhase({ kind: 'error', message: 'No image available.' });
      return;
    }

    // Resize from source — single JPEG encode, no intermediate decode.
    const canvas = document.createElement('canvas');
    const sw = source instanceof HTMLCanvasElement ? source.width : source.width;
    const sh = source instanceof HTMLCanvasElement ? source.height : source.height;
    const scale = Math.min(MAX_DIM / sw, MAX_DIM / sh, 1);
    canvas.width = Math.round(sw * scale);
    canvas.height = Math.round(sh * scale);
    const ctx = canvas.getContext('2d')!;
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(source, 0, 0, canvas.width, canvas.height);

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
    }, 'image/jpeg', UPLOAD_QUALITY);
  }, [phase, type]);

  const handleClose = useCallback(() => {
    stopCamera();
    fileImageRef.current = null;
    frameCanvasRef.current = null;
    setFilterText('');
    setPhase({ kind: 'idle' });
  }, [stopCamera]);

  // ---------- Render ----------

  if (phase.kind === 'idle') {
    return (
      <div className="flex gap-2">
        <Button type="button" variant="secondary" onClick={() => setPhase({ kind: 'preview' })}>
          Snap cover photo
        </Button>
        <Button
          type="button"
          variant="secondary"
          onClick={() => fileInputRef.current?.click()}
        >
          Upload image
        </Button>
        <input
          ref={fileInputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="hidden"
          onChange={handleFileChange}
        />
      </div>
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

        {/* Camera preview */}
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
                Cancel
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
            <p className="text-sm">Analysing photo&hellip;</p>
          </div>
        )}

        {/* Not configured */}
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

        {/* Configured but no results */}
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
        {phase.kind === 'results' && phase.results.length > 0 && (() => {
          const items = phase.results as MediaResultMap[T][];
          const filtered = filterText.trim()
            ? items.filter(item => {
                const r = item as Partial<MovieLookupResult & MusicLookupResult & GameLookupResult>;
                const haystack = [
                  r.title,
                  (r as MusicLookupResult).artistName,
                  (r as GameLookupResult).developer,
                  (r as GameLookupResult).publisher,
                  (r as GameLookupResult).platform,
                  r.description,
                  String(r.year),
                ].filter(Boolean).join(' ').toLowerCase();
                return haystack.includes(filterText.trim().toLowerCase());
              })
            : items;

          return (
            <div className="space-y-3">
              <p className="text-sm text-white/70 text-center">
                {filtered.length} of {items.length} match{items.length > 1 ? 'es' : ''}
                {filterText.trim() ? ' (filtered)' : ''}
              </p>
              <input
                type="text"
                value={filterText}
                onChange={e => setFilterText(e.target.value)}
                placeholder="Filter results…"
                className="w-full rounded-md border border-border bg-input-bg px-3 py-2 text-sm text-text-primary placeholder:text-text-tertiary outline-none focus:border-primary"
              />
              <div className="rounded-md bg-input-bg border border-border max-h-80 overflow-auto">
                {filtered.length === 0 ? (
                  <div className="px-3 py-4 text-sm text-text-secondary text-center">
                    No matches for "{filterText}"
                  </div>
                ) : (
                  filtered.map((item, i) => {
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
                  })
                )}
              </div>
            </div>
          );
        })()}

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
  item: MediaResultMap[T],
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
