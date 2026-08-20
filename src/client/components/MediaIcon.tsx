import type { MediaType } from '../services/types';
import { MEDIA } from '../services/mediaRegistry';

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
      src={MEDIA[type].iconSrc}
      alt={decorative ? '' : MEDIA[type].iconAlt}
      aria-hidden={decorative || undefined}
      className={className}
      loading="lazy"
    />
  );
}
