import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import MovieForm from './MovieForm';

// Swap the data layer so the form's "Fetch metadata" buttons can be exercised
// without a backend or a TanStack Query refetch dance.
const mockLookupMovieById = vi.fn();
const mockLookupMovieByImdbId = vi.fn();
const mockUseLookup = vi.fn().mockReturnValue({ data: undefined, isLoading: false, error: null });
vi.mock('../services/lookup', async (importOriginal) => {
  const original = await importOriginal<typeof import('../services/lookup')>();
  return {
    ...original,
    lookupMovieById: (id: string) => mockLookupMovieById(id),
    lookupMovieByImdbId: (id: string) => mockLookupMovieByImdbId(id),
    useLookup: () => mockUseLookup(),
  };
});
vi.mock('../services/tags', () => ({
  useTags: () => ({ data: [], isLoading: false, error: null }),
}));

function renderForm(onSubmit = vi.fn(), props: Partial<Parameters<typeof MovieForm>[0]> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <MovieForm onSubmit={onSubmit} {...props} />
    </QueryClientProvider>,
  );
  return { onSubmit };
}

describe('MovieForm — Fetch metadata by TMDB ID', () => {
  beforeEach(() => {
    mockLookupMovieById.mockReset();
    mockLookupMovieByImdbId.mockReset();
    mockUseLookup.mockReset();
    mockUseLookup.mockReturnValue({ data: undefined, isLoading: false, error: null });
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('disables the fetch button when the TMDB ID field is empty', () => {
    renderForm();
    const button = screen.getByRole('button', { name: /fetch metadata by tmdb id/i });
    expect(button).toBeDisabled();
  });

  it('populates form fields when the lookup returns a found result', async () => {
    mockLookupMovieById.mockResolvedValue({
      kind: 'found',
      result: {
        provider: 'tmdb',
        providerKey: '27205',
        title: 'Inception',
        originalTitle: 'Inception',
        year: 2010,
        director: 'Christopher Nolan',
        runtimeMinutes: 148,
        description: 'A heist on the subconscious.',
        imageUrl: 'https://image.tmdb.org/t/p/w342/poster.jpg',
        genres: null,
      },
    });

    renderForm();
    const user = userEvent.setup();

    const tmdbInput = screen.getByPlaceholderText('e.g. 27205');
    await user.type(tmdbInput, '27205');
    await user.click(screen.getByRole('button', { name: /fetch metadata by tmdb id/i }));

    // Director / Year / Runtime are unique values; Inception fills both
    // Title and Original title so it appears twice.
    await waitFor(() => {
      expect(screen.getAllByDisplayValue('Inception')).toHaveLength(2);
    });
    expect(screen.getByDisplayValue('Christopher Nolan')).toBeInTheDocument();
    expect(screen.getByDisplayValue('2010')).toBeInTheDocument();
    expect(screen.getByDisplayValue('148')).toBeInTheDocument();
    expect(mockLookupMovieById).toHaveBeenCalledWith('27205');
  });

  it('shows a hint when the provider reports not-configured and leaves the title empty', async () => {
    mockLookupMovieById.mockResolvedValue({ kind: 'not-configured' });

    renderForm();
    const user = userEvent.setup();
    const tmdbInput = screen.getByPlaceholderText('e.g. 27205');
    await user.type(tmdbInput, '27205');
    await user.click(screen.getByRole('button', { name: /fetch metadata by tmdb id/i }));

    expect(await screen.findByText(/not configured/i)).toBeInTheDocument();
    // No movie title was injected.
    expect(screen.queryByDisplayValue('Inception')).not.toBeInTheDocument();
  });

  it('shows a not-found hint when the lookup misses', async () => {
    mockLookupMovieById.mockResolvedValue({ kind: 'not-found' });

    renderForm();
    const user = userEvent.setup();
    const tmdbInput = screen.getByPlaceholderText('e.g. 27205');
    await user.type(tmdbInput, '9999999');
    await user.click(screen.getByRole('button', { name: /fetch metadata by tmdb id/i }));

    expect(await screen.findByText(/no movie with tmdb id 9999999/i)).toBeInTheDocument();
  });

  it('IMDB Fetch populates form fields when the lookup returns a found result', async () => {
    mockLookupMovieByImdbId.mockResolvedValue({
      kind: 'found',
      result: {
        provider: 'tmdb',
        providerKey: '603',
        title: 'The Matrix',
        originalTitle: 'The Matrix',
        year: 1999,
        director: 'Lana Wachowski & Lilly Wachowski',
        runtimeMinutes: 136,
        description: 'A hacker discovers the truth.',
        imageUrl: 'https://image.tmdb.org/t/p/w342/matrix.jpg',
        genres: null,
      },
    });

    renderForm();
    const user = userEvent.setup();

    const imdbInput = screen.getByPlaceholderText('e.g. tt1375666');
    await user.type(imdbInput, 'tt0133093');
    await user.click(screen.getByRole('button', { name: /fetch metadata by imdb id/i }));

    await waitFor(() => {
      expect(screen.getAllByDisplayValue('The Matrix')).toHaveLength(2);
    });
    expect(screen.getByDisplayValue('Lana Wachowski & Lilly Wachowski')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1999')).toBeInTheDocument();
    expect(screen.getByDisplayValue('136')).toBeInTheDocument();
    expect(mockLookupMovieByImdbId).toHaveBeenCalledWith('tt0133093');
    // The TMDB-id lookup must not have been used.
    expect(mockLookupMovieById).not.toHaveBeenCalled();
  });

  it('IMDB Fetch shows not-found when the resolver returns nothing', async () => {
    mockLookupMovieByImdbId.mockResolvedValue({ kind: 'not-found' });

    renderForm();
    const user = userEvent.setup();
    const imdbInput = screen.getByPlaceholderText('e.g. tt1375666');
    await user.type(imdbInput, 'tt9999999');
    await user.click(screen.getByRole('button', { name: /fetch metadata by imdb id/i }));

    expect(await screen.findByText(/no movie with imdb id tt9999999/i)).toBeInTheDocument();
  });

  it('opens the cover editor with one click when an existing cover is collapsed', async () => {
    renderForm(vi.fn(), {
      initial: {
        title: 'Inception',
        formats: 0,
        status: 'Owned',
        watchStatus: 'Unwatched',
        watchCount: 0,
        imagePath: '/covers/abc1234567890def',
        tags: [],
      },
    });

    await userEvent.click(screen.getByRole('button', { name: /change cover/i }));

    expect(screen.getByPlaceholderText(/cover.jpg/i)).toBeInTheDocument();
    expect(screen.getByTestId('cover-editor-row')).toContainElement(screen.getByPlaceholderText(/cover.jpg/i));
  });

  it('picking a TMDB search result enriches director and runtime via a follow-up by-id call', async () => {
    // Search response carries title/year/description but not director or
    // runtime -- those come from the chained /movie/{id} call.
    mockUseLookup.mockReturnValue({
      data: {
        provider: 'tmdb',
        configured: true,
        results: [
          {
            provider: 'tmdb',
            providerKey: '27205',
            title: 'Inception',
            originalTitle: 'Inception',
            year: 2010,
            director: null,
            runtimeMinutes: null,
            description: 'A heist on the subconscious.',
            imageUrl: 'https://image.tmdb.org/t/p/w342/poster.jpg',
            genres: null,
          },
        ],
      },
      isLoading: false,
      error: null,
    });
    mockLookupMovieById.mockResolvedValue({
      kind: 'found',
      result: {
        provider: 'tmdb',
        providerKey: '27205',
        title: 'Inception',
        originalTitle: 'Inception',
        year: 2010,
        director: 'Christopher Nolan',
        runtimeMinutes: 148,
        description: 'A heist on the subconscious.',
        imageUrl: 'https://image.tmdb.org/t/p/w342/poster.jpg',
        genres: null,
      },
    });

    renderForm();
    const user = userEvent.setup();

    // Open the OnlineSearch dropdown by typing into it.
    await user.type(screen.getByPlaceholderText('e.g. Inception'), 'inc');
    // Click the suggestion. The dropdown row's primary text is
    // "Inception (2010)".
    await user.click(await screen.findByText('Inception (2010)'));

    // The synchronous patch fills title/year right away.
    await waitFor(() => {
      expect(screen.getAllByDisplayValue('Inception').length).toBeGreaterThanOrEqual(1);
    });

    // The async enrichment fills director and runtime once /movie/{id} resolves.
    await waitFor(() => {
      expect(screen.getByDisplayValue('Christopher Nolan')).toBeInTheDocument();
    });
    expect(screen.getByDisplayValue('148')).toBeInTheDocument();
    expect(mockLookupMovieById).toHaveBeenCalledWith('27205');
  });
});
