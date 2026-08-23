import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import { filtersToParams, type Filters } from './filters';
import type {
  Album,
  CollectionStatus,
  CompletionStatus,
  Condition,
  Game,
  MediaType,
  Movie,
  MusicFormat,
  WatchStatus,
} from './types';

type ItemMap = { movies: Movie; music: Album; games: Game };

/**
 * List with an optional filter object. `query` stays the free-text
 * search; the filter object gets serialised to the per-endpoint query
 * params via {@link filtersToParams}. The query key includes both so
 * TanStack Query caches by the full (query + filters) pair and a
 * filter change refetches deterministically.
 */
export function useList<T extends MediaType>(type: T, query: string, filters?: Filters<T>) {
  return useQuery({
    queryKey: [type, 'list', query, filters ?? {}],
    queryFn: () => {
      const params = filters ? filtersToParams(filters as Record<string, unknown>) : new URLSearchParams();
      if (query) params.set('query', query);
      const qs = params.toString();
      return api<ItemMap[T][]>(`/api/${type}${qs ? `?${qs}` : ''}`);
    },
  });
}

export interface DashboardCounts {
  movies: number;
  music: number;
  games: number;
}

export interface DashboardRecent {
  type: MediaType;
  id: number;
  title: string;
  year: number | null;
  imagePath: string | null;
  addedAt: string;
}

export interface DashboardSummary {
  counts: DashboardCounts;
  recent: DashboardRecent[];
}

/**
 * Single-shot dashboard payload (per-type counts + the most recent
 * additions across all three types). Replaces the old "fetch every
 * list" approach so the home page renders without dragging the entire
 * collection over the wire.
 */
export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: () => api<DashboardSummary>('/api/dashboard'),
  });
}

export function useItem<T extends MediaType>(type: T, id: number | undefined) {
  return useQuery({
    queryKey: [type, 'item', id],
    queryFn: () => api<ItemMap[T]>(`/api/${type}/${id}`),
    enabled: id != null,
  });
}

export function useCreate<T extends MediaType>(type: T) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (item: ItemMap[T]) =>
      api<ItemMap[T]>(`/api/${type}`, { method: 'POST', body: JSON.stringify(item) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: [type] }),
  });
}

export function useUpdate<T extends MediaType>(type: T) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (item: ItemMap[T] & { id: number }) =>
      api<ItemMap[T]>(`/api/${type}/${item.id}`, { method: 'PUT', body: JSON.stringify(item) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: [type] }),
  });
}

export function useDelete<T extends MediaType>(type: T) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api(`/api/${type}/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: [type] }),
  });
}

/** Fields the bulk-update endpoint accepts per media type (a partial map).
 * Only keys present are set on every selected row; absent keys are untouched.
 * The union reflects the shared collection fields plus the per-type extras;
 * the server rejects any key a given type does not support. */
export interface BulkUpdates {
  status?: CollectionStatus;
  condition?: Condition | null;
  personalRating?: number | null;
  acquiredOn?: string | null;
  acquisitionPrice?: number | null;
  acquisitionCurrency?: string | null;
  acquisitionSource?: string | null;
  description?: string | null;
  notes?: string | null;
  tags?: string[];
  // movies
  watchStatus?: WatchStatus;
  watchCount?: number;
  lastWatchedOn?: string | null;
  // music
  format?: MusicFormat;
  listenCount?: number;
  // games
  completionStatus?: CompletionStatus;
  hoursPlayed?: number | null;
  // games + music
  lastPlayedOn?: string | null;
}

/** Set the SAME fields to the SAME values across many selected ids. The
 * payload is `{ ids, updates }` where `updates` is a partial map. Invalidates
 * the type's list query cache so the grid/table refetches. */
export function useBulkUpdate<T extends MediaType>(type: T) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ ids, updates }: { ids: number[]; updates: BulkUpdates }) =>
      api(`/api/${type}/bulk`, { method: 'PATCH', body: JSON.stringify({ ids, updates }) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: [type] }),
  });
}
