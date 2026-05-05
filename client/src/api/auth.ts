import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';

export interface AuthState {
  needsSetup: boolean;
  isAuthenticated: boolean;
  userName: string | null;
}

export function useAuth() {
  return useQuery({
    queryKey: ['auth'],
    queryFn: () => api<AuthState>('/api/auth/me'),
  });
}

export function useSetup() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { userName: string; password: string }) =>
      api('/api/auth/setup', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['auth'] }),
  });
}

export function useLogin() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { userName: string; password: string }) =>
      api('/api/auth/login', { method: 'POST', body: JSON.stringify(body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['auth'] }),
  });
}

export function useLogout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api('/api/auth/logout', { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries(),
  });
}
