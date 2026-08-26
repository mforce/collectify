import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AlbumForm from './AlbumForm';

const mockLookupAlbumByMbid = vi.fn();
const mockUseLookup = vi.fn().mockReturnValue({ data: undefined, isLoading: false, error: null });
vi.mock('../services/lookup', async (importOriginal) => {
  const original = await importOriginal<typeof import('../services/lookup')>();
  return {
    ...original,
    lookupAlbumByMbid: (id: string) => mockLookupAlbumByMbid(id),
    useLookup: () => mockUseLookup(),
  };
});
vi.mock('../services/tags', () => ({
  useTags: () => ({ data: [], isLoading: false, error: null }),
}));

function renderForm(onSubmit = vi.fn()) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <AlbumForm onSubmit={onSubmit} />
    </QueryClientProvider>,
  );
  return { onSubmit };
}

describe('AlbumForm — Fetch metadata by MusicBrainz Release ID', () => {
  beforeEach(() => {
    mockLookupAlbumByMbid.mockReset();
    mockUseLookup.mockReset();
    mockUseLookup.mockReturnValue({ data: undefined, isLoading: false, error: null });
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('disables the fetch button when the MBID field is empty', () => {
    renderForm();
    expect(screen.getByRole('button', { name: /fetch metadata by musicbrainz release id/i })).toBeDisabled();
  });

  it('populates form fields when the lookup returns a found result', async () => {
    mockLookupAlbumByMbid.mockResolvedValue({
      kind: 'found',
      result: {
        provider: 'musicbrainz',
        providerKey: 'f4e51c80-99e2-39e1-8062-c9b8e2685bdf',
        title: 'OK Computer',
        artistName: 'Radiohead',
        year: 1997,
        label: 'Parlophone',
        description: null,
        imageUrl: 'https://coverartarchive.org/release/f4e51c80-99e2-39e1-8062-c9b8e2685bdf/front-500',
        genres: null,
      },
    });

    renderForm();
    const user = userEvent.setup();

    const mbidInput = screen.getByPlaceholderText('e.g. f4e51c80-99e2-39e1-8062-c9b8e2685bdf');
    await user.type(mbidInput, 'f4e51c80-99e2-39e1-8062-c9b8e2685bdf');
    await user.click(screen.getByRole('button', { name: /fetch metadata by musicbrainz release id/i }));

    await waitFor(() => {
      expect(screen.getByDisplayValue('OK Computer')).toBeInTheDocument();
    });
    expect(screen.getByDisplayValue('Radiohead')).toBeInTheDocument();
    expect(screen.getByDisplayValue('1997')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Parlophone')).toBeInTheDocument();
    expect(mockLookupAlbumByMbid).toHaveBeenCalledWith('f4e51c80-99e2-39e1-8062-c9b8e2685bdf');
  });

  it('shows a hint when the provider reports not-configured', async () => {
    mockLookupAlbumByMbid.mockResolvedValue({ kind: 'not-configured' });

    renderForm();
    const user = userEvent.setup();
    await user.type(screen.getByPlaceholderText('e.g. f4e51c80-99e2-39e1-8062-c9b8e2685bdf'), 'abc');
    await user.click(screen.getByRole('button', { name: /fetch metadata by musicbrainz release id/i }));

    expect(await screen.findByText(/not configured/i)).toBeInTheDocument();
    expect(screen.queryByDisplayValue('OK Computer')).not.toBeInTheDocument();
  });

  it('shows a not-found hint when the lookup misses', async () => {
    mockLookupAlbumByMbid.mockResolvedValue({ kind: 'not-found' });

    renderForm();
    const user = userEvent.setup();
    await user.type(screen.getByPlaceholderText('e.g. f4e51c80-99e2-39e1-8062-c9b8e2685bdf'), '00000000-0000-0000-0000-000000000000');
    await user.click(screen.getByRole('button', { name: /fetch metadata by musicbrainz release id/i }));

    expect(
      await screen.findByText(/no release with musicbrainz release id/i),
    ).toBeInTheDocument();
  });

  it('picking an OnlineSearch result populates fields and stores the MBID', async () => {
    mockUseLookup.mockReturnValue({
      data: {
        provider: 'musicbrainz',
        configured: true,
        results: [
          {
            provider: 'musicbrainz',
            providerKey: 'f4e51c80-99e2-39e1-8062-c9b8e2685bdf',
            title: 'OK Computer',
            artistName: 'Radiohead',
            year: 1997,
            label: 'Parlophone',
            description: null,
            imageUrl: null,
            genres: null,
          },
        ],
      },
      isLoading: false,
      error: null,
    });

    renderForm();
    const user = userEvent.setup();

    await user.type(screen.getByPlaceholderText('e.g. OK Computer'), 'ok');
    await user.click(await screen.findByText('OK Computer (1997)'));

    await waitFor(() => {
      expect(screen.getByDisplayValue('OK Computer')).toBeInTheDocument();
    });
    expect(screen.getByDisplayValue('Radiohead')).toBeInTheDocument();
    expect(screen.getByDisplayValue('f4e51c80-99e2-39e1-8062-c9b8e2685bdf')).toBeInTheDocument();
  });
});

describe('AlbumForm — Genres', () => {
  it('typing a genre renders it as a chip and submits it as an array', async () => {
    const { onSubmit } = renderForm();
    const user = userEvent.setup();

    const genreInput = screen.getByPlaceholderText('Add genre…');
    await user.type(genreInput, 'Rock');
    await user.keyboard('{Enter}');

    expect(screen.getByText('rock')).toBeInTheDocument();

    const titleLabel = screen.getByText('Title');
    const titleInput = titleLabel.parentElement?.querySelector('input') as HTMLInputElement;
    await user.type(titleInput, 'Test Album');
    const artistLabel = screen.getByText('Artist');
    const artistInput = artistLabel.parentElement?.querySelector('input') as HTMLInputElement;
    await user.type(artistInput, 'Test Artist');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ genres: ['rock'] }),
    );
  });
});
