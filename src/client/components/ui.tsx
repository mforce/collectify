import { useEffect, useRef, useState, type ButtonHTMLAttributes, type InputHTMLAttributes, type KeyboardEvent, type SelectHTMLAttributes, type TextareaHTMLAttributes } from 'react';
import {
  COLLECTION_STATUSES,
  CONDITIONS,
  type CollectionStatus,
  type Condition,
} from '../services/types';

// ─── Button ──────────────────────────────────────────────────────

const btnBase =
  'inline-flex items-center justify-center rounded transition-colors text-sm font-medium disabled:opacity-40 disabled:pointer-events-none focus:outline-none focus-visible:ring-2 focus-visible:ring-brand focus-visible:ring-offset-1';

export function Button({ className = '', variant = 'primary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'danger' }) {
  const variants: Record<string, string> = {
    primary: 'bg-brand hover:bg-brand-hover text-white min-h-[40px] px-4 py-2',
    secondary: 'bg-white hover:bg-gray-50 text-text-primary border border-border min-h-[40px] px-4 py-2',
    danger: 'bg-error hover:bg-red-600 text-white min-h-[40px] px-4 py-2',
  };
  return (
    <button {...props} className={`${btnBase} ${variants[variant]} ${className}`} />
  );
}

// ─── Input / Select / Textarea ──────────────────────────────────

const inputBase =
  'block w-full rounded border px-3 py-2 text-sm bg-white transition-colors placeholder:text-text-tertiary focus:outline-none focus:border-brand focus:ring-1 focus:ring-brand disabled:opacity-40 disabled:bg-gray-50';

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
          className="absolute z-20 mt-1 w-full rounded border bg-white border-border max-h-72 overflow-auto"
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
                className={`w-full text-left px-3 py-2 text-sm border-b border-border last:border-b-0 flex items-center justify-between transition-colors ${
                  isActive ? 'bg-gray-50 text-text-primary' : 'text-text-secondary hover:bg-gray-50'
                }`}
              >
                <span>{row.text}</span>
                {isSelected && <span aria-hidden className="text-brand text-xs">✓</span>}
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
    <label htmlFor={htmlFor} className="block text-xs font-medium text-text-secondary mb-1">
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
    <div className={`rounded border border-border bg-white p-4 ${className}`}>{children}</div>
  );
}

// ─── Section heading ─────────────────────────────────────────────

export function SectionHeading({ children }: { children: React.ReactNode }) {
  return (
    <h2 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mt-6 mb-2 pb-1 border-b border-border">
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
            className={`w-9 h-9 rounded text-sm font-medium border transition-colors ${
              selected
                ? 'bg-brand border-brand text-white'
                : value != null && n <= value
                  ? 'bg-brand/20 border-brand/30 text-brand'
                  : 'bg-white border-border text-text-secondary hover:border-gray-400'
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
  Owned: 'bg-brand/5 text-brand border-brand/20',
  Wishlist: 'bg-gray-100 text-text-secondary border-border',
  OnOrder: 'bg-brand/10 text-brand border-brand/30',
  Sold: 'bg-gray-50 text-text-tertiary border-border',
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
    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-gray-50 text-text-secondary border-border">
      {label}
    </span>
  );
}

// ─── Tag chips & input ──────────────────────────────────────────

export function TagChip({ name, onRemove }: { name: string; onRemove?: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 pl-2 pr-1 py-0.5 rounded-full text-xs bg-brand/10 text-brand border border-brand/20">
      {name}
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`Remove tag ${name}`}
          className="inline-flex items-center justify-center min-w-[24px] min-h-[24px] rounded-full hover:bg-brand/20 transition-colors"
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
      <div className="flex flex-wrap gap-1.5 items-center rounded border border-border bg-white p-1.5 focus-within:border-brand transition-colors">
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
              className="px-2 py-0.5 rounded-full text-xs bg-gray-50 text-text-secondary hover:bg-gray-100 border border-border transition-colors"
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
            className="text-xs text-brand hover:text-brand-hover underline whitespace-nowrap transition-colors"
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
          className="w-full object-contain rounded border border-border bg-gray-50 cursor-zoom-in hover:opacity-90 transition-opacity"
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
            className="max-h-[90dvh] max-w-full object-contain rounded"
          />
        </div>
      )}
    </>
  );
}
