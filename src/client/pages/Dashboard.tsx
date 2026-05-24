import { Link } from 'react-router-dom';
import { useDashboard, type DashboardRecent } from '../services/collection';
import { Card } from '../components/ui';

const TYPE_LABEL: Record<DashboardRecent['type'], string> = {
  movies: 'Movie',
  music: 'Album',
  games: 'Game',
};

export default function Dashboard() {
  const dashboard = useDashboard();
  const summary = dashboard.data;
  const counts = summary?.counts;

  const tiles = [
    { to: '/movies', label: 'Movies', count: counts?.movies },
    { to: '/music', label: 'Music', count: counts?.music },
    { to: '/games', label: 'Games', count: counts?.games },
  ];

  return (
    <div className="space-y-8">
      <h1 className="text-xl font-medium text-text-primary tracking-tight">Your collection</h1>

      <div className="grid sm:grid-cols-3 gap-3">
        {tiles.map((t) => (
          <Link key={t.to} to={t.to} className="block">
            <Card className="hover:border-brand/40 transition-colors">
              <div className="text-text-secondary text-sm">{t.label}</div>
              <div className="text-2xl font-medium text-text-primary mt-1">
                {t.count ?? '…'}
              </div>
            </Card>
          </Link>
        ))}
      </div>

      <RecentSection summary={summary} loading={dashboard.isLoading} error={dashboard.error} />
    </div>
  );
}

function RecentSection({
  summary,
  loading,
  error,
}: {
  summary: { recent: DashboardRecent[] } | undefined;
  loading: boolean;
  error: unknown;
}) {
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
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {summary.recent.map((r) => (
            <Link key={`${r.type}-${r.id}`} to={`/${r.type}/${r.id}`} className="group block">
              <Card className="h-full !p-0 overflow-hidden transition-shadow hover:shadow-md group-hover:border-brand/30 dark:hover:bg-card/80">
                <div className="flex flex-col sm:flex-row">
                  {/* Cover art — dominant visual element */}
                  <div className="relative w-full sm:w-48 shrink-0 bg-imgPlaceholder overflow-hidden">
                    {r.imagePath ? (
                      <img
                        src={r.imagePath}
                        alt=""
                        loading="lazy"
                        className="w-full h-48 sm:h-auto sm:aspect-[2/3] object-cover transition-transform duration-300 group-hover:scale-105"
                      />
                    ) : (
                      <div
                        aria-hidden
                        className="w-full h-48 sm:h-auto sm:aspect-[2/3] flex items-center justify-center text-text-tertiary text-sm font-medium"
                      >
                        no cover
                      </div>
                    )}
                  </div>

                  {/* Metadata — right side on desktop, below cover on mobile */}
                  <div className="flex-1 p-3 flex flex-col gap-1.5 min-w-0">
                    <h3 className="font-medium text-text-primary leading-snug line-clamp-2">{r.title}</h3>
                    <div className="text-xs uppercase tracking-wide text-text-tertiary">
                      {TYPE_LABEL[r.type]}{r.year != null ? ` · ${r.year}` : ''}
                    </div>
                  </div>
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
