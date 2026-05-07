import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import MovieForm from './MovieForm';

// Swap the data layer so the form's "Fetch metadata" button can be exercised
// without a backend or a TanStack Query refetch dance.
const mockLookupMovieById = vi.fn();
vi.mock('../services/lookup', async (importOriginal) => {
  const original = await importOriginal<typeof import('../services/lookup')>();
  return {
    ...original,
    lookupMovieById: (id: string) => mockLookupMovieById(id),
    useLookup: () => ({ data: undefined, isLoading: false, error: null }),
  };
});
vi.mock('../services/tags', () => ({
  useTags: () => ({ data: [], isLoading: false, error: null }),
}));

function renderForm(onSubmit = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <MovieForm onSubmit={onSubmit} />
    </QueryClientProvider>,
  );
  return { onSubmit };
}

describe('MovieForm — Fetch metadata by TMDB ID', () => {
  beforeEach(() => {
    mockLookupMovieById.mockReset();
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('disables the fetch button when the TMDB ID field is empty', () => {
    renderForm();
    const button = screen.getByRole('button', { name: /fetch metadata/i });
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
    await user.click(screen.getByRole('button', { name: /fetch metadata/i }));

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
    await user.click(screen.getByRole('button', { name: /fetch metadata/i }));

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
    await user.click(screen.getByRole('button', { name: /fetch metadata/i }));

    expect(await screen.findByText(/no movie with tmdb id 9999999/i)).toBeInTheDocument();
  });
});
