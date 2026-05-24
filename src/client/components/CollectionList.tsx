import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useList } from '../services/collection';
import { useFiltersState } from '../services/filters';
import { Button, Card, Input, StatusPill, TagChip } from './ui';
import BarcodeLookup from './BarcodeLookup';
import FiltersPanel from './FiltersPanel';
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
  const [searchParams, setSearchParams] = useSearchParams();
  const query = searchParams.get('q') ?? '';
  const setQuery = (next: string) => {
    const params = new URLSearchParams(searchParams);
    if (next) params.set('q', next);
    else params.delete('q');
    setSearchParams(params, { replace: true });
  };
  const { filters, setFilters, clear } = useFiltersState(type);
  const list = useList(type, query, filters);
  const items = list.data ?? [];
  const navigate = useNavigate();

  const onBarcodePick = (item: ResultMap[T]) => {
    navigate(newPath, { state: { prefill: item } });
  };

  const onBarcodeFallback = (code: string) => {
    navigate(newPath, { state: { barcodeOnly: code } });
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-xl font-medium text-text-primary tracking-tight">{title}</h1>
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

      <FiltersPanel
        type={type}
        value={filters}
        onChange={setFilters}
        onClear={clear}
      />

      {list.isLoading && <p className="text-text-secondary">Loading…</p>}
      {list.error && <p className="text-error">Failed to load.</p>}
      {!list.isLoading && items.length === 0 && (
        <Card className="text-center text-text-secondary py-8">No items yet — click "+ Add" to start.</Card>
      )}

      <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {items.map((item) => {
          const base = item as CollectionItemBase & { id?: number; imagePath?: string | null };
          const r = renderItem(item);
          const tags = base.tags ?? [];
          return (
            <Link key={base.id} to={`${newPath.replace(/\/new$/, '')}/${base.id}`} className="group block">
              <Card className="h-full !p-0 overflow-hidden transition-shadow hover:shadow-md group-hover:border-brand/30 dark:hover:bg-card/80">
                <div className="flex flex-col sm:flex-row">
                  {/* Cover art — dominant visual element */}
                  <div className="relative w-full sm:w-48 shrink-0 bg-imgPlaceholder overflow-hidden">
                    {base.imagePath ? (
                      <img
                        src={base.imagePath}
                        alt=""
                        loading="lazy"
                        className="w-full h-48 sm:h-auto sm:aspect-[2/3] object-cover transition-transform duration-300 group-hover:scale-105"
                      />
                    ) : (
                      <div
                        aria-hidden
                        className="w-full h-48 sm:h-auto sm:aspect-[2/3] flex items-center justify-center text-text-tertiary text-sm font-medium"
                      >
                        no cover
                      </div>
                    )}
                  </div>

                  {/* Metadata — right side on desktop, below cover on mobile */}
                  <div className="flex-1 p-3 flex flex-col gap-1.5 min-w-0">
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-medium text-text-primary leading-snug line-clamp-2">{r.primary}</h3>
                      <StatusPill status={base.status} />
                    </div>

                    {r.secondary && (
                      <div className="text-sm text-text-secondary truncate">{r.secondary}</div>
                    )}
                    {r.tertiary && (
                      <div className="text-xs text-text-tertiary truncate">{r.tertiary}</div>
                    )}

                    {base.personalRating != null && (
                      <div className="text-sm text-brand font-medium">★ {base.personalRating}/10</div>
                    )}

                    {tags.length > 0 && (
                      <div className="flex flex-wrap gap-1 mt-auto pt-1">
                        {tags.slice(0, 4).map((t) => (
                          <TagChip key={t} name={t} />
                        ))}
                        {tags.length > 4 && (
                          <span className="text-xs text-text-tertiary">+{tags.length - 4}</span>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
