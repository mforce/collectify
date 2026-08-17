import { useEffect, useRef, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type KeyboardEvent, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import {
  collectionStatusLabel,
  conditionLabel,
  type CollectionStatus,
  type Condition,
} from '../services/types';

// ─── Button ──────────────────────────────────────────────────────

const btnBase =
  'inline-flex min-h-[44px] items-center justify-center rounded-xl transition-colors text-sm font-bold disabled:opacity-40 disabled:pointer-events-none focus:outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-2 focus-visible:ring-offset-surface';

export function Button({ className = '', variant = 'primary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }) {
  const variants: Record<string, string> = {
    primary: 'bg-brand hover:bg-brand-hover text-white px-5 py-2 shadow-sm shadow-brand/20',
    secondary: 'bg-card hover:bg-pill-bg text-text-primary border border-border px-5 py-2',
    danger: 'bg-error hover:bg-red-600 text-white px-5 py-2',
  };
  return (
    <button {...props} className={`${btnBase} ${variants[variant]} ${className}`} />
  );
}

// ─── Input / Select / Textarea ──────────────────────────────────

const inputBase =
  'block min-h-[44px] w-full rounded-xl border px-3 py-2 text-sm bg-input-bg transition-colors placeholder:text-input-placeholder focus:outline-none focus:border-brand focus:ring-2 focus:ring-brand/20 disabled:opacity-40 disabled:bg-pill-bg text-text-primary';

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input {...props} className={`${inputBase} border-border ${className}`} />
  );
}

export function Textarea({ className = '', ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea {...props} className={`${inputBase} border-border resize-y ${className}`} />
  );
}

export function Select({ className = '', ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select {...props} className={`${inputBase} border-border ${className}`} />
  );
}

// ─── Searchable select ──────────────────────────────────────────

export interface SearchableOption {
  value: string;
  label: string;
  group?: string;
}

interface SearchableSelectProps {
  value: string;
  onChange: (next: string) => void;
  options: SearchableOption[];
  placeholder?: string;
  id?: string;
}

export function SearchableSelect({
  value,
  onChange,
  options,
  placeholder = 'Pick one…',
  id,
}: SearchableSelectProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
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

  const rendered: { kind: 'header' | 'option'; text: string; flatIndex?: number; opt?: SearchableOption }[] = [];
  let lastGroup: string | undefined = undefined;
  filtered.forEach((opt, i) => {
    if (opt.group && opt.group !== lastGroup) {
      rendered.push({ kind: 'header', text: opt.group });
      lastGroup = opt.group;
    } else if (!opt.group && lastGroup !== undefined) {
      lastGroup = undefined;
    }
    rendered.push({ kind: 'option', text: opt.label, flatIndex: i, opt });
  });

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
        className={`${inputBase} border-border`}
      />
      {open && (
        <div
          id={id ? `${id}-listbox` : undefined}
          role="listbox"
          className="absolute z-20 mt-2 max-h-72 w-full overflow-auto rounded-xl border border-border bg-input-bg shadow-card"
        >
          {filtered.length === 0 && (
            <div className="px-3 py-2 text-sm text-text-secondary">No matches.</div>
          )}
          {rendered.map((row, idx) => {
            if (row.kind === 'header') {
              return (
                <div
                  key={`h-${row.text}-${idx}`}
                  className="px-3 py-1 text-xs font-medium uppercase tracking-wide text-text-tertiary border-b border-border"
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
                  e.preventDefault();
                  commit(row.opt!);
                }}
                className={`w-full text-left px-3 py-2 text-sm border-b last:border-b-0 flex items-center justify-between transition-colors ${
                  isActive ? 'category-active-soft' : 'text-text-secondary category-hover-soft'
                }`}
              >
                <span>{row.text}</span>
                {isSelected && <span aria-hidden className="category-text text-xs">✓</span>}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ─── Label / Field ──────────────────────────────────────────────

export function Label({ children, htmlFor }: { children: React.ReactNode; htmlFor?: string }) {
  return (
    <label htmlFor={htmlFor} className="mb-1 block text-xs font-bold text-text-secondary">
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

// ─── Card ────────────────────────────────────────────────────────

export function Card({ children, className = '' }: { children: React.ReactNode; className?: string }) {
  return (
    <div className={`rounded-lg border border-border bg-card p-4 shadow-card ${className}`}>{children}</div>
  );
}

// ─── Section heading ─────────────────────────────────────────────

export function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="mb-2 mt-6 border-b border-border pb-1 text-xs font-bold uppercase tracking-wide text-text-tertiary">
      {children}
    </h2>
  );
}

// ─── Rating ──────────────────────────────────────────────────────

interface RatingInputProps {
  value: number | null | undefined;
  onChange: (next: number | null) => void;
  ariaLabel?: string;
}

const CAT_RATING_ACTIVE: Record<string, string> = {
  movies: 'bg-movies border-movies text-white',
  music: 'bg-music border-music text-white',
  games: 'bg-games border-games text-white',
};

const CAT_RATING_FILLED: Record<string, string> = {
  movies: 'bg-movies/20 border-movies/30 text-movies',
  music: 'bg-music/20 border-music/30 text-music',
  games: 'bg-games/20 border-games/30 text-games',
};

const CAT_RATING_CLEAR: Record<string, string> = {
  movies: 'border-movies/40 text-movies hover:border-movies',
  music: 'border-music/40 text-music hover:border-music',
  games: 'border-games/40 text-games hover:border-games',
};

export function RatingInput({ value, onChange, ariaLabel = 'Personal rating', category }: RatingInputProps & { category?: string }) {
  return (
    <div role="radiogroup" aria-label={ariaLabel} className="flex flex-wrap gap-1.5">
      {Array.from({ length: 10 }, (_, i) => i + 1).map((n) => {
        const selected = value === n;
        const activeClass = category && CAT_RATING_ACTIVE[category] ? CAT_RATING_ACTIVE[category] : 'bg-brand border-brand text-white';
        const filledClass = category && CAT_RATING_FILLED[category] ? CAT_RATING_FILLED[category] : 'bg-brand/20 border-brand/30 text-brand';
        const clearClass = category && CAT_RATING_CLEAR[category] ? CAT_RATING_CLEAR[category] : 'border-border text-text-secondary hover:border-gray-400';
        return (
          <button
            key={n}
            type="button"
            role="radio"
            aria-checked={selected}
            aria-label={`${n} of 10`}
            onClick={() => onChange(selected ? null : n)}
            className={`h-9 w-9 rounded-xl border text-sm font-bold transition-colors ${
              selected
                ? activeClass
                : value != null && n <= value
                  ? filledClass
                  : clearClass
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
          className="ml-1 px-2 py-1 text-xs text-text-secondary hover:text-text-primary transition-colors"
          aria-label="Clear rating"
        >
          clear
        </button>
      )}
    </div>
  );
}

// ─── Pills ───────────────────────────────────────────────────────

const STATUS_STYLE: Record<CollectionStatus, string> = {
  Owned: 'bg-pill-bg text-text-secondary border-border',
  Wishlist: 'bg-pill-bg text-text-secondary border-border',
  OnOrder: 'bg-pill-bg text-text-secondary border-border',
  Sold: 'bg-pill-bg text-text-tertiary border-border',
};

const CAT_STATUS_STYLE: Record<string, Record<CollectionStatus, string>> = {
  movies: {
    Owned: 'bg-movies-light text-movies border-movies-border',
    Wishlist: 'bg-movies-light/60 text-movies/70 border-movies-border',
    OnOrder: 'bg-movies-light text-movies border-movies-border',
    Sold: 'bg-pill-bg text-text-tertiary border-border',
  },
  music: {
    Owned: 'bg-music-light text-music border-music-border',
    Wishlist: 'bg-music-light/60 text-music/70 border-music-border',
    OnOrder: 'bg-music-light text-music border-music-border',
    Sold: 'bg-pill-bg text-text-tertiary border-border',
  },
  games: {
    Owned: 'bg-games-light text-games border-games-border',
    Wishlist: 'bg-games-light/60 text-games/70 border-games-border',
    OnOrder: 'bg-games-light text-games border-games-border',
    Sold: 'bg-pill-bg text-text-tertiary border-border',
  },
};

export function StatusPill({ status, category }: { status: CollectionStatus; category?: string }) {
  const label = collectionStatusLabel(status) ?? status;
  const style = category && CAT_STATUS_STYLE[category] ? CAT_STATUS_STYLE[category][status] : STATUS_STYLE[status];
  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${style}`}>
      {label}
    </span>
  );
}

export function ConditionPill({ condition }: { condition: Condition }) {
  const label = conditionLabel(condition) ?? condition;
  return (
    <span className="inline-flex items-center rounded-full border border-border bg-pill-bg px-2 py-0.5 text-xs font-semibold text-text-secondary">
      {label}
    </span>
  );
}

// ─── Tag chips & input ──────────────────────────────────────────

const CAT_TAG_STYLE: Record<string, string> = {
  movies: 'bg-movies-light text-movies border-movies-border hover:bg-movies/20',
  music: 'bg-music-light text-music border-music-border hover:bg-music/20',
  games: 'bg-games-light text-games border-games-border hover:bg-games/20',
};

export function TagChip({ name, category, onRemove }: { name: string; category?: string; onRemove?: () => void }) {
  const style = category && CAT_TAG_STYLE[category] ? CAT_TAG_STYLE[category] : 'bg-brand/10 text-brand border-brand/20 hover:bg-brand/20';
  return (
    <span className={`inline-flex items-center gap-1 rounded-full border py-0.5 pl-2 pr-1 text-xs font-semibold ${style}`}>
      {name}
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Remove tag ${name}`}
          className="inline-flex items-center justify-center min-w-[24px] min-h-[24px] rounded-full hover:bg-black/10 dark:hover:bg-white/10 transition-colors"
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
  category?: string;
}

export function TagInput({ value, onChange, suggestions = [], placeholder = 'Add tag…', category }: TagInputProps) {
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
      <div className="category-focus-within flex flex-wrap items-center gap-1.5 rounded-xl border border-border bg-card p-1.5 transition-colors focus-within:border-brand focus-within:ring-2 focus-within:ring-brand/20">
        {value.map((t) => (
          <TagChip key={t} name={t} category={category} onRemove={() => remove(t)} />
        ))}
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={onKey}
          onBlur={() => commit(draft)}
          placeholder={value.length === 0 ? placeholder : ''}
          aria-label="Add tag"
          className="flex-1 min-w-[8ch] bg-transparent text-sm text-text-primary placeholder:text-text-tertiary focus:outline-none px-1"
        />
      </div>
      {remaining.length > 0 && (
        <div className="mt-1 flex flex-wrap gap-1">
          <span className="text-xs text-text-tertiary mr-1">Existing:</span>
          {remaining.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => commit(s)}
              className="rounded-full border border-border bg-pill-bg px-2 py-0.5 text-xs text-text-secondary transition-colors hover:bg-card hover:text-text-primary"
            >
              + {s}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── External ID field ──────────────────────────────────────────

interface ExternalIdFieldProps {
  label: string;
  value: string | null | undefined;
  onChange: (next: string | null) => void;
  urlPrefix?: string;
  placeholder?: string;
}

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
            className="whitespace-nowrap text-xs font-semibold text-brand underline transition-colors hover:text-brand-hover"
            aria-label={`Open ${label} on the provider's site`}
          >
            View ↗
          </a>
        )}
      </div>
    </Field>
  );
}

// ─── Cover preview ──────────────────────────────────────────────

interface CoverPreviewProps {
  src?: string | null;
  alt?: string;
  className?: string;
}

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
          className="w-full cursor-zoom-in rounded-lg border border-border bg-imgPlaceholder object-contain transition-opacity hover:opacity-90"
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
            className="max-h-[90dvh] max-w-full rounded-lg object-contain"
          />
        </div>
      )}
    </>
  );
}

// ─── View switcher ──────────────────────────────────────────────

import type { ViewMode } from '../hooks/useViewPreference';

const VIEW_MODES: { value: ViewMode; label: string; icon: React.ReactNode }[] = [
  {
    value: 'list',
    label: 'List',
    icon: (
      <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" className="w-4 h-4">
        <rect x="2" y="2" width="3" height="3" rx="0.5" />
        <line x1="6" y1="3.5" x2="14" y2="3.5" />
        <rect x="2" y="7" width="3" height="3" rx="0.5" />
        <line x1="6" y1="8.5" x2="14" y2="8.5" />
        <rect x="2" y="12" width="3" height="3" rx="0.5" />
        <line x1="6" y1="13.5" x2="14" y2="13.5" />
      </svg>
    ),
  },
  {
    value: 'medium',
    label: 'Medium',
    icon: (
      <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" className="w-4 h-4">
        <rect x="2" y="2" width="5" height="5" rx="1" />
        <rect x="9" y="2" width="5" height="5" rx="1" />
        <rect x="2" y="9" width="5" height="5" rx="1" />
        <rect x="9" y="9" width="5" height="5" rx="1" />
      </svg>
    ),
  },
  {
    value: 'big',
    label: 'Big',
    icon: (
      <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" className="w-4 h-4">
        <rect x="2" y="2" width="12" height="12" rx="1.5" />
      </svg>
    ),
  },
];

export function ViewSwitcher({ value, onChange }: { value: ViewMode; onChange: (v: ViewMode) => void }) {
  return (
    <div className="inline-flex overflow-hidden rounded-xl border border-border bg-card">
      {VIEW_MODES.map((mode, i) => {
        const active = mode.value === value;
        return (
          <button
            key={mode.value}
            type="button"
            aria-pressed={active}
            title={mode.label}
            onClick={() => onChange(mode.value)}
            className={`inline-flex items-center justify-center w-9 h-8 transition-colors ${
              active ? 'category-active-soft' : 'bg-card text-text-secondary category-hover-soft hover:text-text-primary'
            } ${i > 0 ? 'border-l border-border' : ''}`}
          >
            {mode.icon}
          </button>
        );
      })}
    </div>
  );
}
