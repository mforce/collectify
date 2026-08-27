import { afterEach, describe, expect, it, vi } from 'vitest';
import { useEffect } from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import CollectionList from './CollectionList';
import type { Album, Game, MediaType, Movie } from '../services/types';

const FIXTURE_MOVIES: Movie[] = [
  { id: 1, title: 'Inception', year: 2010, director: 'Christopher Nolan', addedAt: '2026-08-20T14:30:00Z', personalRating: 9, formats: 2, status: 'Owned', watchStatus: 'Watched', watchCount: 3 },
  { id: 2, title: 'Heat', year: null, director: 'Michael Mann', addedAt: 'not-a-date', personalRating: null, formats: 1, status: 'Owned', watchStatus: 'Unwatched', watchCount: 0 },
];

const FIXTURE_ALBUMS: Album[] = [
  { id: 1, title: 'OK Computer', artistName: 'Radiohead', year: 1997, addedAt: '2026-08-21', personalRating: 8, format: 'Cd', status: 'Owned', listenCount: 12 },
  { id: 2, title: 'Kid A', artistName: 'Radiohead', year: null, personalRating: null, format: 'Cd', status: 'Owned', listenCount: 0 },
];

const FIXTURE_GAMES: Game[] = [
  { id: 1, title: 'Hades', year: 2020, addedAt: '2026-08-22T01:02:03Z', personalRating: 10, platform: 'Pc', digitalStores: 0, status: 'Owned', completionStatus: 'HundredPercent', hoursPlayed: 42.5 },
  { id: 2, title: 'Celeste', year: null, personalRating: null, platform: 'Pc', digitalStores: 0, status: 'Owned', completionStatus: 'NotStarted', hoursPlayed: 0 },
];

const TITLES: Record<MediaType, string> = { movies: 'Movies', music: 'Music', games: 'Games' };

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function pathnameOf(url: string): string {
  return new URL(url, 'http://localhost').pathname;
}

/** Most recent GET call to the given pathname, or undefined if none match. */
function lastCallTo(calls: [input: RequestInfo | URL, init?: RequestInit][], pathname: string) {
  for (let i = calls.length - 1; i >= 0; i--) {
    if (pathnameOf(String(calls[i][0])) === pathname) return calls[i];
  }
  return undefined;
}

function mockFetch(onBulk?: () => unknown) {
  const spy = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    const method = init?.method ?? 'GET';
    const pathname = pathnameOf(url);
    if (method === 'GET' && pathname === '/api/movies') return jsonResponse(FIXTURE_MOVIES);
    if (method === 'GET' && pathname === '/api/music') return jsonResponse(FIXTURE_ALBUMS);
    if (method === 'GET' && pathname === '/api/games') return jsonResponse(FIXTURE_GAMES);
    if (method === 'PATCH' && pathname === '/api/movies/bulk') {
      return jsonResponse(onBulk ? onBulk() : []);
    }
    throw new Error(`Unexpected fetch: ${method} ${url}`);
  });
  globalThis.fetch = spy as unknown as typeof fetch;
  return spy;
}

function LocationObserver({ onLocation }: { onLocation: (location: { key: string; path: string }) => void }) {
  const location = useLocation();
  useEffect(() => {
    onLocation({ key: location.key, path: `${location.pathname}${location.search}` });
  }, [location.key, location.pathname, location.search, onLocation]);
  return null;
}

function renderList(opts?: {
  type?: MediaType;
  initialEntries?: string[];
  onLocation?: (location: { key: string; path: string }) => void;
}) {
  const type = opts?.type ?? 'movies';
  const renderItem = (item: Movie | Album | Game) => {
    if (type === 'movies') {
      const movie = item as Movie;
      return { primary: movie.title, secondary: movie.director, tertiary: 'Blu-ray' };
    }
    if (type === 'music') {
      const album = item as Album;
      return { primary: album.title, secondary: album.artistName, tertiary: 'CD' };
    }
    const game = item as Game;
    return { primary: game.title, secondary: 'PC', tertiary: 'Physical' };
  };
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={opts?.initialEntries ?? ['/']}>
        {opts?.onLocation && <LocationObserver onLocation={opts.onLocation} />}
        <CollectionList
          type={type}
          title={TITLES[type]}
          newPath={`/${type}/new`}
          category={type}
          renderItem={renderItem}
        />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

function metadataPairs(title: string): [string, string][] {
  const card = screen.getByRole('heading', { level: 3, name: title }).closest('a');
  const metadata = card?.querySelector('dl[aria-label="Sortable metadata"]');
  expect(metadata).not.toBeNull();
  const labels = within(metadata as HTMLElement).getAllByRole('term').map((node) => node.textContent ?? '');
  const values = within(metadata as HTMLElement).getAllByRole('definition').map((node) => node.textContent ?? '');
  return labels.map((label, index) => [label, values[index]]);
}

async function selectViewAndAssertMetadata(mode: string, title: string, expected: [string, string][]) {
  const user = userEvent.setup();
  await user.click(screen.getByTitle(mode));
  expect(metadataPairs(title)).toEqual(expected);
  expect(metadataPairs(title).filter(([label]) => label === 'Rating')).toHaveLength(1);
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
    expect(fetchSpy.mock.calls.filter(([u]) => pathnameOf(String(u)) === '/api/movies')).toHaveLength(1);

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
      expect(fetchSpy.mock.calls.filter(([u]) => pathnameOf(String(u)) === '/api/movies')).toHaveLength(2);
    });
  });

  it('exposes a Genres field in the bulk modal, mapped to updates.genres', async () => {
    const fetchSpy = mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);
    await user.click(screen.getByRole('button', { name: 'Edit selected' }));

    expect(screen.getByText('Genres')).toBeInTheDocument();
    await user.type(screen.getByPlaceholderText('Add genre…'), 'Sci-Fi{Enter}');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => {
      const bulkCall = fetchSpy.mock.calls.find(([u]) => String(u) === '/api/movies/bulk');
      expect(bulkCall).toBeDefined();
    });
    const [, init] = fetchSpy.mock.calls.find(([u]) => String(u) === '/api/movies/bulk')!;
    expect(JSON.parse(init!.body as string)).toEqual({ ids: [1], updates: { genres: ['sci-fi'] } });
  });

  it('Confirm is disabled until a field is set, and enables once one is', async () => {
    const fetchSpy = mockFetch();
    renderList();
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);
    await user.click(screen.getByRole('button', { name: 'Edit selected' }));

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    expect(confirmButton).toBeDisabled();

    await user.click(confirmButton);
    expect(fetchSpy.mock.calls.some(([u]) => String(u) === '/api/movies/bulk')).toBe(false);

    await user.click(screen.getByRole('radio', { name: '7 of 10' }));
    expect(confirmButton).not.toBeDisabled();
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

  it('drops a selection that falls out of the result set before a bulk edit is confirmed', async () => {
    // useList is mocked directly (network-driven query-key changes leave
    // `data` transiently undefined, which would mask what this test checks);
    // this gives full control over `list.data` with no loading gap.
    let currentData: Movie[] = FIXTURE_MOVIES;
    const bulkMutate = vi.fn((_vars: unknown, opts?: { onSuccess?: () => void }) => {
      opts?.onSuccess?.();
    });

    vi.doMock('../services/collection', async () => {
      const actual = await vi.importActual<typeof import('../services/collection')>('../services/collection');
      return {
        ...actual,
        useList: () => ({ data: currentData, isLoading: false, error: null }),
        useBulkUpdate: () => ({ mutate: bulkMutate, isPending: false, isError: false, error: null }),
      };
    });
    vi.resetModules();
    const { default: MockedCollectionList } = await import('./CollectionList');

    const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
    const buildElement = () => (
      <QueryClientProvider client={qc}>
        <MemoryRouter>
          <MockedCollectionList
            type="movies"
            title="Movies"
            newPath="/movies/new"
            category="movies"
            renderItem={(m: Movie) => ({ primary: m.title })}
          />
        </MemoryRouter>
      </QueryClientProvider>
    );
    const { rerender } = render(buildElement());
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]); // Inception (id 1)
    await user.click(checkboxes[1]); // Heat (id 2)
    expect(screen.getByText('2 selected')).toBeInTheDocument();

    // Simulate a search/filter change narrowing the result set: Heat (id 2) drops out.
    currentData = FIXTURE_MOVIES.filter((m) => m.title === 'Inception');
    rerender(buildElement());

    await waitFor(() => expect(screen.queryByText('Heat')).not.toBeInTheDocument());
    // The stale selection (Heat) must have been dropped; Inception stays selected.
    expect(screen.getByText('1 selected')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Edit selected' }));
    await user.click(screen.getByRole('radio', { name: '7 of 10' }));
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(bulkMutate).toHaveBeenCalledWith({ ids: [1], updates: { personalRating: 7 } }, expect.any(Object));

    vi.doUnmock('../services/collection');
    vi.resetModules();
  });
});

describe('CollectionList — sort controls', () => {
  it('sort controls expose shared and current-type options', async () => {
    mockFetch();
    const movies = renderList({ type: 'movies' });
    await screen.findByText('Inception');
    const movieOptions = Array.from((screen.getByLabelText('Sort by') as HTMLSelectElement).options).map((o) => o.value);
    expect(movieOptions).toEqual(expect.arrayContaining(['title', 'year', 'addedAt', 'personalRating', 'watchStatus', 'watchCount']));
    expect(movieOptions).not.toEqual(expect.arrayContaining(['listenCount']));
    expect(movieOptions).not.toEqual(expect.arrayContaining(['hoursPlayed']));
    expect(movieOptions).not.toEqual(expect.arrayContaining(['completionStatus']));
    movies.unmount();

    mockFetch();
    const music = renderList({ type: 'music' });
    await screen.findByText('OK Computer');
    const musicOptions = Array.from((screen.getByLabelText('Sort by') as HTMLSelectElement).options).map((o) => o.value);
    expect(musicOptions).toEqual(expect.arrayContaining(['listenCount']));
    expect(musicOptions).not.toEqual(expect.arrayContaining(['watchStatus']));
    expect(musicOptions).not.toEqual(expect.arrayContaining(['watchCount']));
    expect(musicOptions).not.toEqual(expect.arrayContaining(['hoursPlayed']));
    expect(musicOptions).not.toEqual(expect.arrayContaining(['completionStatus']));
    music.unmount();

    mockFetch();
    const games = renderList({ type: 'games' });
    await screen.findByText('Hades');
    const gameOptions = Array.from((screen.getByLabelText('Sort by') as HTMLSelectElement).options).map((o) => o.value);
    expect(gameOptions).toEqual(expect.arrayContaining(['hoursPlayed', 'completionStatus']));
    expect(gameOptions).not.toEqual(expect.arrayContaining(['watchStatus']));
    expect(gameOptions).not.toEqual(expect.arrayContaining(['watchCount']));
    expect(gameOptions).not.toEqual(expect.arrayContaining(['listenCount']));
    games.unmount();
  });

  it('changing sort field or direction updates URL and refetches canonical request', async () => {
    const fetchSpy = mockFetch();
    renderList({ type: 'movies', initialEntries: ['/?q=heat&director=Nolan'] });
    const user = userEvent.setup();

    await screen.findByText('Inception');

    await user.selectOptions(screen.getByLabelText('Sort by'), 'year');

    await waitFor(() => {
      const call = lastCallTo(fetchSpy.mock.calls, '/api/movies');
      expect(call).toBeDefined();
      const params = new URL(String(call![0]), 'http://localhost').searchParams;
      expect(params.get('query')).toBe('heat');
      expect(params.get('director')).toBe('Nolan');
      expect(params.get('sort')).toBe('year');
      expect(params.get('dir')).toBe('desc');
    });

    await user.selectOptions(screen.getByLabelText('Direction'), 'asc');

    await waitFor(() => {
      const call = lastCallTo(fetchSpy.mock.calls, '/api/movies');
      const params = new URL(String(call![0]), 'http://localhost').searchParams;
      expect(params.get('query')).toBe('heat');
      expect(params.get('director')).toBe('Nolan');
      expect(params.get('sort')).toBe('year');
      expect(params.get('dir')).toBe('asc');
    });
  });

  it('initial URL selects controls and drives first request', async () => {
    const fetchSpy = mockFetch();
    renderList({ type: 'movies', initialEntries: ['/?sort=personalRating&dir=asc'] });

    await screen.findByText('Inception');

    expect((screen.getByLabelText('Sort by') as HTMLSelectElement).value).toBe('personalRating');
    expect((screen.getByLabelText('Direction') as HTMLSelectElement).value).toBe('asc');

    const call = fetchSpy.mock.calls.find(([u]) => pathnameOf(String(u)) === '/api/movies');
    expect(call).toBeDefined();
    const params = new URL(String(call![0]), 'http://localhost').searchParams;
    expect(params.get('sort')).toBe('personalRating');
    expect(params.get('dir')).toBe('asc');
  });

  it('invalid URL is replaced once with defaults without a second fetch loop', async () => {
    const fetchSpy = mockFetch();
    const locations: { key: string; path: string }[] = [];
    renderList({
      type: 'movies',
      initialEntries: ['/?q=heat&sort=bogus'],
      onLocation: (location) => locations.push(location),
    });

    await screen.findByText('Inception');

    await waitFor(() => {
      expect((screen.getByLabelText('Sort by') as HTMLSelectElement).value).toBe('addedAt');
    });
    expect((screen.getByLabelText('Direction') as HTMLSelectElement).value).toBe('desc');

    await waitFor(() => expect(locations).toHaveLength(2));
    expect(new Set(locations.map(({ key }) => key)).size).toBe(2);
    expect(locations.map(({ path }) => path)).toEqual([
      '/?q=heat&sort=bogus',
      '/?q=heat&sort=addedAt&dir=desc',
    ]);

    const movieCalls = fetchSpy.mock.calls.filter(([u]) => pathnameOf(String(u)) === '/api/movies');
    expect(movieCalls).toHaveLength(1);
    const params = new URL(String(movieCalls[0][0]), 'http://localhost').searchParams;
    expect(params.get('sort')).toBe('addedAt');
    expect(params.get('dir')).toBe('desc');
  });

  it('sorting a stable result preserves selected IDs', async () => {
    let order: number[] = [1, 2];
    const spy = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      if (method === 'GET' && pathnameOf(url) === '/api/movies') {
        const byId = new Map(FIXTURE_MOVIES.map((m) => [m.id, m]));
        return jsonResponse(order.map((id) => byId.get(id)));
      }
      throw new Error(`Unexpected fetch: ${method} ${url}`);
    });
    globalThis.fetch = spy as unknown as typeof fetch;

    renderList({ type: 'movies' });
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]); // Inception (id 1).
    expect(screen.getByText('1 selected')).toBeInTheDocument();

    // Same IDs, reverse order: a refetch triggered by a direction change
    // must not treat the reordering as a membership change.
    order = [2, 1];
    await user.selectOptions(screen.getByLabelText('Direction'), 'asc');

    await waitFor(() => {
      expect(spy.mock.calls.filter(([u]) => pathnameOf(String(u)) === '/api/movies')).toHaveLength(2);
    });
    await waitFor(() => {
      const titles = screen.getAllByRole('heading', { level: 3 }).map((heading) => heading.textContent);
      expect(titles).toEqual(['Heat', 'Inception']);
    });
    expect(screen.getByText('1 selected')).toBeInTheDocument();
  });

  it('membership change still prunes missing selected IDs', async () => {
    let narrowed = false;
    const spy = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      const method = init?.method ?? 'GET';
      if (method === 'GET' && pathnameOf(url) === '/api/movies') {
        return jsonResponse(narrowed ? [FIXTURE_MOVIES[1]] : FIXTURE_MOVIES);
      }
      throw new Error(`Unexpected fetch: ${method} ${url}`);
    });
    globalThis.fetch = spy as unknown as typeof fetch;

    renderList({ type: 'movies' });
    const user = userEvent.setup();

    await screen.findByText('Inception');
    const checkboxes = screen.getAllByLabelText('Select item');
    await user.click(checkboxes[0]);
    await user.click(checkboxes[1]);
    expect(screen.getByText('2 selected')).toBeInTheDocument();

    narrowed = true;
    await user.type(screen.getByPlaceholderText('Search movies…'), 'h');

    await waitFor(() => {
      expect(spy.mock.calls.filter(([u]) => pathnameOf(String(u)) === '/api/movies')).toHaveLength(2);
    });
    await waitFor(() => {
      expect(screen.getByText('Heat')).toBeInTheDocument();
      expect(screen.queryByText('Inception')).not.toBeInTheDocument();
    });
    expect(screen.getByText('1 selected')).toBeInTheDocument();
  });
});

describe('CollectionList — sortable metadata', () => {
  it.each(['List', 'Medium', 'Big'])('shows movie sortable values in %s view', async (mode) => {
    mockFetch();
    renderList({ type: 'movies' });
    await screen.findByText('Inception');

    await selectViewAndAssertMetadata(mode, 'Inception', [
      ['Year', '2010'],
      ['Date added', 'Aug 20, 2026'],
      ['Rating', '9/10'],
      ['Watch status', 'Watched'],
      ['Watch count', '3'],
    ]);
    expect(metadataPairs('Heat')).toEqual([
      ['Year', '—'],
      ['Date added', '—'],
      ['Rating', '—'],
      ['Watch status', 'Unwatched'],
      ['Watch count', '0'],
    ]);
    expect(screen.getByText('Christopher Nolan')).toBeInTheDocument();
    expect(screen.getAllByText('Blu-ray').length).toBeGreaterThan(0);
  });

  it.each(['List', 'Medium', 'Big'])('shows only music sortable values in %s view', async (mode) => {
    mockFetch();
    renderList({ type: 'music' });
    await screen.findByText('OK Computer');

    await selectViewAndAssertMetadata(mode, 'OK Computer', [
      ['Year', '1997'],
      ['Date added', 'Aug 21, 2026'],
      ['Rating', '8/10'],
      ['Listen count', '12'],
    ]);
    expect(metadataPairs('Kid A')).toEqual([
      ['Year', '—'],
      ['Date added', '—'],
      ['Rating', '—'],
      ['Listen count', '0'],
    ]);
    expect(metadataPairs('OK Computer').map(([label]) => label)).not.toEqual(
      expect.arrayContaining(['Watch status', 'Watch count', 'Hours played', 'Completion status']),
    );
    expect(screen.getAllByText('Radiohead').length).toBeGreaterThan(0);
    expect(screen.getAllByText('CD').length).toBeGreaterThan(0);
  });

  it.each(['List', 'Medium', 'Big'])('shows only game sortable values in %s view', async (mode) => {
    mockFetch();
    renderList({ type: 'games' });
    await screen.findByText('Hades');

    await selectViewAndAssertMetadata(mode, 'Hades', [
      ['Year', '2020'],
      ['Date added', 'Aug 22, 2026'],
      ['Rating', '10/10'],
      ['Hours played', '42.5h'],
      ['Completion status', '100%'],
    ]);
    expect(metadataPairs('Celeste')).toEqual([
      ['Year', '—'],
      ['Date added', '—'],
      ['Rating', '—'],
      ['Hours played', '0h'],
      ['Completion status', 'Not started'],
    ]);
    expect(metadataPairs('Hades').map(([label]) => label)).not.toEqual(
      expect.arrayContaining(['Watch status', 'Watch count', 'Listen count']),
    );
    expect(screen.getAllByText('PC').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Physical').length).toBeGreaterThan(0);
  });
});
