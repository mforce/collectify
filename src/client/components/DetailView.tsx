import { Link } from 'react-router-dom';
import { Card, CoverPreview, StatusPill, TagChip } from './ui';
import type { Album, Game, Movie } from '../services/types';
import { MOVIE_FORMAT_FLAGS, WATCH_STATUSES, COMPLETION_STATUSES, gamePlatformLabel } from '../services/types';

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
    <div className="flex items-start gap-2 py-1.5">
      <dt className="w-36 shrink-0 text-sm text-text-secondary">{label}</dt>
      <dd className="text-sm text-text-primary break-words">{value}</dd>
    </div>
  );
}

// ─── Movie detail ────────────────────────────────────────────────

function MovieDetail({ item }: { item: Movie }) {
  const formats = MOVIE_FORMAT_FLAGS.filter((f) => (item.formats & f.value) !== 0);
  const watchLabel = WATCH_STATUSES.find((w) => w.value === item.watchStatus)?.label;

  return (
    <div className="space-y-6">
      {/* Hero section */}
      <div className="flex flex-col md:flex-row gap-6 items-start">
        <div className="w-48 shrink-0 mx-auto md:mx-0">
          <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
        </div>
        <div className="flex-1 space-y-3 min-w-0">
          <div>
            <h2 className="text-xl font-medium text-text-primary leading-tight">{item.title}</h2>
            {item.originalTitle && item.originalTitle !== item.title && (
              <p className="text-sm text-text-secondary mt-0.5">{item.originalTitle}</p>
            )}
          </div>

          {/* Year / Director / Runtime row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span>{item.year}</span>}
            {item.director && <span>{item.director}</span>}
            {item.runtimeMinutes && <span>{item.runtimeMinutes} min</span>}
          </div>

          {/* Formats */}
          {formats.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {formats.map((f) => (
                <span key={f.key} className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-brand/10 text-brand border border-brand/20">
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
      <Card className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Studio" value={item.studio} />
          <InfoRow label="Genres" value={item.genres} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="TMDB ID" value={item.tmdbId} />
          <InfoRow label="IMDB ID" value={item.imdbId} />
        </dl>
      </Card>

      {/* Watching info */}
      {(item.lastWatchedOn || item.watchCount > 0) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Watching</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last watched" value={item.lastWatchedOn ? formatDate(item.lastWatchedOn) : undefined} />
            <InfoRow label="Watch count" value={`${item.watchCount}`} />
          </dl>
        </Card>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </Card>
      )}

      {/* Notes */}
      {item.notes && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </Card>
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
      <div className="flex flex-col md:flex-row gap-6 items-start">
        <div className="w-48 shrink-0 mx-auto md:mx-0">
          <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
        </div>
        <div className="flex-1 space-y-3 min-w-0">
          <div>
            <h2 className="text-xl font-medium text-text-primary leading-tight">{item.title}</h2>
            {item.artistName && (
              <p className="text-sm text-text-secondary mt-0.5">{item.artistName}</p>
            )}
          </div>

          {/* Year / Format row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span>{item.year}</span>}
            {item.format && (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-brand/10 text-brand border border-brand/20">
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
      <Card className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Label" value={item.label} />
          <InfoRow label="Genres" value={item.genres} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="MusicBrainz ID" value={item.musicBrainzReleaseId} />
          <InfoRow label="Discogs ID" value={item.discogsId} />
        </dl>
      </Card>

      {/* Listening info */}
      {(item.lastPlayedOn || item.listenCount > 0) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Listening</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last played" value={item.lastPlayedOn ? formatDate(item.lastPlayedOn) : undefined} />
            <InfoRow label="Listen count" value={`${item.listenCount}`} />
          </dl>
        </Card>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </Card>
      )}

      {/* Notes */}
      {item.notes && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </Card>
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
  const completionLabel = COMPLETION_STATUSES.find((c) => c.value === item.completionStatus)?.label;

  return (
    <div className="space-y-6">
      {/* Hero section */}
      <div className="flex flex-col md:flex-row gap-6 items-start">
        <div className="w-48 shrink-0 mx-auto md:mx-0">
          <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
        </div>
        <div className="flex-1 space-y-3 min-w-0">
          <div>
            <h2 className="text-xl font-medium text-text-primary leading-tight">{item.title}</h2>
            {gamePlatformLabel(item.platform) && (
              <p className="text-sm text-text-secondary mt-0.5">{gamePlatformLabel(item.platform)}</p>
            )}
          </div>

          {/* Year / Platform row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-text-secondary">
            {item.year && <span>{item.year}</span>}
            {item.isDigital ? (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-brand/10 text-brand border border-brand/20">
                Digital{item.digitalStore ? ` (${item.digitalStore})` : ''}
              </span>
            ) : (
              <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-pill-bg text-text-secondary border border-border">
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
      <Card className="p-4">
        <dl className="grid md:grid-cols-2 gap-x-6">
          <InfoRow label="Publisher" value={item.publisher} />
          <InfoRow label="Developer" value={item.developer} />
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow label="IGDB ID" value={item.igdbId} />
        </dl>
      </Card>

      {/* Playing info */}
      {(item.lastPlayedOn || item.hoursPlayed != null) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Playing</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Last played" value={item.lastPlayedOn ? formatDate(item.lastPlayedOn) : undefined} />
            {item.hoursPlayed != null && (
              <InfoRow label="Hours played" value={`${Number(item.hoursPlayed).toFixed(1)}h`} />
            )}
          </dl>
        </Card>
      )}

      {/* Acquisition info */}
      {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource) && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Acquisition</h3>
          <dl className="grid md:grid-cols-2 gap-x-6">
            <InfoRow label="Date" value={item.acquiredOn ? formatDate(item.acquiredOn) : undefined} />
            <InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} />
            <InfoRow label="Source" value={item.acquisitionSource} />
          </dl>
        </Card>
      )}

      {/* Notes */}
      {item.notes && (
        <Card className="p-4">
          <h3 className="text-xs font-medium uppercase tracking-wide text-text-tertiary mb-2">Notes</h3>
          <p className="text-sm text-text-primary whitespace-pre-wrap">{item.notes}</p>
        </Card>
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
        <Link to={`/${type}`} className="text-sm text-text-secondary hover:text-text-primary transition-colors flex items-center gap-1">
          ← Back to {type}
        </Link>
        <button
          type="button"
          onClick={onEdit}
          className="inline-flex items-center px-3 py-1.5 rounded-md text-sm border border-border bg-card hover:border-brand/40 transition-colors"
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
