import type { Album } from '../services/types';
import { musicFormatLabel } from '../services/types';
import { CoverPreview, StatusPill, TagChip } from './ui';
import { MusicFormatIcon } from './FormatIcons';
import {
  formatDate,
  formatPrice,
  HeroTitle,
  InfoRow,
  ThemedCard,
} from './detailShared';

export default function MusicDetail({ item }: { item: Album }) {
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
          <HeroTitle type="music" title={item.title} subtitle={item.artistName} />
          <StatusPill status={item.status} category="music" />
        </div>
      </div>
      <ThemedCard type="music" className="p-4">
        <h3>Release</h3>
        <InfoRow label="Release date" value={formatDate(item.releaseDate)} />
        <InfoRow label="Year" value={item.year?.toString()} />
        {item.format && (
          <p>
            <MusicFormatIcon format={item.format} className="inline h-4 w-4" />{' '}
            {musicFormatLabel(item.format) ?? item.format}
          </p>
        )}
      </ThemedCard>
      {(item.genres?.length || item.label) && (
        <ThemedCard type="music" className="p-4">
          <h3>Metadata</h3>
          <InfoRow label="Label" value={item.label} />
          {!!item.genres?.length && (
            <div>
              {item.genres.map((genre) => (
                <TagChip key={genre} name={genre} category="music" />
              ))}
            </div>
          )}
        </ThemedCard>
      )}
      {(item.barcode || item.musicBrainzReleaseId || item.discogsId) && (
        <ThemedCard type="music" className="p-4">
          <h3>IDs</h3>
          <InfoRow label="Barcode" value={item.barcode} />
          <InfoRow
            label="MusicBrainz ID"
            value={item.musicBrainzReleaseId}
          />
          <InfoRow label="Discogs ID" value={item.discogsId} />
        </ThemedCard>
      )}
      <ThemedCard type="music" className="p-4">
        <h3>Personal</h3>
        <InfoRow
          label="Rating"
          value={item.personalRating != null ? `★ ${item.personalRating}/10` : null}
        />
        <InfoRow
          label="Listen count"
          value={item.listenCount > 0 ? `${item.listenCount}` : null}
        />
        <InfoRow label="Last played" value={formatDate(item.lastPlayedOn)} />
      </ThemedCard>
      {item.description && (
        <ThemedCard type="music" className="p-4">
          <h3>Description</h3>
          {item.description}
        </ThemedCard>
      )}
      {item.notes && (
        <ThemedCard type="music" className="p-4">
          <h3>Notes</h3>
          {item.notes}
        </ThemedCard>
      )}
      {!!item.tags?.length && (
        <div>
          {item.tags.map((t) => (
            <TagChip key={t} name={t} category="music" />
          ))}
        </div>
      )}
      {(item.acquiredOn ||
        item.acquisitionPrice != null ||
        item.acquisitionSource ||
        item.condition) && (
        <ThemedCard type="music" className="p-4">
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
