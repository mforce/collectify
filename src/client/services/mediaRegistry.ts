// ---- Correctness core: the registry shape. Transcribed verbatim. ----
import type { ComponentType } from 'react';
import MovieForm from '../components/MovieForm';
import AlbumForm from '../components/AlbumForm';
import GameForm from '../components/GameForm';
import type { Album, Game, MediaType, Movie } from './types';
import type { GameLookupResult, MovieLookupResult, MusicLookupResult } from './lookup';

/** The per-media-type lookup result type (was the duplicated `ResultMap` values). */
export type MediaResultMap = {
  movies: MovieLookupResult;
  music: MusicLookupResult;
  games: GameLookupResult;
};

/** The per-media-type collection item type. */
export type MediaItemMap = {
  movies: Movie;
  music: Album;
  games: Game;
};

export interface MediaConfig {
  /** Singular human label, e.g. 'Movie'. */
  label: string;
  /** Plural nav label, e.g. 'Movies'. */
  pluralLabel: string;
  /** Icon used by MediaIcon / nav / headers. */
  iconSrc: string;
  iconAlt: string;
  /** Add-page title, e.g. 'Add a movie'. */
  addTitle: string;
  /** Edit-page singular title, e.g. 'Movie' (paired with `label`—only differs if labels diverge). */
  singularTitle: string;
  /** Success toast after create, e.g. 'Movie added.' */
  addSuccess: string;
  /** Delete toast, e.g. 'Movie deleted.' */
  deletedMessage: string;
  /** Route paths. */
  paths: { list: string; new: string; item: string }; // '/movies', '/movies/new', '/movies/:id'
  /** Theme fallback tokens (Tailwind theme variants). Must match today's bytes for the class composites. */
  theme: {
    /** e.g. 'text-movies' — heading / accent text. */
    textAccent: string;
    /** e.g. 'text-movies' — used by AddPage/EditPage `themeByType.title`. */
    titleText: string;
    /** Card/surface token: e.g. 'theme-movies'. */
    cardTheme: string;
    /** AddPage/EditPage title-by-type heading text class. */
    heading: string;
    /** Submit button class. */
    submitButton: string;
    /** Dashboard tile border/accent composites — exact bytes. */
    tileBorder: string;
    tileAccent: string;
    /** Dashboard recent-card hover composite — exact bytes. */
    recentHover: string;
    /** Layout nav active composites (desktop + mobile) — exact bytes. */
    navActiveDesktop: string;
    navActiveMobile: string;
  };
  /** Form component (movie => MovieForm, etc.). */
  formComponent: ComponentType<never>;
  /** Provider canonical name used to detect the prefill provider-key, e.g. 'tmdb'. */
  providerName: string;
  /** Lookup-result type for this media type. */
  lookupResultType: MediaResultMap[MediaType];
  /** Collection item type for this media type. */
  itemType: MediaItemMap[MediaType];
}

export const MEDIA: Record<MediaType, MediaConfig> = {
  movies: {
    label: 'Movie', pluralLabel: 'Movies', iconSrc: '/brand/media-movies.svg', iconAlt: 'Movies',
    addTitle: 'Add a movie', singularTitle: 'Movie', addSuccess: 'Movie added.', deletedMessage: 'Movie deleted.',
    paths: { list: '/movies', new: '/movies/new', item: '/movies/:id' },
    theme: {
      textAccent: 'text-movies', titleText: 'text-movies', cardTheme: 'theme-movies', heading: 'text-movies',
      submitButton: 'bg-movies text-[#071333] hover:bg-movies/85',
      tileBorder: 'border-movies-border bg-movies-light', tileAccent: 'text-movies',
      recentHover: 'group-hover:border-movies group-hover:bg-movies-light/70',
      navActiveDesktop: 'bg-movies-light text-movies border-movies-border shadow-sm',
      navActiveMobile: 'bg-movies-light text-movies border-movies-border',
    },
    formComponent: MovieForm, providerName: 'tmdb', lookupResultType: null!, itemType: null!,
  },
  music: {
    label: 'Album', pluralLabel: 'Music', iconSrc: '/brand/media-music.svg', iconAlt: 'Music',
    addTitle: 'Add an album', singularTitle: 'Album', addSuccess: 'Album added.', deletedMessage: 'Album deleted.',
    paths: { list: '/music', new: '/music/new', item: '/music/:id' },
    theme: {
      textAccent: 'text-music', titleText: 'text-music', cardTheme: 'theme-music', heading: 'text-music',
      submitButton: 'bg-music text-white hover:bg-music/85',
      tileBorder: 'border-music-border bg-music-light', tileAccent: 'text-music',
      recentHover: 'group-hover:border-music group-hover:bg-music-light/70',
      navActiveDesktop: 'bg-music-light text-music border-music-border shadow-sm',
      navActiveMobile: 'bg-music-light text-music border-music-border',
    },
    formComponent: AlbumForm, providerName: 'musicbrainz', lookupResultType: null!, itemType: null!,
  },
  games: {
    label: 'Game', pluralLabel: 'Games', iconSrc: '/brand/media-games.svg', iconAlt: 'Games',
    addTitle: 'Add a game', singularTitle: 'Game', addSuccess: 'Game added.', deletedMessage: 'Game deleted.',
    paths: { list: '/games', new: '/games/new', item: '/games/:id' },
    theme: {
      textAccent: 'text-games', titleText: 'text-games', cardTheme: 'theme-games', heading: 'text-games',
      submitButton: 'bg-games text-white hover:bg-games/85',
      tileBorder: 'border-games-border bg-games-light', tileAccent: 'text-games',
      recentHover: 'group-hover:border-games group-hover:bg-games-light/70',
      navActiveDesktop: 'bg-games-light text-games border-games-border shadow-sm',
      navActiveMobile: 'bg-games-light text-games border-games-border',
    },
    formComponent: GameForm, providerName: 'igdb', lookupResultType: null!, itemType: null!,
  },
};
