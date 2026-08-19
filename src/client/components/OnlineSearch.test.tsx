import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import OnlineSearch from './OnlineSearch';

// Swap the data layer with a controllable test double.
const mockUseLookup = vi.fn();
vi.mock('../services/lookup', () => ({
  useLookup: (type: string, query: string, platform?: string) => mockUseLookup(type, query, platform),
}));

describe('OnlineSearch', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    mockUseLookup.mockReset();
    vi.useRealTimers();
  });

  function renderForMovies(onPick = vi.fn()) {
    render(
      <OnlineSearch
        type="movies"
        onPick={onPick}
        renderItem={(r) => ({
          primary: r.title,
          secondary: r.description ?? undefined,
          image: r.imageUrl,
        })}
      />,
    );
    return { onPick };
  }

  it('does not query until at least 2 chars after debounce', async () => {
    mockUseLookup.mockReturnValue({ data: undefined, isLoading: false, error: null });
    renderForMovies();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    await user.type(screen.getByPlaceholderText('Type a title…'), 'i');
    vi.advanceTimersByTime(500);

    // Hook is still called by React on every render, but with the empty
    // debounced value -- never with the in-progress single-char input.
    const queries = mockUseLookup.mock.calls.map(([, q]) => q);
    expect(queries).not.toContain('i');
  });

  it('passes the debounced query to the hook once the user stops typing', async () => {
    mockUseLookup.mockReturnValue({ data: undefined, isLoading: false, error: null });
    renderForMovies();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    await user.type(screen.getByPlaceholderText('Type a title…'), 'inception');
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      const queries = mockUseLookup.mock.calls.map(([, q]) => q);
      expect(queries).toContain('inception');
    });
  });

  it('renders the suggestions and calls onPick with the chosen item', async () => {
    const item = {
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
    };
    mockUseLookup.mockReturnValue({
      data: { provider: 'tmdb', configured: true, results: [item] },
      isLoading: false,
      error: null,
    });
    const { onPick } = renderForMovies();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    await user.type(screen.getByPlaceholderText('Type a title…'), 'inception');
    vi.advanceTimersByTime(400);

    const choice = await screen.findByText('Inception');
    await user.click(choice);

    expect(onPick).toHaveBeenCalledWith(item);
  });

  it('shows the not-configured hint when the server reports configured=false', () => {
    // The dropdown is closed at mount; the inline label below the input
    // surfaces the hint so users see it before they even start typing.
    mockUseLookup.mockReturnValue({
      data: { provider: 'tmdb', configured: false, results: [] },
      isLoading: false,
      error: null,
    });
    renderForMovies();

    expect(screen.getByText(/online lookup not configured/i)).toBeInTheDocument();
  });

  it('shows "no matches" when the provider is configured but returned nothing', async () => {
    mockUseLookup.mockReturnValue({
      data: { provider: 'tmdb', configured: true, results: [] },
      isLoading: false,
      error: null,
    });
    renderForMovies();
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

    await user.type(screen.getByPlaceholderText('Type a title…'), 'zzz');
    vi.advanceTimersByTime(400);

    expect(await screen.findByText('No matches.')).toBeInTheDocument();
  });
});
