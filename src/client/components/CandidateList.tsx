import type { ReactNode } from 'react';
import type { MediaType } from '../services/types';
import type { MediaResultMap } from '../services/mediaRegistry';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from '../services/lookup';

export type CandidateView = { primary: string; secondary?: ReactNode; image?: string | null };

export function defaultView<T extends MediaType>(_type: T, item: MediaResultMap[T]): CandidateView {
  const r = item as Partial<MovieLookupResult & MusicLookupResult & GameLookupResult>;
  const primary = (r.title ?? '') + (r.year ? ` (${r.year})` : '');
  const gameBits = [(r as GameLookupResult).developer, (r as GameLookupResult).platform].filter(Boolean).join(' · ');
  const secondary = (r as MusicLookupResult).artistName ?? (gameBits || r.description?.slice(0, 120) || undefined);
  return { primary, secondary, image: r.imageUrl ?? null };
}

export default function CandidateList<T extends MediaType>({ type, items, onPick, renderItem }: {
  type: T; items: MediaResultMap[T][]; onPick: (item: MediaResultMap[T]) => void;
  renderItem?: (item: MediaResultMap[T]) => CandidateView;
}) {
  return <>{items.map((item, i) => {
    const view = renderItem?.(item) ?? defaultView(type, item);
    return <button type="button" key={`${item.providerKey ?? i}`} onClick={() => onPick(item)} className="category-hover-soft flex w-full items-start gap-3 border-b border-border px-3 py-2 text-left transition-colors last:border-b-0">
      {view.image && <img src={view.image} alt="" className="w-10 h-14 object-cover rounded flex-none" />}
      <div className="min-w-0 flex-1"><div className="text-sm text-text-primary truncate">{view.primary}</div>{view.secondary && <div className="text-xs text-text-secondary truncate">{view.secondary}</div>}</div>
    </button>;
  })}</>;
}
