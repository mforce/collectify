import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useState, type ReactNode } from 'react';
import { useBulkUpdate, useList, type BulkUpdates } from '../services/collection';
import { useFiltersState } from '../services/filters';
import { ApiError } from '../services/client';
import { Button, Card, Field, Input, RatingInput, Select, StatusPill, TagChip, TagInput, ViewSwitcher } from './ui';
import BarcodeLookup from './BarcodeLookup';
import PhotoLookup from './PhotoLookup';
import FiltersPanel from './FiltersPanel';
import MediaIcon from './MediaIcon';
import { useViewPreference, type ViewMode } from '../hooks/useViewPreference';
import {
  COLLECTION_STATUSES,
  WATCH_STATUSES,
  type CollectionItemBase,
  type CollectionStatus,
  type MediaType,
  type WatchStatus,
} from '../services/types';
import type { MediaResultMap } from '../services/mediaRegistry';

interface RenderedItem {
  primary: string;
  secondary?: ReactNode;
  tertiary?: ReactNode;
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

// A checkbox rendered as a sibling of the surrounding <Link>; stopping
// propagation here keeps a click on the checkbox from also firing the
// card's navigation.
function SelectCheckbox({ id, checked, onToggle }: { id: number; checked: boolean; onToggle: (id: number) => void }) {
  return (
    <span
      onClick={(e) => e.stopPropagation()}
      className="absolute left-2 top-2 z-10 flex h-6 w-6 items-center justify-center rounded-md bg-card/90 shadow-sm"
    >
      <input
        type="checkbox"
        checked={checked}
        onChange={() => onToggle(id)}
        aria-label="Select item"
        className="h-4 w-4 cursor-pointer accent-brand"
      />
    </span>
  );
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

interface CardSelectProps {
  selected: boolean;
  onToggle: (id: number) => void;
}

function ListCard({ item, r, category, selected, onToggle }: { item: BaseItem; r: RenderedItem; category?: string } & CardSelectProps) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  return (
    <Card className={`relative !p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      {item.id != null && <SelectCheckbox id={item.id} checked={selected} onToggle={onToggle} />}
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

function MediumCard({ item, r, category, selected, onToggle }: { item: BaseItem; r: RenderedItem; category?: string } & CardSelectProps) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  const tags = item.tags ?? [];
  return (
    <Card className={`relative !p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      {item.id != null && <SelectCheckbox id={item.id} checked={selected} onToggle={onToggle} />}
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

function BigCard({ item, r, category, selected, onToggle }: { item: BaseItem; r: RenderedItem; category?: string } & CardSelectProps) {
  const titleClass = category ? TITLE_CLASS[category] : 'text-text-primary';
  const tags = item.tags ?? [];
  return (
    <Card className={`relative !p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${category ? CARD_BORDER[category] : 'group-hover:border-brand/30'}`}>
      {item.id != null && <SelectCheckbox id={item.id} checked={selected} onToggle={onToggle} />}
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

  const onBarcodePick = (item: MediaResultMap[T]) => {
    navigate(newPath, { state: { prefill: item } });
  };
  const onBarcodeFallback = (code: string) => {
    navigate(newPath, { state: { barcodeOnly: code } });
  };

  const btnClass = category ? BTN_CLASS[category] : '';

  // ─── Bulk selection + edit ─────────────────────────────────────
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const toggleSelected = (id: number) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };
  const clearSelection = () => setSelected(new Set());

  const allIds = items
    .map((item) => (item as BaseItem).id)
    .filter((id): id is number => typeof id === 'number');
  const allSelected = allIds.length > 0 && allIds.every((id) => selected.has(id));
  const toggleSelectAll = () => setSelected(allSelected ? new Set() : new Set(allIds));

  const [bulkModalOpen, setBulkModalOpen] = useState(false);
  const [bulkStatus, setBulkStatus] = useState<CollectionStatus | ''>('');
  const [bulkRating, setBulkRating] = useState<number | null>(null);
  const [bulkTags, setBulkTags] = useState<string[]>([]);
  const [bulkAcquiredOn, setBulkAcquiredOn] = useState('');
  const [bulkWatchStatus, setBulkWatchStatus] = useState<WatchStatus | ''>('');

  const bulkUpdate = useBulkUpdate(type);

  const openBulkEdit = () => {
    setBulkStatus('');
    setBulkRating(null);
    setBulkTags([]);
    setBulkAcquiredOn('');
    setBulkWatchStatus('');
    setBulkModalOpen(true);
  };
  const closeBulkEdit = () => setBulkModalOpen(false);

  const confirmBulkEdit = () => {
    const updates: BulkUpdates = {};
    if (bulkStatus) updates.status = bulkStatus;
    if (bulkRating != null) updates.personalRating = bulkRating;
    if (bulkTags.length > 0) updates.tags = bulkTags;
    if (bulkAcquiredOn) updates.acquiredOn = bulkAcquiredOn;
    if (category === 'movies' && bulkWatchStatus) updates.watchStatus = bulkWatchStatus;

    bulkUpdate.mutate(
      { ids: [...selected], updates },
      {
        onSuccess: () => {
          clearSelection();
          setBulkModalOpen(false);
        },
      },
    );
  };

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
          {items.length > 0 && (
            <label className="flex items-center gap-1.5 text-xs font-semibold text-text-secondary">
              <input
                type="checkbox"
                checked={allSelected}
                onChange={toggleSelectAll}
                aria-label="Select all on current page"
                className="h-4 w-4 accent-brand"
              />
              Select all
            </label>
          )}
          <ViewSwitcher value={viewMode} onChange={setViewMode} />
          <BarcodeLookup type={type} onPick={onBarcodePick} onBarcodeFallback={onBarcodeFallback} />
          <PhotoLookup type={type} onPick={onBarcodePick} />
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

      {selected.size > 0 && (
        <div className="flex items-center justify-between gap-3 rounded-xl border border-border bg-card px-4 py-2">
          <span className="text-sm font-semibold text-text-primary">{selected.size} selected</span>
          <div className="flex items-center gap-2">
            <Button type="button" variant="secondary" onClick={clearSelection}>Clear</Button>
            <Button type="button" onClick={openBulkEdit}>Edit selected</Button>
          </div>
        </div>
      )}

      {list.isLoading && <p className="text-text-secondary">Loading…</p>}
      {list.error && <p className="text-error">Failed to load.</p>}
      {!list.isLoading && items.length === 0 && (
        <Card className="text-center text-text-secondary py-8">No items yet — click "+ Add" to start.</Card>
      )}

      <div className={`grid ${gridClass}`}>
        {items.map((item) => {
          const base = item as BaseItem;
          const r = renderItem(item);
          const isSelected = base.id != null && selected.has(base.id);
          return (
            <Link key={base.id} to={`${newPath.replace(/\/new$/, '')}/${base.id}`} className="group block">
              {viewMode === 'list' && <ListCard item={base} r={r} category={category} selected={isSelected} onToggle={toggleSelected} />}
              {viewMode === 'medium' && <MediumCard item={base} r={r} category={category} selected={isSelected} onToggle={toggleSelected} />}
              {viewMode === 'big' && <BigCard item={base} r={r} category={category} selected={isSelected} onToggle={toggleSelected} />}
            </Link>
          );
        })}
      </div>

      {bulkModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
          <Card className="w-full max-w-md space-y-4">
            <h2 className="text-lg font-bold text-text-primary">
              Edit {selected.size} item{selected.size === 1 ? '' : 's'}
            </h2>

            <Field label="Status">
              <Select value={bulkStatus} onChange={(e) => setBulkStatus(e.target.value as CollectionStatus | '')}>
                <option value="">— Leave unchanged —</option>
                {COLLECTION_STATUSES.map((s) => (
                  <option key={s.value} value={s.value}>{s.label}</option>
                ))}
              </Select>
            </Field>

            {category === 'movies' && (
              <Field label="Watch status">
                <Select value={bulkWatchStatus} onChange={(e) => setBulkWatchStatus(e.target.value as WatchStatus | '')}>
                  <option value="">— Leave unchanged —</option>
                  {WATCH_STATUSES.map((s) => (
                    <option key={s.value} value={s.value}>{s.label}</option>
                  ))}
                </Select>
              </Field>
            )}

            <Field label="Personal rating">
              <RatingInput value={bulkRating} onChange={setBulkRating} category={category} />
            </Field>

            <Field label="Tags">
              <TagInput value={bulkTags} onChange={setBulkTags} category={category} />
            </Field>

            <Field label="Acquired on">
              <Input type="date" value={bulkAcquiredOn} onChange={(e) => setBulkAcquiredOn(e.target.value)} />
            </Field>

            {bulkUpdate.isError && (
              <p className="text-sm text-error">
                {bulkUpdate.error instanceof ApiError ? bulkUpdate.error.message : 'Something went wrong.'}
              </p>
            )}

            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" onClick={closeBulkEdit}>Cancel</Button>
              <Button type="button" onClick={confirmBulkEdit} disabled={bulkUpdate.isPending}>Confirm</Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
