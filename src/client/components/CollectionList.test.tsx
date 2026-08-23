import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import CollectionList from './CollectionList';
import type { Movie } from '../services/types';

const FIXTURE_MOVIES: Movie[] = [
  { id: 1, title: 'Inception', formats: 2, status: 'Owned', watchStatus: 'Unwatched', watchCount: 0 },
  { id: 2, title: 'Heat', formats: 1, status: 'Owned', watchStatus: 'Unwatched', watchCount: 0 },
];

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function mockFetch(onBulk?: () => unknown) {
  const spy = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    if (method === 'GET' && url === '/api/movies') {
      return jsonResponse(FIXTURE_MOVIES);
    }
    if (method === 'PATCH' && url === '/api/movies/bulk') {
      return jsonResponse(onBulk ? onBulk() : []);
    }
    throw new Error(`Unexpected fetch: ${method} ${url}`);
  });
  globalThis.fetch = spy as unknown as typeof fetch;
  return spy;
}

function renderList() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <CollectionList
          type="movies"
          title="Movies"
          newPath="/movies/new"
          category="movies"
          renderItem={(m: Movie) => ({ primary: m.title })}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('CollectionList — bulk select + update', () => {
  it('shows the bulk bar with the correct count when a card is selected, and hides it on deselect', async () => {
    mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);

    expect(screen.getByText('1 selected')).toBeInTheDocument();

    await user.click(checkboxes[0]);
    expect(screen.queryByText('1 selected')).not.toBeInTheDocument();
  });

  it('"Select all on current page" toggles every rendered card', async () => {
    mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const selectAll = screen.getByLabelText('Select all on current page');

    await user.click(selectAll);
    expect(screen.getByText('2 selected')).toBeInTheDocument();

    await user.click(selectAll);
    expect(screen.queryByText(/selected/)).not.toBeInTheDocument();
  });

  it('confirming the modal issues PATCH movies/bulk with only the set fields, then refetches the list', async () => {
    const fetchSpy = mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    expect(fetchSpy.mock.calls.filter(([u]) => String(u) === '/api/movies')).toHaveLength(1);

    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);
    await user.click(screen.getByRole('button', { name: 'Edit selected' }));

    await user.click(screen.getByRole('radio', { name: '7 of 10' }));
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => {
      const bulkCall = fetchSpy.mock.calls.find(([u]) => String(u) === '/api/movies/bulk');
      expect(bulkCall).toBeDefined();
    });

    const [, init] = fetchSpy.mock.calls.find(([u]) => String(u) === '/api/movies/bulk')!;
    expect(init?.method).toBe('PATCH');
    expect(JSON.parse(init!.body as string)).toEqual({ ids: [1], updates: { personalRating: 7 } });

    // Modal closes and selection clears.
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument());
    expect(screen.queryByText(/selected/)).not.toBeInTheDocument();

    // The bulk mutation invalidates the list query, triggering a refetch.
    await waitFor(() => {
      expect(fetchSpy.mock.calls.filter(([u]) => String(u) === '/api/movies')).toHaveLength(2);
    });
  });

  it('Cancel closes the modal and sends nothing', async () => {
    const fetchSpy = mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);
    await user.click(screen.getByRole('button', { name: 'Edit selected' }));

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('button', { name: 'Confirm' })).not.toBeInTheDocument();
    expect(fetchSpy.mock.calls.some(([u]) => String(u) === '/api/movies/bulk')).toBe(false);
    // Selection (and thus the bulk bar) survives a cancel — only the modal closes.
    expect(screen.getByText('1 selected')).toBeInTheDocument();
  });
});
