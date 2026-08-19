import { useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import type {
  CollectionStatus,
  CompletionStatus,
  DigitalStore,
  GamePlatform,
  MediaType,
  MovieFormat,
  MusicFormat,
  WatchStatus,
} from './types';

// Per-type filter shapes mirror the query params the server endpoints
// accept. Keep them flat and JSON-serialisable so the URL <->
// state round-trip stays trivial.

export interface MovieFilters {
  yearFrom?: number;
  yearTo?: number;
  director?: string;
  studio?: string;
  genre?: string;
  format?: MovieFormat;
  status?: CollectionStatus;
  watchStatus?: WatchStatus;
  ratingMin?: number;
  tag?: string[];
}

export interface AlbumFilters {
  yearFrom?: number;
  yearTo?: number;
  artist?: string;
  label?: string;
  genre?: string;
  format?: MusicFormat;
  status?: CollectionStatus;
  ratingMin?: number;
  tag?: string[];
}

export interface GameFilters {
  yearFrom?: number;
  yearTo?: number;
  publisher?: string;
  developer?: string;
  platform?: GamePlatform;
  digital?: boolean;
  digitalStore?: DigitalStore;
  status?: CollectionStatus;
  completionStatus?: CompletionStatus;
  ratingMin?: number;
  tag?: string[];
}

export type FiltersMap = {
  movies: MovieFilters;
  music: AlbumFilters;
  games: GameFilters;
};

export type Filters<T extends MediaType> = FiltersMap[T];

/**
 * Serialize a filters object into URLSearchParams. Arrays get one
 * entry per value (so `tag=a&tag=b`) which matches the binding shape
 * on the server. Booleans become "true"/"false"; undefined / null /
 * empty-string / NaN / empty-array values are dropped.
 */
export function filtersToParams(filters: Record<string, unknown>): URLSearchParams {
  const out = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (value === undefined || value === null || value === '') continue;
    if (Array.isArray(value)) {
      for (const v of value) {
        if (v === undefined || v === null || v === '') continue;
        out.append(key, String(v));
      }
      continue;
    }
    if (typeof value === 'number' && Number.isNaN(value)) continue;
    out.append(key, String(value));
  }
  return out;
}

/**
 * Inverse of {@link filtersToParams}. Pulls a filters object out of
 * a URLSearchParams. The per-key shape table is the source of truth
 * for how each query param is typed; values for keys not listed are
 * preserved as strings.
 */
function paramsToFilters<T extends MediaType>(type: T, params: URLSearchParams): Filters<T> {
  const result: Record<string, unknown> = {};
  const schema = SCHEMA[type];
  for (const [key, def] of Object.entries(schema)) {
    if (def === 'string[]') {
      const values = params.getAll(key).filter((v) => v.length > 0);
      if (values.length > 0) result[key] = values;
    } else if (def === 'number') {
      const raw = params.get(key);
      if (raw !== null && raw.length > 0) {
        const n = Number(raw);
        if (Number.isFinite(n)) result[key] = n;
      }
    } else if (def === 'boolean') {
      const raw = params.get(key);
      if (raw === 'true') result[key] = true;
      else if (raw === 'false') result[key] = false;
    } else {
      const raw = params.get(key);
      if (raw !== null && raw.length > 0) result[key] = raw;
    }
  }
  return result as Filters<T>;
}

type ParamShape = 'string' | 'number' | 'boolean' | 'string[]';

// Keys not listed default to `string`, but every filter key here is
// explicitly typed so the per-type round-trip stays predictable.
const SCHEMA: Record<MediaType, Record<string, ParamShape>> = {
  movies: {
    yearFrom: 'number', yearTo: 'number',
    director: 'string', studio: 'string', genre: 'string',
    format: 'string', status: 'string', watchStatus: 'string',
    ratingMin: 'number',
    tag: 'string[]',
  },
  music: {
    yearFrom: 'number', yearTo: 'number',
    artist: 'string', label: 'string', genre: 'string',
    format: 'string', status: 'string',
    ratingMin: 'number',
    tag: 'string[]',
  },
  games: {
    yearFrom: 'number', yearTo: 'number',
    publisher: 'string', developer: 'string',
    platform: 'string', digital: 'boolean', digitalStore: 'string',
    status: 'string', completionStatus: 'string',
    ratingMin: 'number',
    tag: 'string[]',
  },
};

/**
 * URL-synced filter state. Reading filters reflects the current
 * search-string; the returned setter writes them back via
 * react-router's navigation, so deep-linking and browser back/forward
 * work for free. Free-text search is intentionally **not** part of the
 * filter set -- the list page's search input owns its own state and
 * stays in the URL via a separate `q` param.
 */
export function useFiltersState<T extends MediaType>(type: T) {
  const [searchParams, setSearchParams] = useSearchParams();
  const filters = paramsToFilters(type, searchParams);

  const setFilters = useCallback(
    (next: Filters<T>) => {
      // Preserve any non-filter params we don't manage (e.g. `q`) so
      // the search input doesn't get clobbered on every filter change.
      const merged = new URLSearchParams();
      const ownKeys = new Set(Object.keys(SCHEMA[type]));
      for (const [k, v] of searchParams.entries()) {
        if (!ownKeys.has(k)) merged.append(k, v);
      }
      for (const [k, v] of filtersToParams(next as unknown as Record<string, unknown>).entries()) {
        merged.append(k, v);
      }
      setSearchParams(merged, { replace: true });
    },
    [type, searchParams, setSearchParams],
  );

  const clear = useCallback(() => {
    const merged = new URLSearchParams();
    const ownKeys = new Set(Object.keys(SCHEMA[type]));
    for (const [k, v] of searchParams.entries()) {
      if (!ownKeys.has(k)) merged.append(k, v);
    }
    setSearchParams(merged, { replace: true });
  }, [type, searchParams, setSearchParams]);

  return { filters, setFilters, clear };
}

/** Number of active (non-default) filter fields on the given object. */
export function activeFilterCount(filters: Record<string, unknown>): number {
  let n = filters.yearFrom != null || filters.yearTo != null ? 1 : 0;
  for (const [key, v] of Object.entries(filters)) {
    if (key === 'yearFrom' || key === 'yearTo') continue;
    if (v === undefined || v === null || v === '') continue;
    if (Array.isArray(v) && v.length === 0) continue;
    n++;
  }
  return n;
}
