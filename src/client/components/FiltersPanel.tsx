import { useState } from 'react';
import {
  Button,
  Card,
  Field,
  Input,
  SearchableSelect,
  Select,
  TagChip,
  TagInput,
} from './ui';
import { activeFilterCount, type Filters } from '../services/filters';
import {
  COLLECTION_STATUSES,
  COMPLETION_STATUSES,
  DIGITAL_STORES,
  digitalStoreLabel,
  GAME_PLATFORMS,
  MOVIE_FORMAT_FLAGS,
  MUSIC_FORMATS,
  WATCH_STATUSES,
  type DigitalStore,
  type MediaType,
} from '../services/types';

interface Props<T extends MediaType> {
  type: T;
  value: Filters<T>;
  onChange: (next: Filters<T>) => void;
  onClear: () => void;
}

/**
 * Per-type collapsible filter panel. Opens to a grid of fields
 * appropriate for the media type; renders a chip strip of currently
 * active filters whether or not the body is open. The empty-state
 * (no active filters) hides the chip strip but leaves the disclosure
 * trigger so the user can still expand the panel.
 */
export default function FiltersPanel<T extends MediaType>({ type, value, onChange, onClear }: Props<T>) {
  const [open, setOpen] = useState(false);
  const active = activeFilterCount(value as unknown as Record<string, unknown>);

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
          className="text-sm text-text-primary hover:text-text-primary inline-flex items-center gap-2"
        >
          <span>{open ? '▾' : '▸'} Filters{active > 0 && ` (${active})`}</span>
        </button>
        {active > 0 && (
          <button
            type="button"
            onClick={onClear}
            className="text-xs text-text-secondary hover:text-error"
          >
            Clear all
          </button>
        )}
      </div>

      {active > 0 && (
        <ActiveChips type={type} value={value} onChange={onChange} />
      )}

      {open && (
        <Card>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {type === 'movies' && <MovieFields value={value as Filters<'movies'>} onChange={onChange as (v: Filters<'movies'>) => void} />}
            {type === 'music' && <AlbumFields value={value as Filters<'music'>} onChange={onChange as (v: Filters<'music'>) => void} />}
            {type === 'games' && <GameFields value={value as Filters<'games'>} onChange={onChange as (v: Filters<'games'>) => void} />}
            <Field label="Tags">
              <TagInput
                value={value.tag ?? []}
                onChange={(tag) => onChange({ ...value, tag } as Filters<T>)}
                category={type}
              />
            </Field>
          </div>
        </Card>
      )}
    </div>
  );
}

// ---------- Per-type field renderers ----------

function MovieFields({ value, onChange }: { value: Filters<'movies'>; onChange: (next: Filters<'movies'>) => void }) {
  const set = <K extends keyof Filters<'movies'>>(k: K, v: Filters<'movies'>[K]) =>
    onChange({ ...value, [k]: v });
  return (
    <>
      <YearRange from={value.yearFrom} to={value.yearTo} onChange={(yf, yt) => onChange({ ...value, yearFrom: yf, yearTo: yt })} />
      <Field label="Director">
        <Input value={value.director ?? ''} onChange={(e) => set('director', e.target.value || undefined)} />
      </Field>
      <Field label="Studio">
        <Input value={value.studio ?? ''} onChange={(e) => set('studio', e.target.value || undefined)} />
      </Field>
      <Field label="Genre">
        <Input value={value.genre ?? ''} onChange={(e) => set('genre', e.target.value || undefined)} placeholder="substring match" />
      </Field>
      <Field label="Format">
        <Select value={value.format ?? ''} onChange={(e) => set('format', (e.target.value || undefined) as Filters<'movies'>['format'])}>
          <option value="">Any</option>
          {MOVIE_FORMAT_FLAGS.map((f) => (
            <option key={f.key} value={f.key}>{f.label}</option>
          ))}
        </Select>
      </Field>
      <Field label="Status">
        <Select value={value.status ?? ''} onChange={(e) => set('status', (e.target.value || undefined) as Filters<'movies'>['status'])}>
          <option value="">Any</option>
          {COLLECTION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </Select>
      </Field>
      <Field label="Watch status">
        <Select value={value.watchStatus ?? ''} onChange={(e) => set('watchStatus', (e.target.value || undefined) as Filters<'movies'>['watchStatus'])}>
          <option value="">Any</option>
          {WATCH_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </Select>
      </Field>
      <RatingMin value={value.ratingMin} onChange={(v) => set('ratingMin', v)} />
    </>
  );
}

function AlbumFields({ value, onChange }: { value: Filters<'music'>; onChange: (next: Filters<'music'>) => void }) {
  const set = <K extends keyof Filters<'music'>>(k: K, v: Filters<'music'>[K]) =>
    onChange({ ...value, [k]: v });
  return (
    <>
      <YearRange from={value.yearFrom} to={value.yearTo} onChange={(yf, yt) => onChange({ ...value, yearFrom: yf, yearTo: yt })} />
      <Field label="Artist">
        <Input value={value.artist ?? ''} onChange={(e) => set('artist', e.target.value || undefined)} />
      </Field>
      <Field label="Label">
        <Input value={value.label ?? ''} onChange={(e) => set('label', e.target.value || undefined)} />
      </Field>
      <Field label="Genre">
        <Input value={value.genre ?? ''} onChange={(e) => set('genre', e.target.value || undefined)} placeholder="substring match" />
      </Field>
      <Field label="Format">
        <Select value={value.format ?? ''} onChange={(e) => set('format', (e.target.value || undefined) as Filters<'music'>['format'])}>
          <option value="">Any</option>
          {MUSIC_FORMATS.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
        </Select>
      </Field>
      <Field label="Status">
        <Select value={value.status ?? ''} onChange={(e) => set('status', (e.target.value || undefined) as Filters<'music'>['status'])}>
          <option value="">Any</option>
          {COLLECTION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </Select>
      </Field>
      <RatingMin value={value.ratingMin} onChange={(v) => set('ratingMin', v)} />
    </>
  );
}

function GameFields({ value, onChange }: { value: Filters<'games'>; onChange: (next: Filters<'games'>) => void }) {
  const set = <K extends keyof Filters<'games'>>(k: K, v: Filters<'games'>[K]) =>
    onChange({ ...value, [k]: v });
  return (
    <>
      <YearRange from={value.yearFrom} to={value.yearTo} onChange={(yf, yt) => onChange({ ...value, yearFrom: yf, yearTo: yt })} />
      <Field label="Platform">
        <SearchableSelect
          value={value.platform ?? ''}
          onChange={(v) => set('platform', (v || undefined) as Filters<'games'>['platform'])}
          options={[{ value: '', label: 'Any' }, ...GAME_PLATFORMS]}
          placeholder="Any"
        />
      </Field>
      <Field label="Publisher">
        <Input value={value.publisher ?? ''} onChange={(e) => set('publisher', e.target.value || undefined)} />
      </Field>
      <Field label="Developer">
        <Input value={value.developer ?? ''} onChange={(e) => set('developer', e.target.value || undefined)} />
      </Field>
      <Field label="Status">
        <Select value={value.status ?? ''} onChange={(e) => set('status', (e.target.value || undefined) as Filters<'games'>['status'])}>
          <option value="">Any</option>
          {COLLECTION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </Select>
      </Field>
      <Field label="Completion">
        <Select value={value.completionStatus ?? ''} onChange={(e) => set('completionStatus', (e.target.value || undefined) as Filters<'games'>['completionStatus'])}>
          <option value="">Any</option>
          {COMPLETION_STATUSES.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
        </Select>
      </Field>
      <Field label="Physical / Digital">
        <Select
          value={value.digital === undefined ? '' : value.digital ? 'true' : 'false'}
          onChange={(e) =>
            set('digital', e.target.value === '' ? undefined : e.target.value === 'true')
          }
        >
          <option value="">Any</option>
          <option value="true">Digital</option>
          <option value="false">Physical</option>
        </Select>
      </Field>
      <Field label="Digital store">
        <Select value={value.digitalStore ?? ''} onChange={(e) => set('digitalStore', (e.target.value || undefined) as Filters<'games'>['digitalStore'])}>
          <option value="">Any</option>
          {DIGITAL_STORES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </Select>
      </Field>
      <RatingMin value={value.ratingMin} onChange={(v) => set('ratingMin', v)} />
    </>
  );
}

// ---------- Shared little controls ----------

function YearRange({
  from,
  to,
  onChange,
}: {
  from: number | undefined;
  to: number | undefined;
  onChange: (f: number | undefined, t: number | undefined) => void;
}) {
  return (
    <Field label="Year range">
      <div className="flex flex-col sm:flex-row gap-2 sm:items-center">
        <Input
          type="number"
          inputMode="numeric"
          placeholder="from"
          value={from ?? ''}
          onChange={(e) => onChange(e.target.value ? Number(e.target.value) : undefined, to)}
        />
        <span className="text-text-tertiary text-xs">–</span>
        <Input
          type="number"
          inputMode="numeric"
          placeholder="to"
          value={to ?? ''}
          onChange={(e) => onChange(from, e.target.value ? Number(e.target.value) : undefined)}
        />
      </div>
    </Field>
  );
}

function RatingMin({ value, onChange }: { value: number | undefined; onChange: (v: number | undefined) => void }) {
  return (
    <Field label="Min rating">
      <Select value={value ?? ''} onChange={(e) => onChange(e.target.value ? Number(e.target.value) : undefined)}>
        <option value="">Any</option>
        {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => (
          <option key={n} value={n}>{n}+</option>
        ))}
      </Select>
    </Field>
  );
}

// ---------- Active filter chips ----------

interface ChipProps<T extends MediaType> {
  type: T;
  value: Filters<T>;
  onChange: (next: Filters<T>) => void;
}

function ActiveChips<T extends MediaType>({ type, value, onChange }: ChipProps<T>) {
  const entries = describeActive(type, value);
  if (entries.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-1.5">
      {entries.map((e) => (
        <span
          key={e.key}
          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-card text-text-primary border border-border"
        >
          <span className="text-text-secondary mr-1">{e.label}:</span> {e.display}
          <button
            type="button"
            onClick={() => onChange(
              e.key === 'yearFrom'
                ? { ...value, yearFrom: undefined, yearTo: undefined } as Filters<T>
                : { ...value, [e.key]: undefined } as Filters<T>,
            )}
            aria-label={`Remove ${e.label} filter`}
            className="ml-0.5 text-text-secondary hover:text-error"
          >
            ×
          </button>
        </span>
      ))}
    </div>
  );
}

/**
 * Turn the filters object into a list of `{ key, label, display }`
 * entries for the chip strip. Year range is rendered as a single chip
 * when either bound is set (so removing it clears both).
 */
function describeActive<T extends MediaType>(_type: T, filters: Filters<T>): { key: string; label: string; display: string }[] {
  const out: { key: string; label: string; display: string }[] = [];
  const f = filters as Record<string, unknown>;
  if (f.yearFrom != null || f.yearTo != null) {
    out.push({
      key: 'yearFrom',
      label: 'Year',
      display: `${f.yearFrom ?? '…'}–${f.yearTo ?? '…'}`,
    });
  }
  const labels: Record<string, string> = {
    director: 'Director', studio: 'Studio', genre: 'Genre',
    artist: 'Artist', label: 'Label',
    publisher: 'Publisher', developer: 'Developer',
    format: 'Format', platform: 'Platform',
    status: 'Status', watchStatus: 'Watch',
    completionStatus: 'Completion', digital: 'Digital',
    digitalStore: 'Store', ratingMin: 'Min rating',
  };
  for (const [key, value] of Object.entries(f)) {
    if (key === 'yearFrom' || key === 'yearTo' || key === 'tag') continue;
    if (value === undefined || value === null || value === '') continue;
    const display = key === 'digitalStore'
      ? digitalStoreLabel(value as DigitalStore) ?? String(value)
      : typeof value === 'boolean'
        ? (value ? 'Digital' : 'Physical')
        : String(value);
    out.push({ key, label: labels[key] ?? key, display });
  }
  if (Array.isArray(f.tag) && f.tag.length > 0) {
    out.push({ key: 'tag', label: 'Tags', display: f.tag.join(', ') });
  }
  return out;
}

/** Tiny reuse so chip removal can share styling without indirection. */
export { TagChip };
