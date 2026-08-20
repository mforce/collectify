import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import MovieDetail from './MovieDetail';
import GameDetail from './GameDetail';
import MusicDetail from './MusicDetail';

describe('rich detail fields', () => {
  it('renders a movie provider rating only when present', () => {
    const item = { title: 'Arrival', formats: 0, status: 'Owned', watchStatus: 'Unwatched', watchCount: 0, barcode: '012345678905', tmdbId: '329865', imdbId: 'tt2543164' } as const;
    const { rerender } = render(<MovieDetail item={{ ...item, providerRating: 8.4 }} />);
    expect(screen.getByText('TMDB ★ 8.4')).toBeInTheDocument();
    expect(screen.getByText('Barcode')).toBeInTheDocument();
    expect(screen.getByText('012345678905')).toBeInTheDocument();
    expect(screen.getByText('TMDB ID')).toBeInTheDocument();
    expect(screen.getByText('329865')).toBeInTheDocument();
    expect(screen.getByText('IMDB ID')).toBeInTheDocument();
    expect(screen.getByText('tt2543164')).toBeInTheDocument();
    rerender(<MovieDetail item={item} />);
    expect(screen.queryByText(/TMDB ★/)).not.toBeInTheDocument();
  });

  it('renders a game age rating', () => {
    render(<GameDetail item={{ title: 'Journey', platform: 'Ps5', digitalStores: 0, status: 'Owned', completionStatus: 'NotStarted', ageRating: 'PEGI 16', barcode: '711719541028', igdbId: '11208' }} />);
    expect(screen.getByText('PEGI 16')).toBeInTheDocument();
    expect(screen.getByText('Barcode')).toBeInTheDocument();
    expect(screen.getByText('711719541028')).toBeInTheDocument();
    expect(screen.getByText('IGDB ID')).toBeInTheDocument();
    expect(screen.getByText('11208')).toBeInTheDocument();
  });

  it('renders a formatted music release date', () => {
    render(<MusicDetail item={{ title: 'Blue', artistName: 'Joni Mitchell', format: 'Vinyl', status: 'Owned', listenCount: 0, releaseDate: '1971-06-22', barcode: '075678271216', musicBrainzReleaseId: 'mb-release-id', discogsId: '12345' }} />);
    expect(screen.getByText(/Jun 22, 1971/)).toBeInTheDocument();
    expect(screen.getByText('Barcode')).toBeInTheDocument();
    expect(screen.getByText('075678271216')).toBeInTheDocument();
    expect(screen.getByText('MusicBrainz ID')).toBeInTheDocument();
    expect(screen.getByText('mb-release-id')).toBeInTheDocument();
    expect(screen.getByText('Discogs ID')).toBeInTheDocument();
    expect(screen.getByText('12345')).toBeInTheDocument();
  });
});
