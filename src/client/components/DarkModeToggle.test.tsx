import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DarkModeToggle from './DarkModeToggle';

// Mock localStorage
const mockLocalStorage = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] ?? null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: mockLocalStorage,
  writable: true,
});

// Mock document.documentElement.classList
const originalClassList = document.documentElement.classList;
beforeEach(() => {
  // Reset class list for each test
  originalClassList.remove('dark');
  vi.clearAllMocks();
  // Clear localStorage store to avoid cross-test pollution
  mockLocalStorage.clear();
});

describe('DarkModeToggle', () => {
  it('renders a toggle button with appropriate aria label', () => {
    render(<DarkModeToggle />);

    const button = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    expect(button).toBeInTheDocument();
  });

  it('shows sun icon in light mode by default', () => {
    render(<DarkModeToggle />);

    // Sun icon should be visible (moon hidden)
    const sunIcon = screen.queryByTestId('sun-icon');
    const moonIcon = screen.queryByTestId('moon-icon');
    
    expect(sunIcon).toBeInTheDocument();
    expect(moonIcon).not.toBeInTheDocument();
  });

  it('toggles to dark mode when clicked', async () => {
    render(<DarkModeToggle />);

    const button = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    await userEvent.click(button);

    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('persists preference to localStorage when toggled', async () => {
    render(<DarkModeToggle />);

    const button = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    await userEvent.click(button);

    expect(mockLocalStorage.setItem).toHaveBeenCalledWith('theme', 'dark');
  });

  it('shows moon icon in dark mode after toggle', async () => {
    render(<DarkModeToggle />);

    const button = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    await userEvent.click(button);

    // Re-render to see updated state
    const sunIcon = screen.queryByTestId('sun-icon');
    const moonIcon = screen.queryByTestId('moon-icon');
    
    expect(sunIcon).not.toBeInTheDocument();
    expect(moonIcon).toBeInTheDocument();
  });

  it('toggles back to light mode when clicked again', async () => {
    render(<DarkModeToggle />);

    const button = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    
    // Toggle to dark
    await userEvent.click(button);
    // Wait for React effect to apply class
    await new Promise((r) => setTimeout(r, 0));
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    
    // Toggle back to light
    await userEvent.click(button);
    await new Promise((r) => setTimeout(r, 0));
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('respects system preference when no localStorage value exists', () => {
    // Clear any stored theme
    mockLocalStorage.clear();
    
    render(<DarkModeToggle />);
    
    // Should default to light mode (no dark class)
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('loads saved preference from localStorage on mount', () => {
    // Pre-set a stored theme
    mockLocalStorage.getItem.mockReturnValueOnce('dark');
    
    render(<DarkModeToggle />);
    
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
