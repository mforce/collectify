import { useEffect, useState, type ReactNode } from 'react';
import { useLookup } from '../services/lookup';
import type { MediaResultMap } from '../services/mediaRegistry';
import type { MediaType, GamePlatform } from '../services/types';
import { Field, Input, Label } from './ui';
import CandidateList from './CandidateList';

interface Props<T extends MediaType> {
  type: T;
  /** How to render a single suggestion row in the dropdown. */
  renderItem: (item: MediaResultMap[T]) => { primary: string; secondary?: ReactNode; image?: string | null };
  /** Called when the user clicks a suggestion. The form converts the result into its own state shape. */
  onPick: (item: MediaResultMap[T]) => void;
  /** Optional label shown above the input. */
  label?: string;
  placeholder?: string;
  /**
   * Games only: pass the game's already-set platform so the backend prioritises
   * same-platform results to the top of the dropdown. Ignored for other types.
   */
  platform?: GamePlatform;
}

/**
 * Provider-backed type-ahead used as a "Search online" affordance on the
 * three media forms. Debounces input by 300 ms, hides the dropdown when the
 * query is empty, and surfaces a small hint when the server reports
 * `configured: false` so users know they need to set the provider key.
 */
export default function OnlineSearch<T extends MediaType>({
  type,
  renderItem,
  onPick,
  label = 'Search online',
  placeholder = 'Type a title…',
  platform,
}: Props<T>) {
  const [query, setQuery] = useState('');
  const [debounced, setDebounced] = useState('');
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setDebounced(query), 300);
    return () => clearTimeout(t);
  }, [query]);

  const lookup = useLookup(type, debounced, platform);
  const data = lookup.data;

  const results = (data?.results ?? []) as MediaResultMap[T][];
  const showDropdown = open && debounced.trim().length >= 2;

  return (
    <div className="relative">
      <Field label={label}>
        <Input
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder={placeholder}
        />
      </Field>

      {showDropdown && (
        <div className="absolute z-10 mt-1 w-full rounded-md bg-input-bg border border-border shadow-lg max-h-80 overflow-auto">
          {lookup.isLoading && <div className="px-3 py-2 text-sm text-text-secondary">Searching…</div>}
          {lookup.error && <div className="px-3 py-2 text-sm text-error">Lookup failed.</div>}

          {data && !data.configured && (
            <div className="px-3 py-2 text-xs text-text-secondary border-b border-border">
              Online lookup is not configured. Set the provider env var to enable.
            </div>
          )}

          {data && data.configured && results.length === 0 && !lookup.isLoading && (
            <div className="px-3 py-2 text-sm text-text-secondary">No matches.</div>
          )}

          <CandidateList type={type} items={results} renderItem={renderItem} onPick={(item) => {
            onPick(item); setOpen(false); setQuery(''); setDebounced('');
          }} />
        </div>
      )}

      {data && !data.configured && !showDropdown && (
        <Label>
          <span className="text-text-tertiary italic">
            Online lookup not configured.
          </span>
        </Label>
      )}
    </div>
  );
}
