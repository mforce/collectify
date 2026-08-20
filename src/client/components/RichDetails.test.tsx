import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import MovieDetail from './MovieDetail';
import GameDetail from './GameDetail';
import MusicDetail from './MusicDetail';

describe('rich detail fields', () => {
  it('renders a movie provider rating only when present', () => {
    const item = { title: 'Arrival', formats: 0, status: 'Owned', watchStatus: 'Unwatched', watchCount: 0 } as const;
    const { rerender } = render(<MovieDetail item={{ ...item, providerRating: 8.4 }} />);
    expect(screen.getByText('TMDB ★ 8.4')).toBeInTheDocument();
    rerender(<MovieDetail item={item} />);
    expect(screen.queryByText(/TMDB ★/)).not.toBeInTheDocument();
  });

  it('renders a game age rating', () => {
    render(<GameDetail item={{ title: 'Journey', platform: 'Ps5', digitalStores: 0, status: 'Owned', completionStatus: 'NotStarted', ageRating: 'PEGI 16' }} />);
    expect(screen.getByText('PEGI 16')).toBeInTheDocument();
  });

  it('renders a formatted music release date', () => {
    render(<MusicDetail item={{ title: 'Blue', artistName: 'Joni Mitchell', format: 'Vinyl', status: 'Owned', listenCount: 0, releaseDate: '1971-06-22' }} />);
    expect(screen.getByText(/Jun 22, 1971/)).toBeInTheDocument();
  });
});
