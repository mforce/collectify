import { useQuery } from '@tanstack/react-query';
import { api } from './client';
import type { MediaType } from './types';

export interface MovieLookupResult {
  provider: string;
  providerKey: string;
  title: string;
  originalTitle: string | null;
  year: number | null;
  director: string | null;
  runtimeMinutes: number | null;
  description: string | null;
  imageUrl: string | null;
  genres: string | null;
}

export interface MusicLookupResult {
  provider: string;
  providerKey: string;
  title: string;
  artistName: string;
  year: number | null;
  label: string | null;
  description: string | null;
  imageUrl: string | null;
  genres: string | null;
}

export interface GameLookupResult {
  provider: string;
  providerKey: string;
  title: string;
  platform: string | null;
  year: number | null;
  publisher: string | null;
  developer: string | null;
  description: string | null;
  imageUrl: string | null;
  genres: string | null;
}

export interface LookupResponse<T> {
  provider: string;
  configured: boolean;
  results: T[];
}

type ResultMap = {
  movies: MovieLookupResult;
  music: MusicLookupResult;
  games: GameLookupResult;
};

/**
 * Search the configured external metadata provider for a media type. Returns
 * an empty result set when no provider is configured for that type yet --
 * the response always includes `configured: false` so callers can hint the
 * UI ("Set TMDB__ApiKey to enable lookups") instead of treating an empty
 * list as "no matches".
 *
 * Disabled until the query has at least 2 non-whitespace characters; the
 * server enforces the same minimum.
 */
export function useLookup<T extends MediaType>(type: T, query: string) {
  const trimmed = query.trim();
  return useQuery({
    queryKey: ['lookup', type, trimmed],
    queryFn: () =>
      api<LookupResponse<ResultMap[T]>>(`/api/lookup/${type}?q=${encodeURIComponent(trimmed)}`),
    enabled: trimmed.length >= 2,
    staleTime: 60_000,
  });
}

export type LookupByIdOutcome =
  | { kind: 'found'; result: MovieLookupResult }
  | { kind: 'not-found' }
  | { kind: 'not-configured' };

/**
 * Direct lookup of a movie by its provider id (e.g. a TMDB id). Imperative
 * by design -- the user clicks a button to trigger a single fetch, no
 * debouncing or background revalidation needed. Returns a discriminated
 * union so the caller can render distinct UX for unconfigured vs unknown
 * id without sniffing array lengths.
 */
export async function lookupMovieById(providerKey: string): Promise<LookupByIdOutcome> {
  const trimmed = providerKey.trim();
  if (!trimmed) return { kind: 'not-found' };
  const response = await api<LookupResponse<MovieLookupResult>>(
    `/api/lookup/movies/by-id/${encodeURIComponent(trimmed)}`,
  );
  if (!response.configured) return { kind: 'not-configured' };
  if (response.results.length === 0) return { kind: 'not-found' };
  return { kind: 'found', result: response.results[0] };
}
