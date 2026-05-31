import { useQuery } from '@tanstack/react-query';
import { api, ApiError } from './client';
import type { GamePlatform, MediaType } from './types';

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
  // Provider canonicalises the first recognised platform name into the
  // shared GamePlatform enum; null when nothing in the source list
  // resolved (form leaves the dropdown unset rather than defaulting).
  platform: GamePlatform | null;
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
  hint?: string;
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

/**
 * Three-way outcome for an imperative direct-lookup call. Lets the caller
 * branch on unconfigured vs not-found without sniffing array lengths.
 * Generic over the result type so movie / music / game lookups share it.
 * Defaults to MovieLookupResult so existing callers don't need updates.
 */
export type LookupByIdOutcome<T = MovieLookupResult> =
  | { kind: 'found'; result: T }
  | { kind: 'not-found' }
  | { kind: 'not-configured' };

async function lookupOneOf<T>(url: string): Promise<LookupByIdOutcome<T>> {
  const response = await api<LookupResponse<T>>(url);
  if (!response.configured) return { kind: 'not-configured' };
  if (response.results.length === 0) return { kind: 'not-found' };
  return { kind: 'found', result: response.results[0] };
}

/**
 * Direct lookup of a movie by its provider id (e.g. a TMDB id). Imperative
 * by design -- the user clicks a button to trigger a single fetch, no
 * debouncing or background revalidation needed.
 */
export async function lookupMovieById(providerKey: string): Promise<LookupByIdOutcome<MovieLookupResult>> {
  const trimmed = providerKey.trim();
  if (!trimmed) return { kind: 'not-found' };
  return lookupOneOf<MovieLookupResult>(`/api/lookup/movies/by-id/${encodeURIComponent(trimmed)}`);
}

/**
 * Like {@link lookupMovieById} but uses an external IMDB id (the "tt..." form).
 * The server resolves it to a TMDB id under the hood; the response shape is
 * identical so the same caller code handles both flows.
 */
export async function lookupMovieByImdbId(imdbId: string): Promise<LookupByIdOutcome<MovieLookupResult>> {
  const trimmed = imdbId.trim();
  if (!trimmed) return { kind: 'not-found' };
  return lookupOneOf<MovieLookupResult>(`/api/lookup/movies/by-imdb-id/${encodeURIComponent(trimmed)}`);
}

/**
 * Direct lookup of a music release by its provider id (a MusicBrainz MBID).
 * Mirrors {@link lookupMovieById} for the music form's Fetch metadata button.
 */
export async function lookupAlbumByMbid(mbid: string): Promise<LookupByIdOutcome<MusicLookupResult>> {
  const trimmed = mbid.trim();
  if (!trimmed) return { kind: 'not-found' };
  return lookupOneOf<MusicLookupResult>(`/api/lookup/music/by-id/${encodeURIComponent(trimmed)}`);
}

/**
 * Direct lookup of a game by its IGDB id. Mirrors {@link lookupMovieById}
 * for the game form's Fetch metadata button.
 */
export async function lookupGameByIgdbId(igdbId: string): Promise<LookupByIdOutcome<GameLookupResult>> {
  const trimmed = igdbId.trim();
  if (!trimmed) return { kind: 'not-found' };
  return lookupOneOf<GameLookupResult>(`/api/lookup/games/by-id/${encodeURIComponent(trimmed)}`);
}

/**
 * Barcode lookup. Returns the full LookupResponse so the caller can show
 * "not configured" hints, render multiple candidates when the same UPC is
 * shared across editions, and handle the empty case differently from a
 * fetch error. Used by the upcoming BarcodeScanner / Scan tab in the add
 * wizard; the imperative shape mirrors {@link lookupMovieById}.
 */
export async function lookupByBarcode<T extends MediaType>(
  type: T,
  barcode: string,
): Promise<LookupResponse<ResultMap[T]>> {
  return api<LookupResponse<ResultMap[T]>>(
    `/api/lookup/${type}/by-barcode/${encodeURIComponent(barcode.trim())}`,
  );
}

/**
 * Photo-snap lookup. Uploads a resized image and returns candidates from
 * OCR + web entity + URL routing paths. Same LookupResponse shape as
 * barcode/title search so the frontend reuses the candidate list UI.
 *
 * Uses fetch directly (not the api() helper) because FormData requires
 * the browser to set the multipart Content-Type boundary. The api() helper
 * would override it with application/json.
 */
export async function lookupByImage<T extends MediaType>(
  type: T,
  file: Blob,
): Promise<LookupResponse<ResultMap[T]>> {
  const form = new FormData();
  form.append('file', file, 'cover.jpg');

  const res = await fetch(`/api/lookup/${type}/by-image`, {
    method: 'POST',
    credentials: 'include',
    body: form,
  });

  if (!res.ok) {
    let message = res.statusText;
    try {
      const data = await res.json();
      if (data?.error) message = data.error;
    } catch {}
    throw new ApiError(res.status, message);
  }

  return res.json() as Promise<LookupResponse<ResultMap[T]>>;
}
