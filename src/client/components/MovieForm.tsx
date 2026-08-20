import { useEffect, useState } from 'react';
import { Button, CoverPreview, ExternalIdField, Field, Input, SectionHeading, Select, Textarea } from './ui';
import CoverEditor from './CoverEditor';
import CoverFormLayout from './CoverFormLayout';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import BarcodeLookup from './BarcodeLookup';
import PhotoLookup from './PhotoLookup';
import { MOVIE_FORMAT_FLAGS, WATCH_STATUSES, type Movie, type WatchStatus } from '../services/types';
import { lookupMovieById, lookupMovieByImdbId, type MovieLookupResult } from '../services/lookup';
import { useLookupProtocol } from '../hooks/useLookupProtocol';

interface Props {
  initial?: Movie;
  /**
   * Lookup result to seed the form with on first mount (e.g. when the
   * user scanned a barcode on the list page and was redirected here).
   * Runs the same import + enrichment chain as picking from in-form
   * search, so missing fields like director / runtime fill in once the
   * follow-up call lands.
   */
  prefillLookup?: MovieLookupResult;
  /**
   * Soft-fallback prefill: just the barcode, no metadata. Set when the
   * list-page scanner couldn't resolve the UPC -- the user can finish
   * via title search without retyping it.
   */
  prefillBarcode?: string;
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

export default function MovieForm({ initial, prefillLookup, prefillBarcode, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [m, setM] = useState<Movie>(initial ?? empty);
  const [coverEditorExpanded, setCoverEditorExpanded] = useState(!m.imagePath);
  useEffect(() => { if (initial) setM(initial); }, [initial]);

  const set = <K extends keyof Movie>(k: K, v: Movie[K]) => setM((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Movie>) => setM((prev) => ({ ...prev, ...p }));
  const toggleFormat = (flag: number) => set('formats', (m.formats ?? 0) ^ flag);

  const protocolConfig = (lookup: typeof lookupMovieById, label: string) => ({
    getDraft: () => m, patchDraft: patch,
    importFields: (_draft: Movie, r: MovieLookupResult) => ({ title: r.title, originalTitle: r.originalTitle ?? null, year: r.year ?? null, director: r.director ?? null, runtimeMinutes: r.runtimeMinutes ?? null, description: r.description ?? null, imagePath: r.imageUrl ?? null }),
    providerNames: ['tmdb'] as const, linkageKey: (draft: Movie) => draft.tmdbId ?? null,
    setLinkageKey: (draft: Movie, value: string) => ({ ...draft, tmdbId: value }),
    enrich: { keyOf: (draft: Movie) => draft.tmdbId ?? null, run: lookupMovieById,
      fill: (draft: Movie, r: MovieLookupResult) => ({ ...draft, director: draft.director ?? r.director ?? null, runtimeMinutes: draft.runtimeMinutes ?? r.runtimeMinutes ?? null }),
      shouldRun: (r: MovieLookupResult) => r.provider === 'tmdb' && (r.director == null || r.runtimeMinutes == null),
      loadingLabel: 'Loading director and runtime…', successLabel: 'Populated from TMDB.', notConfiguredLabel: 'TMDB lookup not configured. Set the provider key.' },
    byId: { label, entityNoun: 'movie', notConfiguredHint: 'TMDB lookup not configured. Set the provider key.', lookup },
  });
  const { importLookup, runById, prefillEffect, fetchState } = useLookupProtocol<'movies', Movie, MovieLookupResult>(protocolConfig(lookupMovieById, 'TMDB ID'));
  const imdbProtocol = useLookupProtocol<'movies', Movie, MovieLookupResult>(protocolConfig(lookupMovieByImdbId, 'IMDB ID'));
  const displayedFetchState = imdbProtocol.fetchState.status === 'loading' || imdbProtocol.fetchState.message
    ? imdbProtocol.fetchState : fetchState;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => prefillEffect(prefillLookup, prefillBarcode), []);
  const fetchByTmdbId = () => runById(m.tmdbId ?? '');
  const fetchByImdbId = () => imdbProtocol.runById(m.imdbId ?? '');

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

      <BarcodeLookup
        type="movies"
        onPick={importLookup}
        onBarcodeFallback={(code) => set('barcode', code)}
        fallbackLabel="Save this barcode anyway"
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <PhotoLookup
        type="movies"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <CoverFormLayout
        fields={(
          <>
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
          </>
        )}
        preview={(
          <CoverPreview
            src={m.imagePath}
            alt={m.title ? `${m.title} poster` : ''}
          />
        )}
        editor={<CoverEditor value={m.imagePath} onChange={(v) => set('imagePath', v)} expanded={coverEditorExpanded} onExpandedChange={setCoverEditorExpanded} />}
        editorExpanded={coverEditorExpanded}
      />

      <div>
        <div className="text-xs font-medium text-text-secondary mb-2">Formats owned</div>
        <div className="flex flex-wrap gap-2">
          {MOVIE_FORMAT_FLAGS.map((f) => {
            const checked = ((m.formats ?? 0) & f.value) !== 0;
            return (
              <button
                type="button"
                key={f.key}
                onClick={() => toggleFormat(f.value)}
                className={`inline-flex min-h-[44px] items-center rounded-xl border px-3 text-sm font-semibold transition-colors ${checked ? 'category-active' : 'border-border bg-input-bg text-text-primary category-hover-soft'}`}
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
          <div>
            <Button
              type="button"
              variant="secondary"
              onClick={fetchByTmdbId}
              disabled={displayedFetchState.status === 'loading' || !(m.tmdbId ?? '').trim()}
              aria-label="Fetch metadata by TMDB ID"
            >
              {displayedFetchState.status === 'loading' ? 'Fetching…' : 'Fetch metadata'}
            </Button>
          </div>
        </div>
        <div className="space-y-1">
          <ExternalIdField
            label="IMDB ID"
            value={m.imdbId}
            onChange={(v) => set('imdbId', v)}
            urlPrefix="https://www.imdb.com/title/"
            placeholder="e.g. tt1375666"
          />
          <div>
            <Button
              type="button"
              variant="secondary"
              onClick={fetchByImdbId}
              disabled={displayedFetchState.status === 'loading' || !(m.imdbId ?? '').trim()}
              aria-label="Fetch metadata by IMDB ID"
            >
              {displayedFetchState.status === 'loading' ? 'Fetching…' : 'Fetch metadata'}
            </Button>
          </div>
        </div>
      </div>
      {displayedFetchState.message && (
        <div className="text-xs text-text-secondary">{displayedFetchState.message}</div>
      )}

      <Field label="Notes">
        <Textarea rows={3} value={m.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
      </Field>

      <div className="flex items-center justify-between">
        <Button type="submit" disabled={submitting} className="bg-movies text-[#071333] hover:bg-movies/85">
          {submitLabel}
        </Button>
        {onDelete && (
          <Button type="button" variant="danger" onClick={onDelete}>Delete</Button>
        )}
      </div>
    </form>
  );
}
