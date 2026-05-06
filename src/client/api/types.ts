export type MovieFormat = 'None' | 'Dvd' | 'BluRay' | 'UhdBluRay';

export const MOVIE_FORMAT_FLAGS: { value: number; key: Exclude<MovieFormat, 'None'>; label: string }[] = [
  { value: 1, key: 'Dvd', label: 'DVD' },
  { value: 2, key: 'BluRay', label: 'Blu-ray' },
  { value: 4, key: 'UhdBluRay', label: 'UHD Blu-ray' },
];

export interface Movie {
  id?: number;
  title: string;
  originalTitle?: string | null;
  year?: number | null;
  formats: number;
  director?: string | null;
  runtimeMinutes?: number | null;
  studio?: string | null;
  genres?: string | null;
  barcode?: string | null;
  tmdbId?: string | null;
  imdbId?: string | null;
  imagePath?: string | null;
  notes?: string | null;
  addedAt?: string;
  updatedAt?: string;
}

export type MusicFormat = 'Cd' | 'Vinyl' | 'Other';
export const MUSIC_FORMATS: { value: MusicFormat; label: string }[] = [
  { value: 'Cd', label: 'CD' },
  { value: 'Vinyl', label: 'Vinyl' },
  { value: 'Other', label: 'Other' },
];

export interface Album {
  id?: number;
  title: string;
  artistName: string;
  year?: number | null;
  format: MusicFormat;
  label?: string | null;
  genres?: string | null;
  barcode?: string | null;
  musicBrainzReleaseId?: string | null;
  discogsId?: string | null;
  imagePath?: string | null;
  notes?: string | null;
  addedAt?: string;
  updatedAt?: string;
}

export type DigitalStore = 'Steam' | 'Gog' | 'Epic' | 'Xbox' | 'Psn' | 'Nintendo' | 'Other';
export const DIGITAL_STORES: { value: DigitalStore; label: string }[] = [
  { value: 'Steam', label: 'Steam' },
  { value: 'Gog', label: 'GOG' },
  { value: 'Epic', label: 'Epic' },
  { value: 'Xbox', label: 'Xbox' },
  { value: 'Psn', label: 'PlayStation Network' },
  { value: 'Nintendo', label: 'Nintendo' },
  { value: 'Other', label: 'Other' },
];

export interface Game {
  id?: number;
  title: string;
  platform?: string | null;
  year?: number | null;
  publisher?: string | null;
  developer?: string | null;
  isDigital: boolean;
  digitalStore?: DigitalStore | null;
  barcode?: string | null;
  igdbId?: string | null;
  imagePath?: string | null;
  notes?: string | null;
  addedAt?: string;
  updatedAt?: string;
}

export type MediaType = 'movies' | 'music' | 'games';
