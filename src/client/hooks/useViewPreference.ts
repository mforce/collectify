import { useState, useCallback } from 'react';
import type { MediaType } from '../services/types';

export type ViewMode = 'list' | 'medium' | 'big';

const VALID_MODES: ViewMode[] = ['list', 'medium', 'big'];
const STORAGE_KEY_PREFIX = 'collectify:view:';

function read(key: string): ViewMode {
  try {
    const raw = localStorage.getItem(STORAGE_KEY_PREFIX + key);
    if (raw && VALID_MODES.includes(raw as ViewMode)) return raw as ViewMode;
  } catch { /* ignore */ }
  return 'big';
}

export function useViewPreference(key: MediaType | 'dashboard'): [ViewMode, (v: ViewMode) => void] {
  const [mode, setMode] = useState<ViewMode>(read(String(key)));

  const write = useCallback((next: ViewMode) => {
    try {
      localStorage.setItem(STORAGE_KEY_PREFIX + String(key), next);
    } catch { /* ignore */ }
    setMode(next);
  }, [key]);

  return [mode, write];
}
