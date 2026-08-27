import { useCallback, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { MediaType } from './types';

export type SortDirection = 'asc' | 'desc';

export type SharedSortField = 'title' | 'year' | 'addedAt' | 'personalRating';

/** Per-type ?sort= keys: the shared fields plus each type's own extras.
 * Wire values match the server's exact keys. */
export interface SortFieldMap {
  movies: SharedSortField | 'watchStatus' | 'watchCount';
  music: SharedSortField | 'listenCount';
  games: SharedSortField | 'hoursPlayed' | 'completionStatus';
}

export type SortField<T extends MediaType> = SortFieldMap[T];

export interface SortState<T extends MediaType> {
  field: SortField<T>;
  direction: SortDirection;
}

export const DEFAULT_SORT_FIELD: SharedSortField = 'addedAt';
export const DEFAULT_SORT_DIRECTION: SortDirection = 'desc';

export interface SortOption {
  value: string;
  label: string;
}

const SHARED_SORT_OPTIONS: SortOption[] = [
  { value: 'title', label: 'Title' },
  { value: 'year', label: 'Year' },
  { value: 'addedAt', label: 'Date added' },
  { value: 'personalRating', label: 'Rating' },
];

const TYPE_SORT_OPTIONS: Record<MediaType, SortOption[]> = {
  movies: [
    { value: 'watchStatus', label: 'Watch status' },
    { value: 'watchCount', label: 'Watch count' },
  ],
  music: [
    { value: 'listenCount', label: 'Listen count' },
  ],
  games: [
    { value: 'hoursPlayed', label: 'Hours played' },
    { value: 'completionStatus', label: 'Completion status' },
  ],
};

export const DIRECTION_OPTIONS: { value: SortDirection; label: string }[] = [
  { value: 'asc', label: 'Ascending' },
  { value: 'desc', label: 'Descending' },
];

/** Sort options for a media type: the four shared fields plus that type's own
 * extras only (never another type's extras). */
export function sortOptions<T extends MediaType>(type: T): SortOption[] {
  return [...SHARED_SORT_OPTIONS, ...TYPE_SORT_OPTIONS[type]];
}

function isValidField<T extends MediaType>(type: T, value: string): value is SortField<T> {
  return sortOptions(type).some((o) => o.value === value);
}

export interface ReadSortStateResult<T extends MediaType> {
  state: SortState<T>;
  /** True when the URL held a present-but-invalid sort/dir value (unknown,
   * empty, or repeated) and must be canonicalized with a single replace
   * navigation. Absent values resolve to the default WITHOUT this flag. */
  needsReplace: boolean;
}

/**
 * Reads sort state out of a URLSearchParams, mirroring the server's
 * unknown/empty/repeated-value contract client-side. Absent `sort`/`dir`
 * silently resolve to addedAt/desc; a present-but-invalid value also
 * resolves to the default but reports `needsReplace` so the caller can
 * canonicalize the URL exactly once.
 */
export function readSortState<T extends MediaType>(type: T, params: URLSearchParams): ReadSortStateResult<T> {
  const sortValues = params.getAll('sort');
  const dirValues = params.getAll('dir');

  let field: SortField<T> = DEFAULT_SORT_FIELD as SortField<T>;
  let needsReplace = false;

  if (sortValues.length === 1 && sortValues[0].length > 0 && isValidField(type, sortValues[0])) {
    field = sortValues[0] as SortField<T>;
  } else if (sortValues.length > 0) {
    needsReplace = true;
  }

  let direction: SortDirection = DEFAULT_SORT_DIRECTION;
  if (dirValues.length === 1 && (dirValues[0] === 'asc' || dirValues[0] === 'desc')) {
    direction = dirValues[0];
  } else if (dirValues.length > 0) {
    needsReplace = true;
  }

  return { state: { field, direction }, needsReplace };
}

/** Writes sort state into a copy of `current`, preserving every other
 * (filter, query, or unrelated) param untouched. */
export function writeSortState<T extends MediaType>(
  current: URLSearchParams,
  state: SortState<T>,
): URLSearchParams {
  const next = new URLSearchParams(current);
  next.set('sort', state.field);
  next.set('dir', state.direction);
  return next;
}

/** Canonical `sort`/`dir` request params. The caller appends these after
 * existing filter/query params are serialized. */
export function serializeSortParams<T extends MediaType>(state: SortState<T>): URLSearchParams {
  const params = new URLSearchParams();
  params.set('sort', state.field);
  params.set('dir', state.direction);
  return params;
}

/**
 * URL-synced sort state, mirroring {@link ../services/filters.useFiltersState}.
 * A malformed URL is canonicalized with a single `replace` navigation; once
 * the canonical URL is installed, `readSortState` no longer reports
 * `needsReplace` and the effect goes inert.
 */
export function useSortState<T extends MediaType>(type: T) {
  const [searchParams, setSearchParams] = useSearchParams();
  const { state, needsReplace } = readSortState(type, searchParams);

  useEffect(() => {
    if (needsReplace) {
      setSearchParams(writeSortState(searchParams, state), { replace: true });
    }
    // Re-run only when the canonicalization need itself changes; `state` at
    // that moment is already captured via `needsReplace`'s computation above.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [needsReplace]);

  const setSortState = useCallback(
    (next: SortState<T>) => {
      setSearchParams(writeSortState(searchParams, next), { replace: true });
    },
    [searchParams, setSearchParams],
  );

  return { state, setSortState };
}
