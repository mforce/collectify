import { useRef, useState } from 'react';
import { useToast } from './toaster';
import { Button, Input } from './ui';

interface Props {
  value: string | null | undefined;
  onChange: (next: string | null) => void;
  expanded?: boolean;
  onExpandedChange?: (expanded: boolean) => void;
}

const ALLOWED_MIME = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_BYTES = 5 * 1024 * 1024;

/**
 * Three-way cover editor: paste an external URL, upload bytes, or
 * remove the current cover. Renders below the existing CoverPreview
 * and is the only path that lets users *change* an entry's
 * imagePath after the lookup / scan flows set it.
 *
 * - URL paste: just writes the typed value into `imagePath`. The
 *   server's CoverImageStore.EnsureLocalAsync runs on save and
 *   downloads + caches it into the CoverImages table on the next
 *   round-trip.
 * - File upload: POSTs the bytes to /api/covers (multipart), gets
 *   back a /covers/{hash} path the SPA can embed immediately.
 * - Remove: clears imagePath; the GC sweep on the next restart drops
 *   the now-orphan cover row.
 *
 * Server-side validation (MIME whitelist, 5 MB cap, magic-byte
 * sniff) is mirrored here for inline error feedback so the user
 * doesn't have to wait on a round-trip to see "too big" / "not an
 * image".
 */
export default function CoverEditor({ value, onChange, expanded, onExpandedChange }: Props) {
  const [openState, setOpenState] = useState(!value);
  const open = expanded ?? openState;
  const [url, setUrl] = useState('');
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const toast = useToast();

  const setOpen = (next: boolean) => {
    setOpenState(next);
    onExpandedChange?.(next);
  };

  const applyUrl = () => {
    const trimmed = url.trim();
    if (!trimmed) return;
    onChange(trimmed);
    setUrl('');
    setOpen(false);
    setError(null);
  };

  const onFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setError(null);

    if (!ALLOWED_MIME.includes(file.type)) {
      setError(`Unsupported file type (${file.type || 'unknown'}). JPEG, PNG, or WebP only.`);
      e.target.value = '';
      return;
    }
    if (file.size > MAX_BYTES) {
      setError(`File is too large (${Math.round(file.size / 1024 / 1024)} MB). Max is 5 MB.`);
      e.target.value = '';
      return;
    }

    setUploading(true);
    try {
      const form = new FormData();
      form.append('file', file);
      const resp = await fetch('/api/covers', {
        method: 'POST',
        body: form,
        credentials: 'include',
      });
      if (resp.status === 413) {
        setError('File is too large. Max is 5 MB.');
        return;
      }
      if (resp.status === 415) {
        setError('Unsupported file type — JPEG, PNG, or WebP only.');
        return;
      }
      if (!resp.ok) {
        // Server bubbled up some other 4xx/5xx; surface its `error`
        // payload when present.
        let message = `Upload failed (${resp.status}).`;
        try {
          const body = await resp.json();
          if (body?.error) message = body.error;
        } catch {
          /* ignore */
        }
        setError(message);
        return;
      }
      const body = (await resp.json()) as { imagePath: string };
      onChange(body.imagePath);
      setOpen(false);
    } catch (err) {
      setError((err as Error).message ?? 'Upload failed.');
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const remove = () => {
    onChange(null);
    setOpen(true);
    setUrl('');
    setError(null);
    toast.success('Cover removed.');
  };

  // Collapsed disclosure when a cover already exists -- avoids
  // dragging two stacked inputs under the cover preview on the
  // happy path. Click "Change cover" to expand.
  if (!open) {
    return (
      <div data-testid="cover-collapsed-actions" className="flex flex-col gap-2 text-xs">
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex min-h-[36px] items-center justify-center rounded-md border border-border bg-pill-bg px-3 py-1.5 font-semibold text-text-primary hover:bg-gray-100 dark:hover:bg-[#353840]"
        >
          Change cover
        </button>
        <button
          type="button"
          onClick={remove}
          className="inline-flex min-h-[36px] items-center justify-center rounded-md border border-error/50 bg-transparent px-3 py-1.5 font-semibold text-error hover:bg-error/10"
        >
          Remove cover
        </button>
      </div>
    );
  }

  return (
    <div data-testid="cover-editor-card" className="w-full min-w-0 space-y-2 rounded-md border border-border bg-card/80 p-3">
      <div data-testid="cover-url-row" className="flex flex-col flex-wrap gap-2 sm:flex-row sm:items-end">
        <div data-testid="cover-url-field" className="w-full min-w-0 flex-1 sm:basis-64">
          <label className="block text-xs font-medium text-text-secondary mb-1">Image URL</label>
          <Input
            className="min-w-0"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://…/cover.jpg"
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                applyUrl();
              }
            }}
          />
        </div>
        <Button type="button" variant="secondary" onClick={applyUrl} disabled={!url.trim()} className="shrink-0 whitespace-nowrap">
          Apply URL
        </Button>
      </div>

      <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center">
        <label htmlFor="cover-editor-file" className="block text-xs font-medium text-text-secondary shrink-0">
          Or upload a file
        </label>
        <input
          ref={fileRef}
          id="cover-editor-file"
          type="file"
          accept={ALLOWED_MIME.join(',')}
          onChange={onFileChange}
          disabled={uploading}
          className="max-w-full min-w-0 text-sm text-text-primary file:mr-2 file:rounded-md file:border-0 file:bg-input-bg file:px-3 file:py-1.5 file:text-sm file:text-text-primary hover:file:bg-gray-300"
        />
        {uploading && <span className="text-xs text-text-secondary">Uploading…</span>}
      </div>

      {error && (
        <p role="alert" className="text-sm text-error">
          {error}
        </p>
      )}

      <div data-testid="cover-editor-actions" className="flex flex-wrap items-center justify-between gap-2">
        {value ? (
          <button
            type="button"
            onClick={remove}
            className="text-xs text-error hover:text-error-hover underline"
          >
            Remove current cover
          </button>
        ) : (
          <span />
        )}
        {value && (
          <button
            type="button"
            onClick={() => setOpen(false)}
            className="text-xs text-text-secondary hover:text-text-primary underline"
          >
            Cancel
          </button>
        )}
      </div>
    </div>
  );
}
