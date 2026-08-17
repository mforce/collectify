import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';
import { Card, CoverPreview, StatusPill, TagChip } from './ui';
import type { Album, Game, MediaType, Movie } from '../services/types';
import { completionStatusLabel, gamePlatformLabel, MOVIE_FORMAT_FLAGS, watchStatusLabel } from '../services/types';
import MediaIcon from './MediaIcon';

interface Props<T> {
  item: T;
  type: 'movies' | 'music' | 'games';
  onEdit: () => void;
}

// ─── Shared helpers ──────────────────────────────────────────────

function formatPrice(price?: number | null, currency?: string | null): string {
  if (price == null) return '';
  const sym = currency === 'USD' ? '$' : currency === 'EUR' ? '€' : currency === 'GBP' ? '£' : `${currency ?? ''} `;
  return `${sym}${Number(price).toFixed(2)}`;
}

function formatDate(date?: string | null): string {
  if (!date) return '';
  const d = new Date(date + 'T00:00:00');
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

// ─── Info row primitive ──────────────────────────────────────────

function InfoRow({ label, value }: { label: string; value?: string | null }) {
  if (!value) return null;
  return (
    <div className="flex flex-col sm:flex-row gap-1 py-1.5">
      <dt className="w-24 shrink-0 sm:w-36 text-sm text-text-secondary">{label}</dt>
      <dd className="text-sm text-text-primary break-words min-w-0">{value}</dd>
    </div>
  );
}

const detailTheme: Record<MediaType, {
  title: string;
  accent: string;
  button: string;
}> = {
  movies: {
    title: 'text-movies',
    accent: 'bg-movies-light text-movies border-movies-border',
    button: 'border-movies-border text-movies hover:bg-movies-light',
  },
  music: {
    title: 'text-music',
    accent: 'bg-music-light text-music border-music-border',
    button: 'border-music-border text-music hover:bg-music-light',
  },
  games: {
    title: 'text-games',
    accent: 'bg-games-light text-games border-games-border',
    button: 'border-games-border text-games hover:bg-games-light',
  },
};

function ThemedCard({ type, children, className = '' }: { type: MediaType; children: ReactNode; className?: string }) {
  return <Card className={className}>{children}</Card>;
}

function HeroTitle({ type, title, subtitle }: { type: MediaType; title: string; subtitle?: string | null }) {
  return (
    <div className="flex items-start gap-3 min-w-0">
      <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl border border-border bg-card shadow-sm">
        <MediaIcon type={type} className="h-7 w-7" />
      </span>
      <div className="min-w-0 flex-1">
        <h2 className={`text-2xl font-extrabold leading-tight tracking-tight ${detailTheme[type].title}`}>{title}</h2>
        {subtitle && <p className="mt-0.5 text-sm text-text-secondary truncate">{subtitle}</p>}
      </div>
    </div>
  );
}

// ─── Movie detail ────────────────────────────────────────────────

function MovieDetail({ item }: { item: Movie }) {
  const formats = MOVIE_FORMAT_FLAGS.filter((f) => (item.formats & f.value) !== 0);
  const watchLabel = watchStatusLabel(item.watchStatus);

  return (
    <div className="space-y-6">
      {/* Hero section */}
      <div className="flex flex-col gap-4 md:flex-row items-start">
        <div className="w-36 shrink-0 sm:w-40 md:w-48 mx-auto">
          {item.imagePath ? (
            <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
          ) : (
            <div aria-hidden className="aspect-[2/3] flex items-center justify-center text-text-tertiary text-sm font-medium bg-imgPlaceholder rounded border border-border">
              no cover
            </div>
          )}
        </div>
        <div className="min-w-0 flex-1 space-y-3">
          <div>
            <HeroTitle
              type="movies"
              title={item.title}
              subtitle={item.originalTitle && item.originalTitle !== item.title ? item.originalTitle : null}
            />
          </div>

          {/* Year / Director / Runtime row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span className="shrink-0">{item.year}</span>}
            {item.director && <span className="flex-1 min-w-0 truncate">{item.director}</span>}
            {item.runtimeMinutes && <span className="shrink-0">{item.runtimeMinutes} min</span>}
          </div>

          {/* Formats */}
          {formats.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {formats.map((f) => (
                <span key={f.key} className={`inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold ${detailTheme.movies.accent}`}>
                  {f.label}
                </span>
              ))}
            </div>
          )}

          {/* Rating */}
          {item.personalRating != null && (
            <div className="text-sm font-medium text-movies">★ {item.personalRating}/10</div>
          )}

          {/* Status + Watch status */}
          <div className="flex flex-wrap gap-2 items-center">
            <StatusPill status={item.status} category="movies" />
            {watchLabel && (
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border bg-pill-bg text-text-secondary border-border">
                {watchLabel}
              </span>
            )}
          </div>

          {/* Tags */}
          {item.tags && item.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {item.tags.map((t) => (
                <TagChip key={t} name={t} category="movies" />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Info grid */}
      <ThemedCard type="movies" className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Studio" value={item.studio} />
          <InfoRow label="Genres" value={item.genres} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="TMDB ID" value={item.tmdbId} />
          <InfoRow label="IMDB ID" value={item.imdbId} />
        </dl>
      </ThemedCard>

      {/* Watching info */}
      {(item.lastWatchedOn || item.watchCount > 0) && (
        <ThemedCard type="movies" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-movies mb-2">Watching</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last watched" value={item.lastWatchedOn ? formatDate(item.lastWatchedOn) : undefined} />
            <InfoRow label="Watch count" value={`${item.watchCount}`} />
          </dl>
        </ThemedCard>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <ThemedCard type="movies" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-movies mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </ThemedCard>
      )}

      {/* Notes */}
      {item.notes && (
        <ThemedCard type="movies" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-movies mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </ThemedCard>
      )}

      {/* Condition */}
      {item.condition && (
        <div className="flex gap-1.5 items-center">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-pill-bg text-text-secondary border-border">
            Condition: {item.condition.replace(/([A-Z])/g, ' $1').trim()}
          </span>
        </div>
      )}
    </div>
  );
}

// ─── Music detail ────────────────────────────────────────────────

function MusicDetail({ item }: { item: Album }) {
  return (
    <div className="space-y-6">
      {/* Hero section */}
      <div className="flex flex-col gap-4 md:flex-row items-start">
        <div className="w-36 shrink-0 sm:w-40 md:w-48 mx-auto">
          {item.imagePath ? (
            <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
          ) : (
            <div aria-hidden className="aspect-[2/3] flex items-center justify-center text-text-tertiary text-sm font-medium bg-imgPlaceholder rounded border border-border">
              no cover
            </div>
          )}
        </div>
        <div className="min-w-0 flex-1 space-y-3">
          <div>
            <HeroTitle type="music" title={item.title} subtitle={item.artistName} />
          </div>

          {/* Year / Format row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span className="shrink-0">{item.year}</span>}
            {item.format && (
              <span className={`inline-flex items-center shrink-0 rounded-full border px-2 py-0.5 text-xs font-semibold ${detailTheme.music.accent}`}>
                {item.format === 'Cd' ? 'CD' : item.format === 'Vinyl' ? 'Vinyl' : 'Other'}
              </span>
            )}
          </div>

          {/* Rating */}
          {item.personalRating != null && (
            <div className="text-sm font-medium text-music">★ {item.personalRating}/10</div>
          )}

          {/* Status */}
          <StatusPill status={item.status} category="music" />

          {/* Tags */}
          {item.tags && item.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {item.tags.map((t) => (
                <TagChip key={t} name={t} category="music" />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Info grid */}
      <ThemedCard type="music" className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Label" value={item.label} />
          <InfoRow label="Genres" value={item.genres} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="MusicBrainz ID" value={item.musicBrainzReleaseId} />
          <InfoRow label="Discogs ID" value={item.discogsId} />
        </dl>
      </ThemedCard>

      {/* Listening info */}
      {(item.lastPlayedOn || item.listenCount > 0) && (
        <ThemedCard type="music" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-music mb-2">Listening</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last played" value={item.lastPlayedOn ? formatDate(item.lastPlayedOn) : undefined} />
            <InfoRow label="Listen count" value={`${item.listenCount}`} />
          </dl>
        </ThemedCard>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <ThemedCard type="music" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-music mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </ThemedCard>
      )}

      {/* Notes */}
      {item.notes && (
        <ThemedCard type="music" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-music mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </ThemedCard>
      )}

      {/* Condition */}
      {item.condition && (
        <div className="flex gap-1.5 items-center">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-pill-bg text-text-secondary border-border">
            Condition: {item.condition.replace(/([A-Z])/g, ' $1').trim()}
          </span>
        </div>
      )}
    </div>
  );
}

// ─── Game detail ─────────────────────────────────────────────────

function GameDetail({ item }: { item: Game }) {
  const completionLabel = completionStatusLabel(item.completionStatus);

  return (
    <div className="space-y-6">
      {/* Hero section */}
      <div className="flex flex-col gap-4 md:flex-row items-start">
        <div className="w-36 shrink-0 sm:w-40 md:w-48 mx-auto">
          {item.imagePath ? (
            <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
          ) : (
            <div aria-hidden className="aspect-[2/3] flex items-center justify-center text-text-tertiary text-sm font-medium bg-imgPlaceholder rounded border border-border">
              no cover
            </div>
          )}
        </div>
        <div className="min-w-0 flex-1 space-y-3">
          <div>
            <HeroTitle type="games" title={item.title} subtitle={gamePlatformLabel(item.platform)} />
          </div>

          {/* Year / Platform row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span className="shrink-0">{item.year}</span>}
            {item.isDigital ? (
              <span className={`inline-flex items-center shrink-0 rounded-full border px-2 py-0.5 text-xs font-semibold ${detailTheme.games.accent}`}>
                Digital{item.digitalStore && <span className="min-w-0 truncate"> ({item.digitalStore})</span>}
              </span>
            ) : (
              <span className="inline-flex items-center shrink-0 px-2 py-0.5 rounded-full text-xs font-medium bg-pill-bg text-text-secondary border border-border">
                Physical
              </span>
            )}
          </div>

          {/* Rating */}
          {item.personalRating != null && (
            <div className="text-sm font-medium text-games">★ {item.personalRating}/10</div>
          )}

          {/* Status + Completion */}
          <div className="flex flex-wrap gap-2 items-center">
            <StatusPill status={item.status} category="games" />
            {completionLabel && (
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold border bg-pill-bg text-text-secondary border-border">
                {completionLabel}
              </span>
            )}
          </div>

          {/* Tags */}
          {item.tags && item.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {item.tags.map((t) => (
                <TagChip key={t} name={t} category="games" />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Info grid */}
      <ThemedCard type="games" className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Publisher" value={item.publisher} />
          <InfoRow label="Developer" value={item.developer} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="IGDB ID" value={item.igdbId} />
        </dl>
      </ThemedCard>

      {/* Playing info */}
      {(item.lastPlayedOn || item.hoursPlayed != null) && (
        <ThemedCard type="games" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-games mb-2">Playing</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last played" value={item.lastPlayedOn ? formatDate(item.lastPlayedOn) : undefined} />
            {item.hoursPlayed != null && (
              <InfoRow label="Hours played" value={`${Number(item.hoursPlayed).toFixed(1)}h`} />
            )}
          </dl>
        </ThemedCard>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <ThemedCard type="games" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-games mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </ThemedCard>
      )}

      {/* Notes */}
      {item.notes && (
        <ThemedCard type="games" className="p-4">
          <h3 className="text-xs font-bold uppercase tracking-wide text-games mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </ThemedCard>
      )}

      {/* Condition */}
      {item.condition && (
        <div className="flex gap-1.5 items-center">
          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium border bg-pill-bg text-text-secondary border-border">
            Condition: {item.condition.replace(/([A-Z])/g, ' $1').trim()}
          </span>
        </div>
      )}
    </div>
  );
}

// ─── Main component ──────────────────────────────────────────────

export default function DetailView<T extends Movie | Album | Game>({ item, type, onEdit }: Props<T>) {
  return (
    <div className="space-y-4">
      {/* Header with back link and edit button */}
      <div className="flex items-center justify-between gap-4">
        <Link to={`/${type}`} className={`flex items-center gap-1 text-sm font-semibold transition-colors ${detailTheme[type].title}`}>
          ← Back to {type}
        </Link>
        <button
          type="button"
          onClick={onEdit}
          className={`inline-flex min-h-[40px] items-center rounded-xl border bg-card px-4 py-1.5 text-sm font-bold transition-colors ${detailTheme[type].button}`}
        >
          Edit
        </button>
      </div>

      {/* Type-specific detail */}
      {type === 'movies' && <MovieDetail item={item as Movie} />}
      {type === 'music' && <MusicDetail item={item as Album} />}
      {type === 'games' && <GameDetail item={item as Game} />}
    </div>
  );
}
