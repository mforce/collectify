import { useEffect, useState } from 'react';
import { Button, ExternalIdField, Field, Input, SectionHeading, Select, Textarea } from './ui';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import { MOVIE_FORMAT_FLAGS, WATCH_STATUSES, type Movie, type WatchStatus } from '../services/types';
import { lookupMovieById, type MovieLookupResult } from '../services/lookup';

interface Props {
  initial?: Movie;
  submitting?: boolean;
  submitLabel?: string;
  onSubmit: (m: Movie) => void;
  onDelete?: () => void;
}

const empty: Movie = {
  title: '',
  formats: 0,
  status: 'Owned',
  watchStatus: 'Unwatched',
  watchCount: 0,
  tags: [],
};

export default function MovieForm({ initial, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [m, setM] = useState<Movie>(initial ?? empty);
  const [fetchState, setFetchState] = useState<{ status: 'idle' | 'loading'; message?: string }>({ status: 'idle' });
  useEffect(() => { if (initial) setM(initial); }, [initial]);

  const set = <K extends keyof Movie>(k: K, v: Movie[K]) => setM((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Movie>) => setM((prev) => ({ ...prev, ...p }));
  const toggleFormat = (flag: number) => set('formats', (m.formats ?? 0) ^ flag);

  const importLookup = (r: MovieLookupResult) => {
    patch({
      title: r.title,
      originalTitle: r.originalTitle ?? null,
      year: r.year ?? null,
      director: r.director ?? null,
      runtimeMinutes: r.runtimeMinutes ?? null,
      description: r.description ?? null,
      imagePath: r.imageUrl ?? null,
      tmdbId: r.provider === 'tmdb' ? r.providerKey : m.tmdbId ?? null,
    });
  };

  const fetchByTmdbId = async () => {
    const id = (m.tmdbId ?? '').trim();
    if (!id) {
      setFetchState({ status: 'idle', message: 'Enter a TMDB ID first.' });
      return;
    }
    setFetchState({ status: 'loading' });
    try {
      const outcome = await lookupMovieById(id);
      if (outcome.kind === 'found') {
        importLookup(outcome.result);
        setFetchState({ status: 'idle', message: 'Populated from TMDB.' });
      } else if (outcome.kind === 'not-configured') {
        setFetchState({ status: 'idle', message: 'TMDB lookup not configured. Set the provider key.' });
      } else {
        setFetchState({ status: 'idle', message: `No movie with TMDB ID ${id}.` });
      }
    } catch (err) {
      setFetchState({ status: 'idle', message: (err as Error).message ?? 'Lookup failed.' });
    }
  };

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ ...m, title: m.title.trim() });
      }}
    >
      <OnlineSearch
        type="movies"
        label="Search online (TMDB)"
        placeholder="e.g. Inception"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Title">
          <Input value={m.title} onChange={(e) => set('title', e.target.value)} required />
        </Field>
        <Field label="Original title">
          <Input value={m.originalTitle ?? ''} onChange={(e) => set('originalTitle', e.target.value || null)} />
        </Field>
        <Field label="Year">
          <Input type="number" value={m.year ?? ''} onChange={(e) => set('year', e.target.value ? Number(e.target.value) : null)} />
        </Field>
        <Field label="Director">
          <Input value={m.director ?? ''} onChange={(e) => set('director', e.target.value || null)} />
        </Field>
        <Field label="Runtime (min)">
          <Input type="number" value={m.runtimeMinutes ?? ''} onChange={(e) => set('runtimeMinutes', e.target.value ? Number(e.target.value) : null)} />
        </Field>
        <Field label="Studio">
          <Input value={m.studio ?? ''} onChange={(e) => set('studio', e.target.value || null)} />
        </Field>
        <Field label="Genres (comma separated)">
          <Input value={m.genres ?? ''} onChange={(e) => set('genres', e.target.value || null)} />
        </Field>
        <Field label="Barcode">
          <Input value={m.barcode ?? ''} onChange={(e) => set('barcode', e.target.value || null)} />
        </Field>
      </div>

      <div>
        <div className="text-xs font-medium text-slate-400 mb-2">Formats owned</div>
        <div className="flex flex-wrap gap-2">
          {MOVIE_FORMAT_FLAGS.map((f) => {
            const checked = ((m.formats ?? 0) & f.value) !== 0;
            return (
              <button
                type="button"
                key={f.key}
                onClick={() => toggleFormat(f.value)}
                className={`px-3 py-1.5 rounded-md text-sm border ${checked ? 'bg-indigo-500 border-indigo-400 text-white' : 'bg-slate-900 border-slate-700 text-slate-300'}`}
              >
                {f.label}
              </button>
            );
          })}
        </div>
      </div>

      <PersonalAcquisitionSection value={m} onChange={patch} />

      <SectionHeading>Watching</SectionHeading>
      <div className="grid sm:grid-cols-3 gap-4">
        <Field label="Watch status">
          <Select
            value={m.watchStatus}
            onChange={(e) => set('watchStatus', e.target.value as WatchStatus)}
          >
            {WATCH_STATUSES.map((w) => (
              <option key={w.value} value={w.value}>{w.label}</option>
            ))}
          </Select>
        </Field>
        <Field label="Last watched">
          <Input
            type="date"
            value={m.lastWatchedOn ?? ''}
            onChange={(e) => set('lastWatchedOn', e.target.value || null)}
          />
        </Field>
        <Field label="Watch count">
          <Input
            type="number"
            min="0"
            value={m.watchCount}
            onChange={(e) => set('watchCount', Number(e.target.value || 0))}
          />
        </Field>
      </div>

      <SectionHeading>External IDs</SectionHeading>
      <div className="grid sm:grid-cols-2 gap-4">
        <div className="space-y-1">
          <ExternalIdField
            label="TMDB ID"
            value={m.tmdbId}
            onChange={(v) => set('tmdbId', v)}
            urlPrefix="https://www.themoviedb.org/movie/"
            placeholder="e.g. 27205"
          />
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="secondary"
              onClick={fetchByTmdbId}
              disabled={fetchState.status === 'loading' || !(m.tmdbId ?? '').trim()}
            >
              {fetchState.status === 'loading' ? 'Fetching…' : 'Fetch metadata'}
            </Button>
            {fetchState.message && (
              <span className="text-xs text-slate-400">{fetchState.message}</span>
            )}
          </div>
        </div>
        <ExternalIdField
          label="IMDB ID"
          value={m.imdbId}
          onChange={(v) => set('imdbId', v)}
          urlPrefix="https://www.imdb.com/title/"
          placeholder="e.g. tt1375666"
        />
      </div>

      <Field label="Notes">
        <Textarea rows={3} value={m.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
      </Field>

      <div className="flex items-center justify-between">
        <Button type="submit" disabled={submitting}>{submitLabel}</Button>
        {onDelete && (
          <Button type="button" variant="danger" onClick={onDelete}>Delete</Button>
        )}
      </div>
    </form>
  );
}
