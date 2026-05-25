import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useList } from '../services/collection';
import { useFiltersState } from '../services/filters';
import { Button, Card, Input, StatusPill, TagChip, ViewSwitcher } from './ui';
import BarcodeLookup from './BarcodeLookup';
import FiltersPanel from './FiltersPanel';
import MediaIcon from './MediaIcon';
import { useViewPreference, type ViewMode } from '../hooks/useViewPreference';
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
  category?: 'movies' | 'music' | 'games';
}

const TITLE_CLASS: Record<string, string> = {
  movies: 'text-movies',
  music: 'text-music',
  games: 'text-games',
};

const BTN_CLASS: Record<string, string> = {
  movies: 'border-movies-border text-movies hover:bg-movies-light',
  music: 'border-music-border text-music hover:bg-music-light',
  games: 'border-games-border text-games hover:bg-games-light',
};

const CARD_BORDER: Record<string, string> = {
  movies: 'group-hover:border-movies group-hover:bg-movies-light/70',
  music: 'group-hover:border-music group-hover:bg-music-light/70',
  games: 'group-hover:border-games group-hover:bg-games-light/70',
};

// ─── Card variants ──────────────────────────────────────────────

interface BaseItem extends CollectionItemBase {
  id?: number;
  imagePath?: string | null;
}

function CoverBlock({ src }: { src?: string | null }) {
  if (src) {
    return (
      <img
        src={src}
        alt=""
        loading="lazy"
        className="w-full aspect-[2/3] object-cover transition-transform duration-300 group-hover:scale-105"
      />
    );
  }
  return (
    <div aria-hidden className="w-full aspect-[2/3] flex items-center justify-center text-text-tertiary text-xs font-medium bg-imgPlaceholder">
      no cover
    </div>
  );
}

function ListCard({ item, r, category }: { item: BaseItem; r: RenderedItem; category?: string }) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  return (
    <Card className={`!p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      <div className="flex items-center gap-3 p-2">
        <div className="w-16 shrink-0 overflow-hidden rounded-xl bg-imgPlaceholder">
          <CoverBlock src={item.imagePath} />
        </div>
        <div className="min-w-0 flex-1 flex flex-col gap-1">
          <div className="flex items-start justify-between gap-2">
            <h3 className={`min-w-0 flex-1 font-medium text-text-primary leading-snug truncate ${titleClass}`}>{r.primary}</h3>
            <StatusPill status={item.status} category={category} />
          </div>
          {r.secondary && (
            <span className="text-sm text-text-secondary truncate shrink-0">{r.secondary}</span>
          )}
          {r.tertiary && (
            <span className="text-xs text-text-tertiary truncate shrink-0">{r.tertiary}</span>
          )}
        </div>
      </div>
    </Card>
  );
}

function MediumCard({ item, r, category }: { item: BaseItem; r: RenderedItem; category?: string }) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  const tags = item.tags ?? [];
  return (
    <Card className={`!p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      <div className="flex gap-3 p-3">
        <div className="w-24 shrink-0 overflow-hidden rounded-xl bg-imgPlaceholder">
          <CoverBlock src={item.imagePath} />
        </div>
        <div className="min-w-0 flex-1 flex flex-col gap-1.5">
          <div className="flex items-start justify-between gap-2">
            <h3 className={`min-w-0 flex-1 font-medium text-text-primary leading-snug line-clamp-2 ${titleClass}`}>{r.primary}</h3>
            <StatusPill status={item.status} category={category} />
          </div>
          {r.secondary && (
            <div className="text-sm text-text-secondary truncate shrink-0">{r.secondary}</div>
          )}
          {r.tertiary && (
            <div className="text-xs text-text-tertiary truncate shrink-0">{r.tertiary}</div>
          )}
          {item.personalRating != null && (
            <div className={`text-sm font-medium ${titleClass}`}>★ {item.personalRating}/10</div>
          )}
          {tags.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {tags.slice(0, 3).map((t) => (
                <TagChip key={t} name={t} category={category} />
              ))}
              {tags.length > 3 && <span className="text-xs text-text-tertiary">+{tags.length - 3}</span>}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}

function BigCard({ item, r, category }: { item: BaseItem; r: RenderedItem; category?: string }) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  const tags = item.tags ?? [];
  return (
    <Card className={`!p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      <div className="flex flex-col gap-3 md:flex-row">
        <div className="relative w-full shrink-0 overflow-hidden bg-imgPlaceholder sm:w-24 md:w-36 lg:w-48">
          <CoverBlock src={item.imagePath} />
        </div>
        <div className="flex flex-col gap-1.5 p-3 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <h3 className={`min-w-0 flex-1 font-medium text-text-primary leading-snug line-clamp-2 ${titleClass}`}>{r.primary}</h3>
            <StatusPill status={item.status} category={category} />
          </div>
          {r.secondary && (
            <div className="text-sm text-text-secondary truncate shrink-0">{r.secondary}</div>
          )}
          {r.tertiary && (
            <div className="text-xs text-text-tertiary truncate shrink-0">{r.tertiary}</div>
          )}
          {item.personalRating != null && (
            <div className={`text-sm font-medium ${titleClass}`}>★ {item.personalRating}/10</div>
          )}
          {tags.length > 0 && (
            <div className="flex flex-wrap gap-1 mt-auto pt-1">
              {tags.slice(0, 4).map((t) => (
                <TagChip key={t} name={t} category={category} />
              ))}
              {tags.length > 4 && <span className="text-xs text-text-tertiary">+{tags.length - 4}</span>}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}

// ─── Main component ─────────────────────────────────────────────

export default function CollectionList<T extends MediaType>({ type, title, newPath, renderItem, category }: Props<T>) {
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
  const [viewMode, setViewMode] = useViewPreference(type);

  const onBarcodePick = (item: ResultMap[T]) => {
    navigate(newPath, { state: { prefill: item } });
  };
  const onBarcodeFallback = (code: string) => {
    navigate(newPath, { state: { barcodeOnly: code } });
  };

  const btnClass = category ? BTN_CLASS[category] : '';

  // Grid classes per view mode
  const gridClass = viewMode === 'list'
    ? 'grid-cols-1 gap-2'
    : viewMode === 'medium'
      ? 'grid-cols-1 sm:grid-cols-2 gap-3'
      : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4';

  return (
    <div className={`space-y-4 ${category ? `theme-${category}` : ''}`}>
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div>
          <div className="flex items-center gap-3">
            {category && (
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-card shadow-sm">
                <MediaIcon type={category} className="h-7 w-7" />
              </span>
            )}
            <h1 className={`text-3xl font-extrabold tracking-tight ${category ? TITLE_CLASS[category] : 'text-text-primary'}`}>{title}</h1>
          </div>
          <p className="mt-1 text-sm text-text-secondary">Search, filter, and add to this shelf.</p>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <ViewSwitcher value={viewMode} onChange={setViewMode} />
          <BarcodeLookup type={type} onPick={onBarcodePick} onBarcodeFallback={onBarcodeFallback} />
          <Link to={newPath}>
            <Button variant={category ? 'secondary' : 'primary'} className={btnClass}>
              <span aria-hidden className="mr-1 text-lg leading-none">+</span>
              Add
            </Button>
          </Link>
        </div>
      </div>

      <Input placeholder={`Search ${title.toLowerCase()}…`} value={query} onChange={(e) => setQuery(e.target.value)} />

      <FiltersPanel type={type} value={filters} onChange={setFilters} onClear={clear} />

      {list.isLoading && <p className="text-text-secondary">Loading…</p>}
      {list.error && <p className="text-error">Failed to load.</p>}
      {!list.isLoading && items.length === 0 && (
        <Card className="text-center text-text-secondary py-8">No items yet — click "+ Add" to start.</Card>
      )}

      <div className={`grid ${gridClass}`}>
        {items.map((item) => {
          const base = item as BaseItem;
          const r = renderItem(item);
          return (
            <Link key={base.id} to={`${newPath.replace(/\/new$/, '')}/${base.id}`} className="group block">
              {viewMode === 'list' && <ListCard item={base} r={r} category={category} />}
              {viewMode === 'medium' && <MediumCard item={base} r={r} category={category} />}
              {viewMode === 'big' && <BigCard item={base} r={r} category={category} />}
            </Link>
          );
        })}
      </div>
    </div>
  );
}
