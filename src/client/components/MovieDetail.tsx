import type { Movie } from '../services/types';
import { MOVIE_FORMAT_FLAGS, watchStatusLabel } from '../services/types';
import { CoverPreview, StatusPill, TagChip } from './ui';
import { MovieFormatIcon } from './FormatIcons';
import {
  detailTheme,
  formatDate,
  formatPrice,
  HeroTitle,
  InfoRow,
  ThemedCard,
} from './detailShared';

export default function MovieDetail({ item }: { item: Movie }) {
  const fs = MOVIE_FORMAT_FLAGS.filter((f) => (item.formats & f.value) !== 0);

  return (
    <div className="space-y-6">
      <div className="flex gap-4">
        <div className="w-36">
          {item.imagePath ? (
            <CoverPreview src={item.imagePath} alt={`${item.title} cover`} />
          ) : (
            <div>no cover</div>
          )}
        </div>
        <div>
          <HeroTitle
            type="movies"
            title={item.title}
            subtitle={
              item.originalTitle && item.originalTitle !== item.title
                ? item.originalTitle
                : null
            }
          />
          <div>
            {fs.map((f) => (
              <span key={f.key} className={detailTheme('movies').accent}>
                <MovieFormatIcon format={f.key} className="inline h-4 w-4" /> {f.label}{' '}
              </span>
            ))}
          </div>
          <StatusPill status={item.status} category="movies" />{' '}
          {watchStatusLabel(item.watchStatus)}
        </div>
      </div>
      {(item.releaseDate || item.year || item.runtimeMinutes) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Release</h3>
          <dl>
            <InfoRow label="Release date" value={formatDate(item.releaseDate)} />
            <InfoRow label="Year" value={item.year?.toString()} />
            <InfoRow
              label="Runtime"
              value={item.runtimeMinutes ? `${item.runtimeMinutes} min` : null}
            />
          </dl>
        </ThemedCard>
      )}
      {(item.director || item.cast) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Credits</h3>
          <dl>
            <InfoRow label="Director" value={item.director} />
            <InfoRow label="Cast" value={item.cast} />
          </dl>
        </ThemedCard>
      )}
      {(item.studio || item.genres || fs.length > 0) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Metadata</h3>
          <dl>
            <InfoRow label="Studio" value={item.studio} />
            <InfoRow label="Genres" value={item.genres} />
          </dl>
        </ThemedCard>
      )}
      {(item.personalRating != null || item.providerRating != null) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Ratings</h3>
          {item.personalRating != null && <p>★ {item.personalRating}/10</p>}
          {item.providerRating != null && <p>TMDB ★ {item.providerRating}</p>}
        </ThemedCard>
      )}
      {(item.watchCount > 0 || item.lastWatchedOn) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Watching</h3>
          <InfoRow label="Watch count" value={`${item.watchCount}`} />
          <InfoRow label="Last watched" value={formatDate(item.lastWatchedOn)} />
        </ThemedCard>
      )}
      {item.description && (
        <ThemedCard type="movies" className="p-4">
          <h3>Description</h3>
          <p>{item.description}</p>
        </ThemedCard>
      )}
      {item.notes && (
        <ThemedCard type="movies" className="p-4">
          <h3>Notes</h3>
          <p>{item.notes}</p>
        </ThemedCard>
      )}
      {!!item.tags?.length && (
        <div>
          {item.tags.map((t) => (
            <TagChip key={t} name={t} category="movies" />
          ))}
        </div>
      )}
      {(item.acquiredOn ||
        item.acquisitionPrice != null ||
        item.acquisitionSource ||
        item.condition) && (
        <ThemedCard type="movies" className="p-4">
          <h3>Acquisition</h3>
          <InfoRow label="Date" value={formatDate(item.acquiredOn)} />
          <InfoRow
            label="Price"
            value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)}
          />
          <InfoRow label="Source" value={item.acquisitionSource} />
          <InfoRow
            label="Condition"
            value={item.condition?.replace(/([A-Z])/g, ' $1').trim()}
          />
        </ThemedCard>
      )}
    </div>
  );
}
