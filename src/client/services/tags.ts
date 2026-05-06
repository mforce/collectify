import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from './client';
import type { Tag } from './types';

const KEY = ['tags'] as const;

export function useTags() {
  return useQuery({
    queryKey: [...KEY, 'list'],
    queryFn: () => api<Tag[]>('/api/tags'),
  });
}

export function useCreateTag() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      api<Tag>('/api/tags', { method: 'POST', body: JSON.stringify({ name }) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEY }),
  });
}

export function useDeleteTag() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api(`/api/tags/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      // Tags can be attached to any media type; deleting one invalidates the
      // chip lists on every collection page, not just /tags itself.
      qc.invalidateQueries({ queryKey: KEY });
      qc.invalidateQueries({ queryKey: ['movies'] });
      qc.invalidateQueries({ queryKey: ['music'] });
      qc.invalidateQueries({ queryKey: ['games'] });
    },
  });
}
