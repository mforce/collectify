import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';

export interface SteamConnection {
  connected: boolean;
  steamId?: string | null;
  personaName?: string | null;
}

export interface SteamConnect {
  configured: boolean;
  redirectUrl?: string | null;
}

export interface SteamOwnedTitle {
  externalGameId: string;
  title: string;
  playtimeMinutes: number;
  /** Steam square icon (~32px). Used as a thumbnail fallback. */
  iconUrl?: string | null;
  /** Steam logo banner (~184x69). Preferred thumbnail when present. */
  logoUrl?: string | null;
  lastPlayedAt?: string | null;
  state: 'importable' | 'imported';
}

export interface SteamPreview {
  /** 'notconnected' | 'ok' | 'unavailable' */
  status: string;
  titles: SteamOwnedTitle[];
  truncated: boolean;
  /** Total searched-library titles (before paging) — enables paging controls. */
  total: number;
  /** Server-configured maximum number of distinct games accepted per import. */
  importCap: number;
}

export interface SteamImportResult {
  imported: number;
  alreadyImported: number;
  items: { externalGameId: string; imported: boolean; alreadyImported: boolean }[];
}

export function useSteamConnection() {
  return useQuery<SteamConnection>({
    queryKey: ['steam', 'connection'],
    queryFn: () => api<SteamConnection>('/api/accounts/steam'),
  });
}

export function useSteamConnect() {
  return useMutation({
    mutationFn: () => api<SteamConnect>('/api/accounts/steam/connect', { method: 'POST' }),
  });
}

export function useSteamGames(enabled: boolean, search = '', offset = 0, limit = 100, hideImported = false) {
  return useQuery<SteamPreview>({
    queryKey: ['steam', 'games', search.trim().toLowerCase(), offset, limit, hideImported],
    queryFn: () => {
      const params = new URLSearchParams();
      const q = search.trim();
      if (q) params.set('q', q);
      if (offset > 0) params.set('offset', String(offset));
      params.set('limit', String(limit));
      if (hideImported) params.set('hideImported', 'true');
      const qs = params.toString();
      return api<SteamPreview>(`/api/accounts/steam/games${qs ? `?${qs}` : ''}`);
    },
    enabled,
  });
}

export function useSteamImport(onSuccess: () => void) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) =>
      api<SteamImportResult>('/api/accounts/steam/import', {
        method: 'POST',
        body: JSON.stringify({ externalGameIds: ids }),
      }),
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ['steam', 'games'] });
      qc.invalidateQueries({ queryKey: ['games'] });
      onSuccess();
    },
  });
}

export function useSteamDisconnect(onSuccess: () => void) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api<void>('/api/accounts/steam', { method: 'DELETE' }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['steam', 'connection'] });
      qc.invalidateQueries({ queryKey: ['steam', 'games'] });
      onSuccess();
    },
  });
}
