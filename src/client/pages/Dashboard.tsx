import { Link } from 'react-router-dom';
import { useDashboard, type DashboardRecent } from '../services/collection';
import { Card, ViewSwitcher } from '../components/ui';
import { useViewPreference, type ViewMode } from '../hooks/useViewPreference';

const TYPE_LABEL: Record<DashboardRecent['type'], string> = {
  movies: 'Movie',
  music: 'Album',
  games: 'Game',
};

const TILE_BORDER: Record<string, string> = {
  movies: 'border-t-movies',
  music: 'border-t-music',
  games: 'border-t-games',
};

export default function Dashboard() {
  const dashboard = useDashboard();
  const summary = dashboard.data;
  const counts = summary?.counts;
  const [viewMode, setViewMode] = useViewPreference('movies');

  const tiles = [
    { to: '/movies', label: 'Movies', count: counts?.movies, type: 'movies' as const },
    { to: '/music', label: 'Music', count: counts?.music, type: 'music' as const },
    { to: '/games', label: 'Games', count: counts?.games, type: 'games' as const },
  ];

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-xl font-medium text-text-primary tracking-tight">Your collection</h1>
        <ViewSwitcher value={viewMode} onChange={setViewMode} />
      </div>

      <div className="grid sm:grid-cols-3 gap-3">
        {tiles.map((t) => (
          <Link key={t.to} to={t.to} className="block">
            <Card className={`hover:border-brand/40 transition-colors border-t-2 ${TILE_BORDER[t.type]}`}>
              <div className="text-text-secondary text-sm">{t.label}</div>
              <div className="text-2xl font-medium text-text-primary mt-1">
                {t.count ?? '…'}
              </div>
            </Card>
          </Link>
        ))}
      </div>

      <RecentSection summary={summary} loading={dashboard.isLoading} error={dashboard.error} viewMode={viewMode} />
    </div>
  );
}

function RecentSection({
  summary,
  loading,
  error,
  viewMode,
}: {
  summary: { recent: DashboardRecent[] } | undefined;
  loading: boolean;
  error: unknown;
  viewMode: ViewMode;
}) {
  const gridClass = viewMode === 'list'
    ? 'grid-cols-1 gap-2'
    : viewMode === 'medium'
      ? 'grid-cols-1 sm:grid-cols-2 gap-3'
      : 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4';

  return (
    <section className="space-y-3">
      <h2 className="text-xs font-medium uppercase tracking-wide text-text-tertiary">
        Recent additions
      </h2>
      {loading && <p className="text-text-secondary">Loading…</p>}
      {error != null && <p className="text-error">Failed to load.</p>}
      {summary && summary.recent.length === 0 && !loading && (
        <Card className="text-center text-text-secondary py-8">
          Nothing here yet — pick a section above and click "+ Add" to start.
        </Card>
      )}
      {summary && summary.recent.length > 0 && (
        <div className={`grid ${gridClass}`}>
          {summary.recent.map((r) => (
            <Link key={`${r.type}-${r.id}`} to={`/${r.type}/${r.id}`} className="group block">
              {viewMode === 'list' ? (
                <ListRecentCard r={r} />
              ) : viewMode === 'medium' ? (
                <MediumRecentCard r={r} />
              ) : (
                <BigRecentCard r={r} />
              )}
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}

function ListRecentCard({ r }: { r: DashboardRecent }) {
  return (
    <Card className="!p-0 overflow-hidden transition-shadow hover:shadow-md group-hover:border-brand/30 dark:hover:bg-card/80">
      <div className="flex items-center gap-3 p-2">
        <div className="w-16 shrink-0 bg-imgPlaceholder overflow-hidden rounded">
          {r.imagePath ? (
            <img src={r.imagePath} alt="" loading="lazy" className="w-full aspect-[2/3] object-cover group-hover:scale-105 transition-transform duration-300" />
          ) : (
            <div aria-hidden className="w-full aspect-[2/3] flex items-center justify-center text-text-tertiary text-xs font-medium bg-imgPlaceholder">no cover</div>
          )}
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="font-medium text-text-primary leading-snug truncate">{r.title}</h3>
          <div className="text-xs uppercase tracking-wide text-text-tertiary">
            {TYPE_LABEL[r.type]}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}

function MediumRecentCard({ r }: { r: DashboardRecent }) {
  return (
    <Card className="!p-0 overflow-hidden transition-shadow hover:shadow-md group-hover:border-brand/30 dark:hover:bg-card/80">
      <div className="flex gap-3 p-3">
        <div className="w-24 shrink-0 bg-imgPlaceholder overflow-hidden rounded">
          {r.imagePath ? (
            <img src={r.imagePath} alt="" loading="lazy" className="w-full aspect-[2/3] object-cover group-hover:scale-105 transition-transform duration-300" />
          ) : (
            <div aria-hidden className="w-full aspect-[2/3] flex items-center justify-center text-text-tertiary text-xs font-medium bg-imgPlaceholder">no cover</div>
          )}
        </div>
        <div className="flex-1 min-w-0 flex flex-col gap-1.5">
          <h3 className="font-medium text-text-primary leading-snug line-clamp-2">{r.title}</h3>
          <div className="text-xs uppercase tracking-wide text-text-tertiary">
            {TYPE_LABEL[r.type]}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}

function BigRecentCard({ r }: { r: DashboardRecent }) {
  return (
    <Card className="h-full !p-0 overflow-hidden transition-shadow hover:shadow-md group-hover:border-brand/30 dark:hover:bg-card/80">
      <div className="flex flex-col md:flex-row">
        <div className="relative w-full shrink-0 bg-imgPlaceholder overflow-hidden sm:w-24 md:w-36 lg:w-48">
          {r.imagePath ? (
            <img src={r.imagePath} alt="" loading="lazy" className="w-full h-40 sm:h-auto md:h-auto sm:aspect-[2/3] md:aspect-[2/3] object-cover transition-transform duration-300 group-hover:scale-105" />
          ) : (
            <div aria-hidden className="w-full h-40 sm:h-auto md:h-auto flex items-center justify-center text-text-tertiary text-sm font-medium sm:aspect-[2/3] md:aspect-[2/3]">no cover</div>
          )}
        </div>
        <div className="flex-1 p-3 flex flex-col gap-1.5 min-w-0">
          <h3 className="font-medium text-text-primary leading-snug line-clamp-2">{r.title}</h3>
          <div className="text-xs uppercase tracking-wide text-text-tertiary">
            {TYPE_LABEL[r.type]}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}
