import { Link } from 'react-router-dom';
import { useDashboard, type DashboardRecent } from '../services/collection';
import { Card, ViewSwitcher } from '../components/ui';
import { useViewPreference, type ViewMode } from '../hooks/useViewPreference';
import MediaIcon from '../components/MediaIcon';
import { MEDIA } from '../services/mediaRegistry';
import type { MediaType } from '../services/types';

export default function Dashboard() {
  const dashboard = useDashboard();
  const summary = dashboard.data;
  const counts = summary?.counts;
  // P3 fix: dashboard uses its own storage key, not coupled to movies
  const [viewMode, setViewMode] = useViewPreference('dashboard');

  const tiles = (['movies', 'music', 'games'] as MediaType[]).map((type) => ({
    to: MEDIA[type].paths.list, label: MEDIA[type].pluralLabel, count: counts?.[type], type,
  }));

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-extrabold tracking-tight text-text-primary">My Collection</h1>
          <p className="mt-1 text-sm text-text-secondary">
            Browse your{' '}
            <Link to="/movies" className="font-semibold text-movies underline-offset-4 hover:underline">
              movies
            </Link>
            ,{' '}
            <Link to="/music" className="font-semibold text-music underline-offset-4 hover:underline">
              music
            </Link>
            , and{' '}
            <Link to="/games" className="font-semibold text-games underline-offset-4 hover:underline">
              games
            </Link>
            .
          </p>
        </div>
        <ViewSwitcher value={viewMode} onChange={setViewMode} />
      </div>

      <div className="grid sm:grid-cols-3 gap-3">
        {tiles.map((t) => (
          <Link key={t.to} to={t.to} className="block">
            <Card className={`border transition-colors hover:-translate-y-0.5 hover:border-brand/40 ${MEDIA[t.type].theme.tileBorder}`}>
              <div className={`mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-card shadow-sm ${MEDIA[t.type].theme.tileAccent}`}>
                <MediaIcon type={t.type} className="h-7 w-7" />
              </div>
              <div className="text-sm font-semibold text-text-secondary">{t.label}</div>
              <div className="mt-1 text-3xl font-extrabold text-text-primary">
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
      <h2 className="text-xs font-bold uppercase tracking-wide text-text-tertiary">
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
    <Card className={`!p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${MEDIA[r.type].theme.recentHover}`}>
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
            {MEDIA[r.type].label}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}

function MediumRecentCard({ r }: { r: DashboardRecent }) {
  return (
    <Card className={`!p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${MEDIA[r.type].theme.recentHover}`}>
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
            {MEDIA[r.type].label}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}

function BigRecentCard({ r }: { r: DashboardRecent }) {
  return (
    <Card className={`h-full !p-0 overflow-hidden transition-all hover:-translate-y-0.5 ${MEDIA[r.type].theme.recentHover}`}>
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
            {MEDIA[r.type].label}{r.year != null ? ` · ${r.year}` : ''}
          </div>
        </div>
      </div>
    </Card>
  );
}
