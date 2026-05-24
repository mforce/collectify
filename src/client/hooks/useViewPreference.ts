import { useState, useCallback } from 'react';
import type { MediaType } from '../services/types';

export type ViewMode = 'list' | 'medium' | 'big';

const STORAGE_KEY_PREFIX = 'collectify:view:';

function read(type: MediaType): ViewMode {
  try {
    return (localStorage.getItem(STORAGE_KEY_PREFIX + type) as ViewMode) ?? 'big';
  } catch {
    return 'big';
  }
}

export function useViewPreference(type: MediaType): [ViewMode, (v: ViewMode) => void] {
  const [mode, setMode] = useState<ViewMode>(read(type));

  const write = useCallback((next: ViewMode) => {
    try {
      localStorage.setItem(STORAGE_KEY_PREFIX + type, next);
    } catch { /* ignore */ }
    setMode(next);
  }, [type]);

  return [mode, write];
}
