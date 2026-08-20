import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import userEvent from '@testing-library/user-event';
import Layout from './Layout';
import { MEDIA } from '../services/mediaRegistry';

// Mock auth hook
vi.mock('../services/auth', () => ({
  useAuth: () => ({ data: { isAuthenticated: true, userName: 'TestUser' } }),
  useLogout: () => ({ mutate: vi.fn() }),
}));

// Mock toaster
vi.mock('./toaster', () => ({
  useToast: () => ({ success: vi.fn(), error: vi.fn() }),
}));

function renderWithRouter(ui: React.ReactNode) {
  return render(<MemoryRouter>{ui}</MemoryRouter>);
}

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

beforeEach(() => {
  document.documentElement.classList.remove('dark');
  vi.clearAllMocks();
  mockLocalStorage.clear();
});

describe('Layout dark mode integration', () => {
  it('derives the active movies desktop class from the media registry', () => {
    const original = MEDIA.movies.theme.navActiveDesktop;
    MEDIA.movies.theme.navActiveDesktop = `${original} registry-proof`;
    try {
      render(<MemoryRouter initialEntries={['/movies']}><Layout><div>Page content</div></Layout></MemoryRouter>);
      expect(screen.getAllByRole('link', { name: 'Movies' })[0]).toHaveClass(...MEDIA.movies.theme.navActiveDesktop.split(' '));
    } finally {
      MEDIA.movies.theme.navActiveDesktop = original;
    }
  });

  it('renders a dark mode toggle button in the header', () => {
    renderWithRouter(<Layout><div>Page content</div></Layout>);

    const toggle = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    expect(toggle).toBeInTheDocument();
  });

  it('dark mode toggle is positioned after user controls on desktop', () => {
    renderWithRouter(<Layout><div>Page content</div></Layout>);

    const userName = screen.getByText('TestUser');
    const logoutBtn = screen.getByRole('button', { name: /sign out/i });
    const darkToggle = screen.getByRole('button', { name: /switch to (light|dark) mode/i });

    // All should be present in the header area
    expect(userName).toBeInTheDocument();
    expect(logoutBtn).toBeInTheDocument();
    expect(darkToggle).toBeInTheDocument();
  });

  it('clicking the toggle applies dark class to document root', async () => {
    renderWithRouter(<Layout><div>Page content</div></Layout>);

    const toggle = screen.getByRole('button', { name: /switch to (light|dark) mode/i });
    await userEvent.click(toggle);

    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
