import { useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type KeyboardEvent, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
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
      className={`inline-flex items-center justify-center rounded-md px-3 py-2 text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed ${variants[variant]} ${className}`}
    />
  );
}

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={`block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:outline-none focus:border-indigo-400 ${className}`}
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
      className={`block w-full rounded-md bg-slate-900 border border-slate-700 px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-indigo-400 ${className}`}
    />
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
    <div role="radiogroup" aria-label={ariaLabel} className="flex flex-wrap gap-1">
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
            className={`w-8 h-8 rounded-md text-sm font-medium border transition-colors ${
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
          className="ml-1 px-2 text-xs text-slate-400 hover:text-slate-200"
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
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-indigo-500/15 text-indigo-200 border border-indigo-500/30">
      {name}
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Remove tag ${name}`}
          className="-mr-0.5 hover:text-white"
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
}

/**
 * Renders the items poster / album art / game art as a small floated
 * thumbnail in the top-right of the form, so the surrounding fields can
 * flow around it instead of being pushed below a wide hero image. When
 * src is null/empty (e.g. a brand-new item that has no cover yet) the
 * component renders nothing.
 */
export function CoverPreview({ src, alt = "" }: CoverPreviewProps) {
  if (!src) return null;
  return (
    <img
      src={src}
      alt={alt}
      loading="lazy"
      className="float-right ml-4 mb-2 w-28 sm:w-36 object-contain rounded-md shadow-lg bg-slate-800"
    />
  );
}
