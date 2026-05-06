import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type { Album, Game, MediaType, Movie } from './types';

type ItemMap = { movies: Movie; music: Album; games: Game };

export function useList<T extends MediaType>(type: T, query: string) {
  return useQuery({
    queryKey: [type, 'list', query],
    queryFn: () => {
      const qs = query ? `?query=${encodeURIComponent(query)}` : '';
      return api<ItemMap[T][]>(`/api/${type}${qs}`);
    },
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
