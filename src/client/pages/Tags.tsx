import { useDeleteTag, useTags } from '../services/tags';
import { useToast } from '../components/toaster';
import { Button, Card } from '../components/ui';
import type { Tag } from '../services/types';

export default function TagsPage() {
  const tags = useTags();
  const del = useDeleteTag();
  const toast = useToast();

  const onDelete = (t: Tag) => {
    if (!confirm(`Delete tag "${t.name}"? It will be removed from every item it's attached to.`)) return;
    del.mutate(t.id, {
      onSuccess: () => toast.success(`Tag "${t.name}" deleted.`),
      onError: (err) => toast.error(`Failed to delete tag: ${(err as Error).message ?? 'unknown error'}`),
    });
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-white">Tags</h1>
      <p className="text-sm text-slate-400">
        Tags you've created across movies, music, and games. Deleting a tag here
        removes it from every item it's attached to — the items themselves are kept.
      </p>

      {tags.isLoading && <p className="text-slate-400">Loading…</p>}
      {tags.error && <p className="text-rose-400">Failed to load tags.</p>}

      {!tags.isLoading && (tags.data ?? []).length === 0 && (
        <Card className="text-center text-slate-400">
          You don't have any tags yet. Add some on a movie, album, or game form.
        </Card>
      )}

      {(tags.data ?? []).length > 0 && (
        <Card className="!p-0">
          <ul className="divide-y divide-slate-800">
            {tags.data!.map((t) => (
              <li key={t.id} className="flex items-center justify-between px-4 py-2">
                <span className="text-sm text-slate-200">{t.name}</span>
                <Button
                  variant="danger"
                  onClick={() => onDelete(t)}
                  disabled={del.isPending}
                  aria-label={`Delete tag ${t.name}`}
                >
                  Delete
                </Button>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {del.error && (
        <p className="text-sm text-rose-400">Failed to delete tag: {(del.error as Error).message}</p>
      )}
    </div>
  );
}
