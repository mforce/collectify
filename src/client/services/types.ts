// ---------- Shared (Phase 1.1) ----------

export type CollectionStatus = 'Owned' | 'Wishlist' | 'OnOrder' | 'Sold';
export const COLLECTION_STATUSES: { value: CollectionStatus; label: string }[] = [
  { value: 'Owned', label: 'Owned' },
  { value: 'Wishlist', label: 'Wishlist' },
  { value: 'OnOrder', label: 'On order' },
  { value: 'Sold', label: 'Sold' },
];

export type Condition = 'New' | 'LikeNew' | 'Good' | 'Fair' | 'Poor';
export const CONDITIONS: { value: Condition; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'LikeNew', label: 'Like new' },
  { value: 'Good', label: 'Good' },
  { value: 'Fair', label: 'Fair' },
  { value: 'Poor', label: 'Poor' },
];

export type WatchStatus = 'Unwatched' | 'Watching' | 'Watched';
export const WATCH_STATUSES: { value: WatchStatus; label: string }[] = [
  { value: 'Unwatched', label: 'Unwatched' },
  { value: 'Watching', label: 'Watching' },
  { value: 'Watched', label: 'Watched' },
];

export type CompletionStatus = 'NotStarted' | 'Playing' | 'Beaten' | 'HundredPercent' | 'Abandoned';
export const COMPLETION_STATUSES: { value: CompletionStatus; label: string }[] = [
  { value: 'NotStarted', label: 'Not started' },
  { value: 'Playing', label: 'Playing' },
  { value: 'Beaten', label: 'Beaten' },
  { value: 'HundredPercent', label: '100%' },
  { value: 'Abandoned', label: 'Abandoned' },
];

export interface Tag {
  id: number;
  name: string;
}

// Common Phase 1.1 fields surfaced on every collection item.
export interface CollectionItemBase {
  description?: string | null;
  personalRating?: number | null;       // 1..10
  status: CollectionStatus;
  condition?: Condition | null;
  acquiredOn?: string | null;            // 'YYYY-MM-DD'
  acquisitionPrice?: number | null;
  acquisitionCurrency?: string | null;   // 3-letter ISO 4217
  acquisitionSource?: string | null;
  tags?: string[];
}

// ---------- Movies ----------

export type MovieFormat = 'None' | 'Dvd' | 'BluRay' | 'UhdBluRay' | 'Vhs' | 'Digital';

export const MOVIE_FORMAT_FLAGS: { value: number; key: Exclude<MovieFormat, 'None'>; label: string }[] = [
  { value: 1, key: 'Dvd', label: 'DVD' },
  { value: 2, key: 'BluRay', label: 'Blu-ray' },
  { value: 4, key: 'UhdBluRay', label: 'UHD Blu-ray' },
  { value: 8, key: 'Vhs', label: 'VHS' },
  { value: 16, key: 'Digital', label: 'Digital' },
];

export interface Movie extends CollectionItemBase {
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
  watchStatus: WatchStatus;
  lastWatchedOn?: string | null;
  watchCount: number;
  addedAt?: string;
  updatedAt?: string;
}

// ---------- Music ----------

export type MusicFormat = 'Cd' | 'Vinyl' | 'Other';
export const MUSIC_FORMATS: { value: MusicFormat; label: string }[] = [
  { value: 'Cd', label: 'CD' },
  { value: 'Vinyl', label: 'Vinyl' },
  { value: 'Other', label: 'Other' },
];

export interface Album extends CollectionItemBase {
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
  listenCount: number;
  lastPlayedOn?: string | null;
  addedAt?: string;
  updatedAt?: string;
}

// ---------- Games ----------

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

// Mirrors Collectify.Domain.Enums.GamePlatform. Order of the array
// drives the <Select> dropdown; tweak freely without breaking storage
// since the JSON wire format uses the string names, not indices.
export type GamePlatform =
  | 'Other'
  | 'Pc' | 'Mac' | 'Linux' | 'Mobile'
  | 'XboxOriginal' | 'Xbox360' | 'XboxOne' | 'XboxSeriesXS'
  | 'Ps1' | 'Ps2' | 'Ps3' | 'Ps4' | 'Ps5' | 'Psp' | 'PsVita'
  | 'Nes' | 'Snes' | 'N64' | 'GameCube' | 'Wii' | 'WiiU' | 'Switch' | 'Switch2'
  | 'GameBoy' | 'GameBoyColor' | 'GameBoyAdvance' | 'NintendoDs' | 'Nintendo3Ds'
  | 'SegaGenesis' | 'SegaSaturn' | 'SegaDreamcast';

export const GAME_PLATFORMS: { value: GamePlatform; label: string; group?: string }[] = [
  { value: 'Pc',             label: 'PC',                       group: 'Computer' },
  { value: 'Mac',            label: 'Mac',                      group: 'Computer' },
  { value: 'Linux',          label: 'Linux',                    group: 'Computer' },
  { value: 'Mobile',         label: 'Mobile',                   group: 'Computer' },

  { value: 'XboxOriginal',   label: 'Xbox (original)',          group: 'Xbox' },
  { value: 'Xbox360',        label: 'Xbox 360',                 group: 'Xbox' },
  { value: 'XboxOne',        label: 'Xbox One',                 group: 'Xbox' },
  { value: 'XboxSeriesXS',   label: 'Xbox Series X|S',          group: 'Xbox' },

  { value: 'Ps1',            label: 'PlayStation',              group: 'PlayStation' },
  { value: 'Ps2',            label: 'PlayStation 2',            group: 'PlayStation' },
  { value: 'Ps3',            label: 'PlayStation 3',            group: 'PlayStation' },
  { value: 'Ps4',            label: 'PlayStation 4',            group: 'PlayStation' },
  { value: 'Ps5',            label: 'PlayStation 5',            group: 'PlayStation' },
  { value: 'Psp',            label: 'PSP',                      group: 'PlayStation' },
  { value: 'PsVita',         label: 'PS Vita',                  group: 'PlayStation' },

  { value: 'Nes',            label: 'NES',                      group: 'Nintendo' },
  { value: 'Snes',           label: 'SNES',                     group: 'Nintendo' },
  { value: 'N64',            label: 'Nintendo 64',              group: 'Nintendo' },
  { value: 'GameCube',       label: 'GameCube',                 group: 'Nintendo' },
  { value: 'Wii',            label: 'Wii',                      group: 'Nintendo' },
  { value: 'WiiU',           label: 'Wii U',                    group: 'Nintendo' },
  { value: 'Switch',         label: 'Switch',                   group: 'Nintendo' },
  { value: 'Switch2',        label: 'Switch 2',                 group: 'Nintendo' },
  { value: 'GameBoy',        label: 'Game Boy',                 group: 'Nintendo' },
  { value: 'GameBoyColor',   label: 'Game Boy Color',           group: 'Nintendo' },
  { value: 'GameBoyAdvance', label: 'Game Boy Advance',         group: 'Nintendo' },
  { value: 'NintendoDs',     label: 'Nintendo DS',              group: 'Nintendo' },
  { value: 'Nintendo3Ds',    label: 'Nintendo 3DS',             group: 'Nintendo' },

  { value: 'SegaGenesis',    label: 'Sega Genesis / Mega Drive',group: 'Sega' },
  { value: 'SegaSaturn',     label: 'Sega Saturn',              group: 'Sega' },
  { value: 'SegaDreamcast',  label: 'Sega Dreamcast',           group: 'Sega' },

  { value: 'Other',          label: 'Other' },
];

export function gamePlatformLabel(value: GamePlatform): string {
  return GAME_PLATFORMS.find((p) => p.value === value)?.label ?? value;
}

// Shared label helpers so display code never open-codes `.find(...)?.label`
// against these tables (acceptance criterion for #95). Each returns the
// label for a member, or undefined when the member is unknown -- call sites
// decide whether to fall back to the raw value or hide.
export function collectionStatusLabel(value: CollectionStatus): string | undefined {
  return COLLECTION_STATUSES.find((s) => s.value === value)?.label;
}

export function conditionLabel(value: Condition): string | undefined {
  return CONDITIONS.find((c) => c.value === value)?.label;
}

export function watchStatusLabel(value: WatchStatus): string | undefined {
  return WATCH_STATUSES.find((w) => w.value === value)?.label;
}

export function completionStatusLabel(value: CompletionStatus): string | undefined {
  return COMPLETION_STATUSES.find((c) => c.value === value)?.label;
}

export function musicFormatLabel(value: MusicFormat): string | undefined {
  return MUSIC_FORMATS.find((f) => f.value === value)?.label;
}

export interface Game extends CollectionItemBase {
  id?: number;
  title: string;
  platform: GamePlatform;
  /** Original free-text platform preserved at migration time when it
   * couldn't map to the enum. Read-only; saving clears it. */
  platformLegacy?: string | null;
  year?: number | null;
  publisher?: string | null;
  developer?: string | null;
  isDigital: boolean;
  digitalStore?: DigitalStore | null;
  barcode?: string | null;
  igdbId?: string | null;
  imagePath?: string | null;
  notes?: string | null;
  completionStatus: CompletionStatus;
  hoursPlayed?: number | null;
  lastPlayedOn?: string | null;
  addedAt?: string;
  updatedAt?: string;
}

export type MediaType = 'movies' | 'music' | 'games';
