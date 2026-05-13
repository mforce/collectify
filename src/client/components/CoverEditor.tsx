import { useRef, useState } from 'react';
import { useToast } from './toaster';
import { Button, Input } from './ui';

interface Props {
  value: string | null | undefined;
  onChange: (next: string | null) => void;
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
export default function CoverEditor({ value, onChange }: Props) {
  const [open, setOpen] = useState(!value);
  const [url, setUrl] = useState('');
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const toast = useToast();

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
      <div className="text-xs flex items-center gap-3">
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="text-indigo-300 hover:text-indigo-200 underline"
        >
          Change cover
        </button>
        <button
          type="button"
          onClick={remove}
          className="text-rose-300 hover:text-rose-200 underline"
        >
          Remove cover
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-2 rounded-md border border-slate-800 bg-slate-900/60 p-3">
      <div className="flex flex-col sm:flex-row gap-2 sm:items-end">
        <div className="flex-1">
          <label className="block text-xs font-medium text-slate-400 mb-1">Image URL</label>
          <Input
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
        <Button type="button" variant="secondary" onClick={applyUrl} disabled={!url.trim()}>
          Apply URL
        </Button>
      </div>

      <div className="flex flex-col sm:flex-row gap-2 sm:items-center">
        <label htmlFor="cover-editor-file" className="block text-xs font-medium text-slate-400">
          Or upload a file
        </label>
        <input
          ref={fileRef}
          id="cover-editor-file"
          type="file"
          accept={ALLOWED_MIME.join(',')}
          onChange={onFileChange}
          disabled={uploading}
          className="text-sm text-slate-300 file:mr-2 file:rounded-md file:border-0 file:bg-slate-700 file:px-3 file:py-1.5 file:text-sm file:text-slate-100 hover:file:bg-slate-600"
        />
        {uploading && <span className="text-xs text-slate-400">Uploading…</span>}
      </div>

      {error && (
        <p role="alert" className="text-sm text-rose-300">
          {error}
        </p>
      )}

      <div className="flex items-center justify-between">
        {value ? (
          <button
            type="button"
            onClick={remove}
            className="text-xs text-rose-300 hover:text-rose-200 underline"
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
            className="text-xs text-slate-400 hover:text-slate-200 underline"
          >
            Cancel
          </button>
        )}
      </div>
    </div>
  );
}
