import type { MediaType } from '../services/types';

const ICON_SRC: Record<MediaType, string> = {
  movies: '/brand/media-movies.svg',
  music: '/brand/media-music.svg',
  games: '/brand/media-games.svg',
};

const ICON_ALT: Record<MediaType, string> = {
  movies: 'Movies',
  music: 'Music',
  games: 'Games',
};

export default function MediaIcon({
  type,
  className = 'h-6 w-6',
  decorative = true,
}: {
  type: MediaType;
  className?: string;
  decorative?: boolean;
}) {
  return (
    <img
      src={ICON_SRC[type]}
      alt={decorative ? '' : ICON_ALT[type]}
      aria-hidden={decorative || undefined}
      className={className}
      loading="lazy"
    />
  );
}
