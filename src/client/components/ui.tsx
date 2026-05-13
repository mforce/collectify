import { useEffect, useRef, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type KeyboardEvent, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import {
  COLLECTION_STATUSES,
  CONDITIONS,
  type CollectionStatus,
  type Condition,
} from '../services/types';

export function Button({ className = '', variant = 'primary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }) {
  const variants = {
    primary: 'bg-indigo-500 hover:bg-indigo-400 text-white',
    secondary: 'bg-slate-700 hover:bg-slate-600 text-slate-100',
    danger: 'bg-rose-600 hover:bg-rose-500 text-white',
  } as const;
  return (
    <button
      {...props}
      className={`inline-flex items-center justify-center rounded-md px-3 py-2 min-h-[44px] text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed ${variants[variant]} ${className}`}
    />
  );
}

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={`block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 min-h-[44px] text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:border-indigo-400 ${className}`}
    />
  );
}

export function Textarea({ className = '', ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={`block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:border-indigo-400 ${className}`}
    />
  );
}

export function Select({ className = '', ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={`block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 min-h-[44px] text-sm text-slate-100 focus:outline-none focus:border-indigo-400 ${className}`}
    />
  );
}

// ---------- Searchable select ----------

export interface SearchableOption {
  value: string;
  label: string;
  /** Optional grouping header (rendered above the first option in each group). */
  group?: string;
}

interface SearchableSelectProps {
  value: string;
  onChange: (next: string) => void;
  options: SearchableOption[];
  placeholder?: string;
  /** Optional id so a parent <Label> can associate via htmlFor. */
  id?: string;
}

/**
 * Typeahead replacement for a long native <select>. Click (or focus) the
 * input to open the popup; typing filters by case-insensitive substring
 * match on the option label *and* its group header (so "nintendo" pulls
 * up every Nintendo platform even though it's not in the labels). Arrow
 * keys / Enter / Escape are wired the way you'd expect; click-outside
 * closes; Tab confirms-and-moves-on without overwriting the value.
 *
 * Groups whose options are all filtered out drop their header so a
 * narrow search isn't sprinkled with empty section labels.
 */
export function SearchableSelect({
  value,
  onChange,
  options,
  placeholder = 'Pick one…',
  id,
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  // Index into the *filtered* list so arrow-key navigation stays in
  // sync with what the user can currently see.
  const [activeIndex, setActiveIndex] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const selected = options.find((o) => o.value === value);

  const filtered = (() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter((o) =>
      o.label.toLowerCase().includes(q) || (o.group ?? '').toLowerCase().includes(q),
    );
  })();

  useEffect(() => {
    if (!open) return;
    const onClick = (e: globalThis.MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener('mousedown', onClick);
    return () => document.removeEventListener('mousedown', onClick);
  }, [open]);

  // Reset highlight to the first visible option whenever the filter changes.
  useEffect(() => {
    setActiveIndex(0);
  }, [query, open]);

  const commit = (opt: SearchableOption) => {
    onChange(opt.value);
    setQuery('');
    setOpen(false);
  };

  const onKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      if (!open) setOpen(true);
      else setActiveIndex((i) => Math.min(i + 1, filtered.length - 1));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === 'Enter') {
      if (open && filtered[activeIndex]) {
        e.preventDefault();
        commit(filtered[activeIndex]);
      }
    } else if (e.key === 'Escape') {
      if (open) {
        e.preventDefault();
        setOpen(false);
        setQuery('');
      }
    }
  };

  // Walk filtered options once, emitting an optgroup header the first
  // time we see a new group label. Tracking activeIndex against the
  // flat filtered array means arrow keys still skip past headers.
  const rendered: { kind: 'header' | 'option'; text: string; flatIndex?: number; opt?: SearchableOption }[] = [];
  let lastGroup: string | undefined = undefined;
  filtered.forEach((opt, i) => {
    if (opt.group && opt.group !== lastGroup) {
      rendered.push({ kind: 'header', text: opt.group });
      lastGroup = opt.group;
    } else if (!opt.group && lastGroup !== undefined) {
      // Reset so a later grouped option after a flat one still emits its header.
      lastGroup = undefined;
    }
    rendered.push({ kind: 'option', text: opt.label, flatIndex: i, opt });
  });

  // The visible "input" is a thin wrapper: when the popup is closed it
  // shows the selected label; when open it shows the filter query the
  // user is typing. That way you don't have to clear the label by hand
  // before searching.
  const displayValue = open ? query : selected?.label ?? '';

  return (
    <div ref={rootRef} className="relative">
      <input
        id={id}
        ref={inputRef}
        role="combobox"
        aria-expanded={open}
        aria-controls={id ? `${id}-listbox` : undefined}
        aria-autocomplete="list"
        autoComplete="off"
        value={displayValue}
        placeholder={placeholder}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={onKey}
        className="block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 min-h-[44px] text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:border-indigo-400"
      />
      {open && (
        <div
          id={id ? `${id}-listbox` : undefined}
          role="listbox"
          className="absolute z-20 mt-1 w-full rounded-md bg-slate-900 border border-slate-700 shadow-lg max-h-72 overflow-auto"
        >
          {filtered.length === 0 && (
            <div className="px-3 py-2 text-sm text-slate-400">No matches.</div>
          )}
          {rendered.map((row, idx) => {
            if (row.kind === 'header') {
              return (
                <div
                  key={`h-${row.text}-${idx}`}
                  className="px-3 py-1 text-xs font-semibold uppercase tracking-wider text-slate-500 border-b border-slate-800"
                >
                  {row.text}
                </div>
              );
            }
            const isActive = row.flatIndex === activeIndex;
            const isSelected = row.opt!.value === value;
            return (
              <button
                key={row.opt!.value}
                type="button"
                role="option"
                aria-selected={isSelected}
                onMouseEnter={() => setActiveIndex(row.flatIndex!)}
                onMouseDown={(e) => {
                  // mousedown (not click) so the click fires before
                  // the input's blur tears down state we just set.
                  e.preventDefault();
                  commit(row.opt!);
                }}
                className={`w-full text-left px-3 py-2 text-sm border-b border-slate-800 last:border-b-0 flex items-center justify-between ${
                  isActive ? 'bg-slate-800 text-white' : 'text-slate-200 hover:bg-slate-800'
                }`}
              >
                <span>{row.text}</span>
                {isSelected && <span aria-hidden className="text-indigo-300 text-xs">✓</span>}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

export function Label({ children, htmlFor }: { children: React.ReactNode; htmlFor?: string }) {
  return (
    <label htmlFor={htmlFor} className="block text-xs font-medium text-slate-400 mb-1">
      {children}
    </label>
  );
}

export function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <Label>{label}</Label>
      {children}
    </div>
  );
}

export function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`rounded-lg bg-slate-900 border border-slate-800 p-4 ${className}`}>{children}</div>
  );
}

export function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-400 mt-6 mb-2 pb-1 border-b border-slate-800">
      {children}
    </h2>
  );
}

// ---------- Rating ----------

interface RatingInputProps {
  value: number | null | undefined;
  onChange: (next: number | null) => void;
  ariaLabel?: string;
}

/**
 * 1–10 rating selector. Renders 10 numbered buttons; clicking one already
 * selected clears the rating. Server validates 1..10 inclusive.
 */
export function RatingInput({ value, onChange, ariaLabel = 'Personal rating' }: RatingInputProps) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className="flex flex-wrap gap-1.5">
      {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => {
        const selected = value === n;
        return (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={selected}
            aria-label={`${n} of 10`}
            onClick={() => onChange(selected ? null : n)}
            className={`w-11 h-11 rounded-md text-sm font-medium border transition-colors ${
              selected
                ? 'bg-amber-500 border-amber-400 text-slate-900'
                : value != null && n <= value
                  ? 'bg-amber-500/30 border-amber-500/40 text-amber-200'
                  : 'bg-slate-900 border-slate-700 text-slate-300 hover:border-slate-500'
            }`}
          >
            {n}
          </button>
        );
      })}
      {value != null && (
        <button
          type="button"
          onClick={() => onChange(null)}
          className="ml-1 px-3 min-h-[44px] inline-flex items-center text-xs text-slate-400 hover:text-slate-200"
          aria-label="Clear rating"
        >
          clear
        </button>
      )}
    </div>
  );
}

// ---------- Pills ----------

const STATUS_STYLE: Record<CollectionStatus, string> = {
  Owned: 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30',
  Wishlist: 'bg-sky-500/15 text-sky-300 border-sky-500/30',
  OnOrder: 'bg-amber-500/15 text-amber-300 border-amber-500/30',
  Sold: 'bg-slate-500/20 text-slate-300 border-slate-500/30',
};

export function StatusPill({ status }: { status: CollectionStatus }) {
  const label = COLLECTION_STATUSES.find((s) => s.value === status)?.label ?? status;
  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border ${STATUS_STYLE[status]}`}>
      {label}
    </span>
  );
}

export function ConditionPill({ condition }: { condition: Condition }) {
  const label = CONDITIONS.find((c) => c.value === condition)?.label ?? condition;
  return (
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-slate-700/40 text-slate-200 border-slate-600">
      {label}
    </span>
  );
}

// ---------- Tag chips & input ----------

export function TagChip({ name, onRemove }: { name: string; onRemove?: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 pl-2 pr-1 py-0.5 rounded-full text-xs bg-indigo-500/15 text-indigo-200 border border-indigo-500/30">
      {name}
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Remove tag ${name}`}
          className="inline-flex items-center justify-center min-w-[24px] min-h-[24px] rounded-full hover:text-white hover:bg-indigo-500/30"
        >
          ×
        </button>
      )}
    </span>
  );
}

interface TagInputProps {
  value: string[];
  onChange: (next: string[]) => void;
  suggestions?: string[];
  placeholder?: string;
}

/**
 * Free-form tag editor. Press Enter or comma to commit; Backspace on an
 * empty input removes the last tag. Names are lowercased and de-duped on
 * commit. Suggestions, when provided, render as quick-add pills below the
 * input (already-selected ones are filtered out).
 */
export function TagInput({ value, onChange, suggestions = [], placeholder = 'Add tag…' }: TagInputProps) {
  const [draft, setDraft] = useState('');

  const commit = (raw: string) => {
    const clean = raw.trim().toLowerCase();
    if (!clean) return;
    if (value.includes(clean)) {
      setDraft('');
      return;
    }
    onChange([...value, clean]);
    setDraft('');
  };

  const onKey = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      commit(draft);
    } else if (e.key === 'Backspace' && draft === '' && value.length > 0) {
      onChange(value.slice(0, -1));
    }
  };

  const remove = (name: string) => onChange(value.filter((t) => t !== name));

  const remaining = suggestions.filter((s) => !value.includes(s)).slice(0, 8);

  return (
    <div>
      <div className="flex flex-wrap gap-1.5 items-center rounded-md bg-slate-900 border border-slate-700 p-1.5 focus-within:border-indigo-400">
        {value.map((t) => (
          <TagChip key={t} name={t} onRemove={() => remove(t)} />
        ))}
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={onKey}
          onBlur={() => commit(draft)}
          placeholder={value.length === 0 ? placeholder : ''}
          aria-label="Add tag"
          className="flex-1 min-w-[8ch] bg-transparent text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none px-1"
        />
      </div>
      {remaining.length > 0 && (
        <div className="mt-1 flex flex-wrap gap-1">
          <span className="text-xs text-slate-500 mr-1">Existing:</span>
          {remaining.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => commit(s)}
              className="px-2 py-0.5 rounded-full text-xs bg-slate-800 text-slate-300 hover:bg-slate-700 border border-slate-700"
            >
              + {s}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ---------- External ID field ----------

interface ExternalIdFieldProps {
  label: string;
  value: string | null | undefined;
  onChange: (next: string | null) => void;
  /** When set and a value is present, renders a "View ↗" link to `${urlPrefix}${value}`. */
  urlPrefix?: string;
  placeholder?: string;
}

/**
 * Editable input for a third-party identifier (TMDB, IMDB, MusicBrainz,
 * IGDB, …). Surfaces the stored value so users can verify what a lookup
 * populated, manually paste an ID to seed metadata, and jump to the
 * provider's page in a new tab.
 */
export function ExternalIdField({ label, value, onChange, urlPrefix, placeholder }: ExternalIdFieldProps) {
  return (
    <Field label={label}>
      <div className="flex gap-2 items-center">
        <Input
          value={value ?? ''}
          placeholder={placeholder}
          onChange={(e) => onChange(e.target.value || null)}
        />
        {value && urlPrefix && (
          <a
            href={`${urlPrefix}${value}`}
            target="_blank"
            rel="noreferrer"
            className="text-xs text-indigo-300 hover:text-indigo-200 underline whitespace-nowrap"
            aria-label={`Open ${label} on the provider's site`}
          >
            View ↗
          </a>
        )}
      </div>
    </Field>
  );
}

// ---------- Cover preview ----------

interface CoverPreviewProps {
  src?: string | null;
  alt?: string;
  className?: string;
}

/**
 * Renders the items poster / album art / game art as a small thumbnail
 * intended to sit alongside the form fields (no extra row). Clicking the
 * thumbnail opens a fullscreen lightbox; Escape or backdrop-click closes
 * it. Renders nothing when src is null/empty.
 */
export function CoverPreview({ src, alt = '', className = '' }: CoverPreviewProps) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open]);

  if (!src) return null;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={alt ? `${alt} — click to enlarge` : 'Click to enlarge'}
        className={`block ${className}`}
      >
        <img
          src={src}
          alt={alt}
          loading="lazy"
          className="w-full object-contain rounded-md shadow-lg bg-slate-800 cursor-zoom-in hover:opacity-90"
        />
      </button>
      {open && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label={alt || 'Cover preview'}
          onClick={() => setOpen(false)}
          className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4 cursor-zoom-out"
        >
          <img
            src={src}
            alt={alt}
            className="max-h-[90dvh] max-w-full object-contain rounded-md shadow-2xl"
          />
        </div>
      )}
    </>
  );
}
