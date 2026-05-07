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
