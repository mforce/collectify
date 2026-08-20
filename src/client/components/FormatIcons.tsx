import type { MovieFormat, MusicFormat, GamePlatform } from '../services/types';

/**
 * Small inline-SVG format/platform icons for Collectify.
 *
 * Every icon is monochrome (inherits `currentColor`), decorative
 * (aria-hidden), and sized by a `className` prop — default ~20px. No external
 * icon library: everything is hand-drawn here so the set stays licensing-safe
 * (silhouettes, not brand logos).
 *
 * Graceful fallback rule: a member with no designed variant renders `null`
 * (the caller keeps showing its text label, no broken image / blank box) for
 * formats; platforms fall back to a generic console silhouette.
 */

type IconProps = { className?: string };

function Disc({ className, children }: IconProps & { children?: React.ReactNode }) {
  return (
    <svg
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      aria-hidden
      className={className ?? 'h-5 w-5'}
    >
      <circle cx="10" cy="10" r="8" />
      <circle cx="10" cy="10" r="4.2" opacity="0.65" />
      {children}
    </svg>
  );
}

// ─── Movie formats ────────────────────────────────────────────────
const MOVIE_FMT: Record<Exclude<MovieFormat, 'None'>, (p: IconProps) => React.ReactElement> = {
  Dvd: ({ className }) => (
    <Disc className={className}>
      <circle cx="10" cy="10" r="1.4" fill="currentColor" stroke="none" />
      <text x="10" y="12.2" textAnchor="middle" fontSize="3.6" fontWeight="700" fill="currentColor" stroke="none">
        DVD
      </text>
    </Disc>
  ),
  BluRay: ({ className }) => (
    <Disc className={className}>
      <circle cx="10" cy="10" r="5.6" opacity="0.4" />
      <text x="10" y="12.2" textAnchor="middle" fontSize="3.6" fontWeight="700" fill="currentColor" stroke="none">
        BD
      </text>
    </Disc>
  ),
  UhdBluRay: ({ className }) => (
    <Disc className={className}>
      <text x="10" y="12.4" textAnchor="middle" fontSize="4.4" fontWeight="800" fill="currentColor" stroke="none">
        4K
      </text>
    </Disc>
  ),
  Vhs: ({ className }) => (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden className={className ?? 'h-5 w-5'}>
      {/* Cassette outline */}
      <rect x="2.5" y="5.5" width="15" height="9" rx="1.4" />
      {/* Label window */}
      <rect x="5.5" y="8" width="9" height="2.6" rx="0.6" opacity="0.6" />
      {/* Two reels */}
      <circle cx="6.8" cy="12" r="1" fill="currentColor" stroke="none" opacity="0.7" />
      <circle cx="13.2" cy="12" r="1" fill="currentColor" stroke="none" opacity="0.7" />
    </svg>
  ),
  Digital: ({ className }) => (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.4" aria-hidden className={className ?? 'h-5 w-5'}>
      {/* Cloud outline */}
      <path d="M6 15.5a3.6 3.6 0 0 1-.4-7.18 4.6 4.6 0 0 1 8.8 1.34A3.1 3.1 0 0 1 14 15.5H6Z" />
      {/* Download arrow */}
      <path d="M10 12.4V6.8M8 10.6l2 2 2-2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  ),
};

export function MovieFormatIcon({ format, className }: { format: MovieFormat } & IconProps) {
  if (format === 'None') return null;
  const render = MOVIE_FMT[format];
  if (!render) return null;
  return render({ className });
}

// ─── Music formats ────────────────────────────────────────────────
const MUSIC_FMT: Record<MusicFormat, (p: IconProps) => React.ReactElement> = {
  Cd: ({ className }) => (
    <Disc className={className}>
      <circle cx="10" cy="10" r="2.6" opacity="0.55" />
      <circle cx="10" cy="10" r="1.1" fill="currentColor" stroke="none" />
    </Disc>
  ),
  Vinyl: ({ className }) => (
    <Disc className={className}>
      {/* Grooves */}
      <circle cx="10" cy="10" r="6" opacity="0.4" />
      <circle cx="10" cy="10" r="4.6" opacity="0.3" />
      {/* Center label */}
      <circle cx="10" cy="10" r="2.4" fill="currentColor" stroke="none" opacity="0.55" />
      <circle cx="10" cy="10" r="0.9" fill="none" stroke="currentColor" />
    </Disc>
  ),
  Other: ({ className }) => (
    <Disc className={className}>
      <circle cx="10" cy="10" r="2" opacity="0.5" />
      <text x="10" y="12.1" textAnchor="middle" fontSize="3.2" fontWeight="700" fill="currentColor" stroke="none">
        ?
      </text>
    </Disc>
  ),
};

export function MusicFormatIcon({ format, className }: { format: MusicFormat } & IconProps) {
  return MUSIC_FMT[format]?.({ className }) ?? null;
}

// ─── Game platforms ───────────────────────────────────────────────
// Curated family silhouettes + generic fallback (issue approach 1).
function ConsoleSilhouette({ className, shape }: IconProps & { shape: 'tower' | 'slab' | 'handheld' | 'arcade' | 'monitor' | 'phone' }) {
  const body: React.ReactNode =
    shape === 'tower' ? (
      <path d="M5 3.5h10l1.2 3.6v9.4a2 2 0 0 1-2 2H5.8a2 2 0 0 1-2-2V7.1L5 3.5Z" />
    ) : shape === 'slab' ? (
      <rect x="3" y="6" width="14" height="8.5" rx="1.6" />
    ) : shape === 'handheld' ? (
      <path d="M3.4 8.2C3.4 6.5 5 5.2 6.9 5.2h6.2c1.9 0 3.5 1.3 3.5 3v2.6c0 1.7-1.6 3-3.5 3H6.9c-1.9 0-3.5-1.3-3.5-3V8.2Z" />
    ) : shape === 'arcade' ? (
      <path d="M4.5 8.2 6.2 5h7.6l1.7 3.2 1.5 3.3v2.6a1.6 1.6 0 0 1-1.6 1.6H4.6a1.6 1.6 0 0 1-1.6-1.6v-2.6l1.5-3.3Z" />
    ) : shape === 'monitor' ? (
      <path d="M2.8 4.6h14.4v9H2.8v-9Z M6.5 15.6h7M10 13.6v2" />
    ) : (
      <path d="M5.5 7.2c-1.4 0-2.5 1.1-2.5 2.5v1.4c0 1.4 1.1 2.5 2.5 2.5h9c1.4 0 2.5-1.1 2.5-2.5V9.7c0-1.4-1.1-2.5-2.5-2.5h-9Z M6 9.6v1M14 9.6v1" />
    );

  return (
    <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.3" aria-hidden className={className ?? 'h-5 w-5'}>
      {body}
    </svg>
  );
}

type ConsoleShape = 'tower' | 'slab' | 'handheld' | 'arcade' | 'monitor' | 'phone';

const PLATFORM_SHAPE: Record<string, ConsoleShape> = {
  // Computer / mobile
  Pc: 'monitor',
  Mac: 'monitor',
  Mobile: 'phone',
  // Xbox — rounded slab console
  XboxOriginal: 'slab',
  Xbox360: 'slab',
  XboxOne: 'slab',
  XboxSeriesXS: 'slab',
  // PlayStation — tower-ish console
  Ps1: 'tower',
  Ps2: 'tower',
  Ps3: 'tower',
  Ps4: 'tower',
  Ps5: 'tower',
  Psp: 'handheld',
  PsVita: 'handheld',
  // Nintendo
  Nes: 'arcade',
  Snes: 'arcade',
  N64: 'arcade',
  GameCube: 'slab',
  Wii: 'slab',
  WiiU: 'handheld',
  Switch: 'handheld',
  Switch2: 'handheld',
  GameBoy: 'handheld',
  GameBoyColor: 'handheld',
  GameBoyAdvance: 'handheld',
  NintendoDs: 'handheld',
  Nintendo3Ds: 'handheld',
  // Sega
  SegaGenesis: 'slab',
  SegaSaturn: 'slab',
  SegaDreamcast: 'slab',
};

export function PlatformIcon({ platform, className }: { platform: GamePlatform } & IconProps) {
  const shape = PLATFORM_SHAPE[platform] ?? 'arcade'; // generic console fallback
  return <ConsoleSilhouette shape={shape} className={className} />;
}
