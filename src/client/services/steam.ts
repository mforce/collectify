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

export function useSteamGames(enabled: boolean) {
  return useQuery<SteamPreview>({
    queryKey: ['steam', 'games'],
    queryFn: () => api<SteamPreview>('/api/accounts/steam/games'),
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
