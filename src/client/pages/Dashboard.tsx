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
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {summary.recent.map((r) => (
            <Link key={`${r.type}-${r.id}`} to={`/${r.type}/${r.id}`} className="block">
              <Card className="hover:border-brand/40 transition-colors !p-3 flex gap-3 h-full">
                {r.imagePath ? (
                  <img
                    src={r.imagePath}
                    alt=""
                    loading="lazy"
                    className="w-12 h-16 object-cover rounded border border-border flex-none bg-gray-50"
                  />
                ) : (
                  <div
                    aria-hidden
                    className="w-12 h-16 rounded flex-none bg-gray-50 border border-border flex items-center justify-center text-text-tertiary text-[10px]"
                  >
                    no cover
                  </div>
                )}
                <div className="min-w-0 flex-1">
                  <div className="text-xs uppercase tracking-wide text-text-tertiary">
                    {TYPE_LABEL[r.type]}
                  </div>
                  <div className="text-sm font-medium text-text-primary truncate">{r.title}</div>
                  {r.year != null && <div className="text-xs text-text-secondary">{r.year}</div>}
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </section>
  );
}
