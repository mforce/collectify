import { describe, expect, it } from 'vitest';
import { MEDIA } from './mediaRegistry';
import { MOVIE_FORMAT_FLAGS, MUSIC_FORMATS, GAME_PLATFORMS } from './types';

describe('media registry', () => {
  it('has exactly the three media types as exhaustive keys', () => {
    expect(Object.keys(MEDIA).sort()).toEqual(['games', 'movies', 'music']);
  });

  it('derives distinct, non-empty theme tokens per type', () => {
    const accents = Object.values(MEDIA).map((m) => m.theme.textAccent);
    expect(new Set(accents).size).toBe(3);
    for (const m of Object.values(MEDIA)) {
      expect(m.theme.textAccent).toBeTruthy();
      expect(m.theme.submitButton).toBeTruthy();
      expect(m.theme.heading).toBeTruthy();
    }
  });

  it('maps each type to its intended provider linkage name', () => {
    expect(MEDIA.movies.providerName).toBe('tmdb');
    expect(MEDIA.music.providerName).toBe('musicbrainz');
    expect(MEDIA.games.providerName).toBe('igdb');
  });

  it('points each route at the current base paths', () => {
    expect(MEDIA.movies.paths.list).toBe('/movies');
    expect(MEDIA.movies.paths.new).toBe('/movies/new');
    expect(MEDIA.music.paths.list).toBe('/music');
    expect(MEDIA.games.paths.list).toBe('/games');
  });
});
