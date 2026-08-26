import { useEffect, useMemo, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Button, Card, SectionHeading } from '../components/ui';
import { toast } from '../components/toaster';
import {
  useSteamConnect,
  useSteamConnection,
  useSteamDisconnect,
  useSteamGames,
  useSteamImport,
} from '../services/steam';

const MAX_SELECTIONS = 500;

function formatPlaytime(minutes: number): string {
  if (!minutes) return '';
  const h = Math.floor(minutes / 60);
  return h >= 1 ? `${h}h` : `${minutes}m`;
}

export default function ImportSteam() {
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState('');
  const [pageSize, setPageSize] = useState(100);
  const [offset, setOffset] = useState(0);
  const [hideImported, setHideImported] = useState(false);

  const connection = useSteamConnection();
  const connect = useSteamConnect();
  const [debouncedFilter, setDebouncedFilter] = useState(filter);
  useEffect(() => {
    const t = setTimeout(() => setDebouncedFilter(filter), 300);
    return () => clearTimeout(t);
  }, [filter]);
  useEffect(() => setOffset(0), [filter]);
  // Search is sent to the server so it filters across the FULL owned library,
  // not just the capped preview slice — reaching lower-playtime titles a user
  // might search for (Codex: paginate/search large libraries).
  const games = useSteamGames(
    connection.data?.connected === true,
    filter.trim() ? debouncedFilter : '',
    offset,
    pageSize,
    hideImported,
  );
  const importSelected = useSteamImport(() => setSelected(new Set()));
  const repairCovers = useSteamImport(() => {});
  const steamMutationPending = importSelected.isPending || repairCovers.isPending;
  const disconnect = useSteamDisconnect(() => setSelected(new Set()));

  // Surface the OpenID callback outcome once, then clear it from the URL so
  // a reload doesn't re-toast.
  const outcome = params.get('steam') as 'connected' | 'error' | null;
  useEffect(() => {
    if (!outcome) return;
    if (outcome === 'connected') toast.success('Steam connected!');
    if (outcome === 'error') toast.error('Could not connect Steam. Try again.');
    params.delete('steam');
    setParams(params, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [outcome]);

  const importable = useMemo(
    () => (games.data?.titles ?? []).filter((g) => g.state === 'importable'),
    [games.data],
  );

  const importedCount = useMemo(
    () => (games.data?.titles ?? []).filter((g) => g.state === 'imported').length,
    [games.data],
  );

  const rendered = games.data?.titles ?? [];

  const toggle = (id: string) => {
    const next = new Set(selected);
    if (next.has(id)) {
      next.delete(id);
      setSelected(next);
      return;
    }
    if (next.size >= MAX_SELECTIONS) {
      toast.info('You can select up to 500 games at a time.');
      return;
    }
    next.add(id);
    setSelected(next);
  };

  const allImportableSelected =
    importable.length > 0 && importable.every((g) => selected.has(g.externalGameId));

  const toggleAll = () => {
    const next = new Set(selected);
    const pageIds = importable.map((g) => g.externalGameId);
    if (allImportableSelected) {
      pageIds.forEach((id) => next.delete(id));
      setSelected(next);
      return;
    }

    const additions = pageIds.filter((id) => !next.has(id));
    const remaining = MAX_SELECTIONS - next.size;
    additions.slice(0, remaining).forEach((id) => next.add(id));
    setSelected(next);
    if (additions.length > remaining)
      toast.info('You can select up to 500 games at a time.');
  };

  const handleConnect = async () => {
    try {
      const res = await connect.mutateAsync();
      if (!res.configured || !res.redirectUrl) {
        toast.error('Steam import is not configured on this server.');
        return;
      }
      // Whole-page navigation to Steam; we come back to
      // /import/steam?steam=connected|error via the OpenID callback.
      window.location.href = res.redirectUrl;
    } catch {
      toast.error('Could not start the Steam connection. Please try again.');
    }
  };

  const handleDisconnect = async () => {
    try {
      await disconnect.mutateAsync();
      toast.info('Steam disconnected. Your imported games stay in your collection.');
    } catch {
      toast.error('Could not disconnect Steam. Please try again.');
    }
  };

  const handleImport = async () => {
    if (steamMutationPending) return;
    try {
      const res = await importSelected.mutateAsync([...selected]);
      if (res.imported > 0) toast.success(`Imported ${res.imported} game${res.imported === 1 ? '' : 's'}`);
      if (res.alreadyImported > 0) toast.info(`${res.alreadyImported} were already in your collection`);
      // Show the imported games in the collection (spec: import → toast → /games).
      if (res.imported > 0) navigate('/games');
    } catch {
      toast.error('Import failed. Please try again.');
    }
  };

  // Re-submits already-imported titles so the server re-derives any missing or
  // stale remote covers (games imported before the 600x900 / hash-path cover fix).
  // Idempotent: import of an already-imported id only heals the cover, never
  // duplicates the game. Disabled when every imported title already has a local
  // cover (the preview doesn't report that, so it's always available; the server
  // simply no-ops on titles that need no healing).
  const handleRepairCovers = async () => {
    if (steamMutationPending) return;
    const importedIds = (games.data?.titles ?? [])
      .filter((g) => g.state === 'imported')
      .map((g) => g.externalGameId);
    if (importedIds.length === 0) {
      toast.info('Nothing to repair — no games imported yet.');
      return;
    }
    try {
      const res = await repairCovers.mutateAsync(importedIds);
      toast.success(
        res.imported > 0
          ? `Re-imported ${res.imported} game${res.imported === 1 ? '' : 's'} and refreshed covers`
          : `Refreshed covers for ${res.alreadyImported} imported game${res.alreadyImported === 1 ? '' : 's'}`,
      );
    } catch {
      toast.error('Could not refresh covers. Please try again.');
    }
  };

  const connected = connection.data?.connected === true;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <SectionHeading>
        <span className="inline-flex items-center gap-2">
          <img
            src="/brand/steam-logo.svg"
            alt="Steam"
            className="inline h-5 w-5 shrink-0"
          />
          Import from Steam
        </span>
      </SectionHeading>

      {!connected ? (
        <Card>
          <p className="mb-4 text-sm text-text-secondary">
            Connect your Steam account to import the games you already own into your
            collection. You'll be asked to authorise access on Steam.
          </p>
          <Button onClick={handleConnect} disabled={connect.isPending}>
            <img
              src="/brand/steam-logo.svg"
              alt=""
              className="mr-2 inline h-4 w-4"
              aria-hidden
            />
            {connect.isPending ? 'Connecting…' : 'Connect Steam'}
          </Button>
        </Card>
      ) : (
        <>
          <Card className="flex items-center justify-between gap-4">
            <div className="min-w-0">
              <p className="flex items-center gap-2 truncate font-semibold text-text-primary">
                <img
                  src="/brand/steam-logo.svg"
                  alt=""
                  className="inline h-4 w-4 shrink-0"
                  aria-hidden
                />
                <span className="truncate">{connection.data?.personaName ?? 'Steam account'}</span>
              </p>
              <p className="text-xs text-text-tertiary">
                {connection.data?.steamId} · Linked via Steam
              </p>
            </div>
            <Button variant="danger" onClick={handleDisconnect} disabled={disconnect.isPending}>
              {disconnect.isPending ? 'Disconnecting…' : 'Disconnect'}
            </Button>
          </Card>

          {games.isLoading && <Card>Loading your Steam games…</Card>}
          {games.isError && (
            <Card>
              <p className="text-sm text-error">
                Couldn't load your Steam games. Make sure your Steam profile's games are
                set to <strong>Public</strong> in Privacy Settings.
              </p>
            </Card>
          )}

          {!games.isLoading && games.data?.status === 'unavailable' && (
            <Card>
              <p className="text-sm text-error">
                Couldn't reach Steam to fetch your games. Make sure your Steam profile's
                games are set to <strong>Public</strong> in Privacy Settings, or try again
                in a moment.
              </p>
            </Card>
          )}

          {!games.isLoading && games.data?.status === 'ok' && (
            <>
              <input
                type="search"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                placeholder="Filter your owned games…"
                className="w-full rounded-md border border-border px-3 py-2 text-sm"
                aria-label="Filter owned games"
              />
              <div className="flex flex-col items-stretch gap-3 pt-2 sm:flex-row sm:items-center sm:gap-4">
                <label className="flex shrink-0 items-center gap-2 text-sm font-semibold text-text-secondary">
                  <input
                    type="checkbox"
                    checked={hideImported}
                    onChange={(e) => {
                      setHideImported(e.target.checked);
                      setOffset(0);
                    }}
                  />
                  Hide imported
                </label>
                <div className="flex w-full flex-wrap items-center justify-between gap-2 sm:ml-auto sm:w-auto sm:flex-nowrap sm:justify-end">
                  <label className="flex items-center gap-2 text-xs text-text-tertiary">
                    Games per page
                    <select
                      value={pageSize}
                      onChange={(e) => {
                        setPageSize(Number(e.target.value));
                        setOffset(0);
                      }}
                      className="rounded-md border border-border bg-card px-2 py-1 text-sm text-text-primary"
                    >
                      <option value={25}>25</option>
                      <option value={50}>50</option>
                      <option value={100}>100</option>
                    </select>
                  </label>
                  <Button variant="secondary" onClick={() => setOffset((o) => Math.max(0, o - pageSize))} disabled={offset === 0}>
                    Prev
                  </Button>
                  <span className="text-xs text-text-tertiary">
                    {games.data.total > 0 ? `${offset + 1}–${Math.min(offset + games.data.titles.length, games.data.total)} of ${games.data.total}` : ''}
                  </span>
                  <Button
                    variant="secondary"
                    onClick={() => setOffset((o) => o + pageSize)}
                    disabled={!games.data.truncated}
                  >
                    Next
                  </Button>
                </div>
              </div>
              <div className="flex items-center justify-between gap-4">
                <label className="flex items-center gap-2 text-sm font-semibold text-text-secondary">
                  <input type="checkbox" checked={allImportableSelected} onChange={toggleAll} disabled={games.data.titles.length === 0} />
                  Select all not-imported ({importable.length})
                </label>
                <Button
                  variant="primary"
                  onClick={handleImport}
                  disabled={selected.size === 0 || steamMutationPending}
                >
                  {importSelected.isPending
                    ? 'Importing…'
                    : `Import selected${selected.size ? ` (${selected.size})` : ''}`}
                </Button>
                {importedCount > 0 && (
                  <Button
                    variant="secondary"
                    onClick={handleRepairCovers}
                    disabled={steamMutationPending}
                    title="Re-derive missing or stale covers for games imported before the cover fix"
                  >
                    Repair covers
                  </Button>
                )}
              </div>

              <Card className="divide-y divide-border">
                {rendered.length === 0 ? (
                  <p className="px-3 py-2.5 text-sm text-text-tertiary">
                    {hideImported
                      ? filter.trim()
                        ? `No unimported games match “${filter}”.`
                        : 'All owned games are already in your collection.'
                      : filter.trim()
                        ? `No matches for “${filter}”.`
                        : <>No owned games returned. Make sure your Steam profile's game details are set to <strong className="text-text-secondary">Public</strong> in Privacy Settings, then try again.</>}
                  </p>
                ) : (
                  rendered.map((g) => {
                    const isImported = g.state === 'imported';
                    const checked = selected.has(g.externalGameId);
                    return (
                      <label
                        key={g.externalGameId}
                        className={`flex items-center gap-3 py-2.5 ${isImported ? 'opacity-60' : ''}`}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          disabled={isImported}
                          onChange={() => toggle(g.externalGameId)}
                        />
                        {g.logoUrl || g.iconUrl ? (
                          <img
                            src={g.logoUrl ?? g.iconUrl!}
                            alt=""
                            className="h-7 w-7 rounded-sm object-cover"
                            loading="lazy"
                          />
                        ) : (
                          <span className="h-7 w-7 rounded-sm bg-pill-bg" aria-hidden />
                        )}
                        <span className="min-w-0 flex-1 truncate text-sm text-text-primary">
                          {g.title}
                        </span>
                        <span className="whitespace-nowrap text-xs text-text-tertiary">
                          {formatPlaytime(g.playtimeMinutes)}
                        </span>
                        {isImported && (
                          <span className="text-xs font-semibold text-text-tertiary">In collection</span>
                        )}
                      </label>
                    );
                  })
                )}
              </Card>
            </>
          )}
        </>
      )}
    </div>
  );
}
