import type { Game } from '../services/types';
import { completionStatusLabel, digitalStoresLabel, gamePlatformLabel } from '../services/types';
import { CoverPreview, StatusPill, TagChip } from './ui';
import { PlatformIcon } from './FormatIcons';
import { formatDate, formatPrice, HeroTitle, InfoRow, ThemedCard } from './detailShared';

export default function GameDetail({ item }: { item: Game }) {
  const store = item.digitalStores ? `Digital · ${digitalStoresLabel(item.digitalStores)}` : 'Physical';
  return <div className="space-y-6">
    <div className="flex gap-4"><div className="w-36">{item.imagePath ? <CoverPreview src={item.imagePath} alt={`${item.title} cover`} /> : <div>no cover</div>}</div><div><HeroTitle type="games" title={item.title} subtitle={<span><PlatformIcon platform={item.platform} className="inline h-4 w-4" /> {gamePlatformLabel(item.platform)}</span>} /><p>{store}</p><StatusPill status={item.status} category="games" /></div></div>
    <ThemedCard type="games" className="p-4"><h3>Platform</h3><p>{gamePlatformLabel(item.platform)} · {item.digitalStores ? 'Digital' : 'Physical'}</p></ThemedCard>
    {(item.releaseDate || item.year) && <ThemedCard type="games" className="p-4"><h3>Release</h3><InfoRow label="Release date" value={formatDate(item.releaseDate)} /><InfoRow label="Year" value={item.year?.toString()} /></ThemedCard>}
    {(item.developer || item.publisher) && <ThemedCard type="games" className="p-4"><h3>Credits</h3><InfoRow label="Developer" value={item.developer} /><InfoRow label="Publisher" value={item.publisher} /></ThemedCard>}
    {item.ageRating && <ThemedCard type="games" className="p-4"><h3>Metadata</h3><InfoRow label="Age rating" value={item.ageRating} /></ThemedCard>}
    <ThemedCard type="games" className="p-4"><h3>Personal</h3><InfoRow label="Rating" value={item.personalRating != null ? `★ ${item.personalRating}/10` : null} /><InfoRow label="Completion" value={completionStatusLabel(item.completionStatus)} /><InfoRow label="Hours played" value={item.hoursPlayed != null ? `${Number(item.hoursPlayed).toFixed(1)}h` : null} /><InfoRow label="Last played" value={formatDate(item.lastPlayedOn)} /></ThemedCard>
    {item.description && <ThemedCard type="games" className="p-4"><h3>Description</h3>{item.description}</ThemedCard>}{item.notes && <ThemedCard type="games" className="p-4"><h3>Notes</h3>{item.notes}</ThemedCard>}
    {!!item.tags?.length && <div>{item.tags.map(t => <TagChip key={t} name={t} category="games" />)}</div>}
    {(item.acquiredOn || item.acquisitionPrice != null || item.acquisitionSource || item.condition) && <ThemedCard type="games" className="p-4"><h3>Acquisition</h3><InfoRow label="Date" value={formatDate(item.acquiredOn)} /><InfoRow label="Price" value={formatPrice(item.acquisitionPrice, item.acquisitionCurrency)} /><InfoRow label="Source" value={item.acquisitionSource} /><InfoRow label="Condition" value={item.condition?.replace(/([A-Z])/g, ' $1').trim()} /></ThemedCard>}
  </div>;
}
