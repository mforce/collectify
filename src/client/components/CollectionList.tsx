import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useList } from '../services/collection';
import { Button, Card, Input, StatusPill, TagChip } from './ui';
import BarcodeLookup from './BarcodeLookup';
import type { CollectionItemBase, MediaType } from '../services/types';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';

type ResultMap = {
  movies: MovieLookupResult;
  music: MusicLookupResult;
  games: GameLookupResult;
};

interface RenderedItem {
  primary: string;
  secondary?: string;
  tertiary?: string;
}

interface Props<T extends MediaType> {
  type: T;
  title: string;
  newPath: string;
  renderItem: (item: any) => RenderedItem;
}

export default function CollectionList<T extends MediaType>({ type, title, newPath, renderItem }: Props<T>) {
  const [query, setQuery] = useState('');
  const list = useList(type, query);
  const items = list.data ?? [];
  const navigate = useNavigate();

  // Picking a barcode candidate from the list-page scanner skips the
  // intermediate landing -- we go straight to /{type}/new and seed the
  // form via React Router state. AddPage reads this and hands it to the
  // form as prefillLookup so the same enrichment chain runs as if the
  // user had scanned from inside the form.
  const onBarcodePick = (item: ResultMap[T]) => {
    navigate(newPath, { state: { prefill: item } });
  };

  // Soft fallback: when the scan resolves but no provider candidates
  // come back (common for movies / games -- UPCitemdb coverage gaps),
  // still salvage the scan by sending the user to /add with just the
  // barcode pre-filled. They can finish via the reliable title search
  // without having to retype the UPC.
  const onBarcodeFallback = (code: string) => {
    navigate(newPath, { state: { barcodeOnly: code } });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-white">{title}</h1>
        <div className="flex items-center gap-2">
          <BarcodeLookup
            type={type}
            onPick={onBarcodePick}
            onBarcodeFallback={onBarcodeFallback}
          />
          <Link to={newPath}>
            <Button>+ Add</Button>
          </Link>
        </div>
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
        {items.map((item) => {
          const base = item as CollectionItemBase & { id?: number; imagePath?: string | null };
          const r = renderItem(item);
          const tags = base.tags ?? [];
          return (
            <Link key={base.id} to={`${newPath.replace(/\/new$/, '')}/${base.id}`} className="block">
              <Card className="hover:border-indigo-500 transition-colors h-full !p-3 flex gap-3">
                {base.imagePath ? (
                  <img
                    src={base.imagePath}
                    alt=""
                    loading="lazy"
                    className="w-16 h-24 object-cover rounded flex-none bg-slate-800"
                  />
                ) : (
                  <div
                    aria-hidden
                    className="w-16 h-24 rounded flex-none bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-600 text-xs"
                  >
                    no cover
                  </div>
                )}

                <div className="min-w-0 flex-1 flex flex-col gap-1">
                  <div className="flex items-start gap-2">
                    <div className="font-medium text-white truncate flex-1">{r.primary}</div>
                    <StatusPill status={base.status} />
                  </div>
                  {r.secondary && <div className="text-sm text-slate-400 truncate">{r.secondary}</div>}
                  {r.tertiary && <div className="text-xs text-slate-500 truncate">{r.tertiary}</div>}
                  {base.personalRating != null && (
                    <div className="text-xs text-amber-300">★ {base.personalRating}/10</div>
                  )}
                  {tags.length > 0 && (
                    <div className="flex flex-wrap gap-1 mt-auto">
                      {tags.slice(0, 4).map((t) => (
                        <TagChip key={t} name={t} />
                      ))}
                      {tags.length > 4 && (
                        <span className="text-xs text-slate-500">+{tags.length - 4}</span>
                      )}
                    </div>
                  )}
                </div>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
