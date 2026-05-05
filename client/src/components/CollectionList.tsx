import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useList } from '../api/collection';
import { Button, Card, Input } from './ui';
import type { MediaType } from '../api/types';

interface Props<T extends MediaType> {
  type: T;
  title: string;
  newPath: string;
  renderItem: (item: any) => { primary: string; secondary?: string; tertiary?: string };
}

export default function CollectionList<T extends MediaType>({ type, title, newPath, renderItem }: Props<T>) {
  const [query, setQuery] = useState('');
  const list = useList(type, query);
  const items = list.data ?? [];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-white">{title}</h1>
        <Link to={newPath}>
          <Button>+ Add</Button>
        </Link>
      </div>

      <Input
        placeholder={`Search ${title.toLowerCase()}…`}
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />

      {list.isLoading && <p className="text-slate-400">Loading…</p>}
      {list.error && <p className="text-rose-400">Failed to load.</p>}
      {!list.isLoading && items.length === 0 && (
        <Card className="text-center text-slate-400">No items yet — click "+ Add" to start.</Card>
      )}

      <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {items.map((item: any) => {
          const r = renderItem(item);
          return (
            <Link key={item.id} to={`${newPath.replace(/\/new$/, '')}/${item.id}`} className="block">
              <Card className="hover:border-indigo-500 transition-colors h-full">
                <div className="font-medium text-white truncate">{r.primary}</div>
                {r.secondary && <div className="text-sm text-slate-400 truncate">{r.secondary}</div>}
                {r.tertiary && <div className="text-xs text-slate-500 mt-1 truncate">{r.tertiary}</div>}
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
