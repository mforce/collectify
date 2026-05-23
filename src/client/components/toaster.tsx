import { useEffect, useState } from 'react';

export type ToastKind = 'success' | 'error' | 'info';

export interface Toast {
  id: number;
  kind: ToastKind;
  message: string;
}

// Module-level so any component can fire a toast without prop-drilling
// a provider. The single <Toaster> mounted at the app root subscribes
// to changes and re-renders the stack. Tests can drive the store
// directly via useToast() inside a render.
let toasts: Toast[] = [];
const listeners = new Set<() => void>();
let nextId = 1;

const SUCCESS_TTL_MS = 2500;
const INFO_TTL_MS = 4000;
const MAX_STACK = 4;

function emit() {
  for (const l of listeners) l();
}

function push(kind: ToastKind, message: string): number {
  const id = nextId++;
  toasts = [...toasts, { id, kind, message }];
  // Trim from the head when the stack overflows so the newest toast is
  // always visible. We don't try to merge duplicates -- a busy save
  // batch should show every result.
  if (toasts.length > MAX_STACK) toasts = toasts.slice(toasts.length - MAX_STACK);
  emit();

  // Errors stay until the user dismisses them; success/info auto-fade.
  if (kind === 'success') setTimeout(() => dismiss(id), SUCCESS_TTL_MS);
  if (kind === 'info') setTimeout(() => dismiss(id), INFO_TTL_MS);

  return id;
}

function dismissImpl(id: number) {
  const before = toasts.length;
  toasts = toasts.filter((t) => t.id !== id);
  if (toasts.length !== before) emit();
}

/**
 * Module-level imperative API. Usable from any module without a hook
 * call -- handy outside React (e.g. interceptors, helpers) and inside
 * tests. The <code>useToast()</code> hook just returns this same
 * object so components and non-components share semantics.
 */
export const toast = {
  success: (message: string) => push('success', message),
  error: (message: string) => push('error', message),
  info: (message: string) => push('info', message),
  dismiss: dismissImpl,
};

/** Re-export for legacy call sites; identical to toast.dismiss. */
export const dismiss = dismissImpl;

/**
 * Test-only: drop every queued toast. Production code should not call
 * this -- toasts auto-dismiss on their own.
 */
export function _resetToasts() {
  toasts = [];
  nextId = 1;
  emit();
}

/** Hook flavour for components that prefer dependency injection. */
export function useToast() {
  return toast;
}

/**
 * Root-mounted toast renderer. Bottom-right on >= sm; bottom-center on
 * mobile so it doesn't fight the on-screen keyboard. Success / info
 * land as role=status (announced politely); errors as role=alert
 * (announced assertively).
 */
export function Toaster() {
  // useState/setTick triggers a re-render whenever the module store
  // changes. Storing the toasts list directly would defeat the
  // module-level singleton because each <Toaster> would get its own
  // copy.
  const [, setTick] = useState(0);

  useEffect(() => {
    const listener = () => setTick((t) => t + 1);
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  if (toasts.length === 0) return null;

  return (
    <div
      // The container is purely structural; per-toast role/aria-live
      // attributes do the announcing.
      className="fixed z-50 bottom-4 right-4 sm:right-6 flex flex-col gap-2 items-end max-w-[calc(100%-2rem)]"
    >
      {toasts.map((t) => (
        <div
          key={t.id}
          role={t.kind === 'error' ? 'alert' : 'status'}
          aria-live={t.kind === 'error' ? 'assertive' : 'polite'}
          className={`flex items-start gap-2 rounded-lg border px-4 py-3 text-sm font-medium min-w-[16rem] max-w-md shadow-sm ${styleFor(t.kind)}`}
        >
          <span className="flex-1 break-words">{t.message}</span>
          <button
            type="button"
            onClick={() => dismiss(t.id)}
            aria-label="Dismiss notification"
            className="opacity-70 hover:opacity-100"
          >
            ×
          </button>
        </div>
      ))}
    </div>
  );
}

function styleFor(kind: ToastKind): string {
  switch (kind) {
    case 'success':
      return 'border-brand bg-brand/5 dark:bg-brand/10 text-text-primary';
    case 'error':
      return 'border-error bg-error/5 dark:bg-red-900/30 text-error';
    case 'info':
      return 'border-border bg-pill-bg text-text-secondary';
  }
}
