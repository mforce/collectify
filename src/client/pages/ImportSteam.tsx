import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Button, Card, SectionHeading } from '../components/ui';
import { toast } from '../components/toaster';
import {
  useSteamConnect,
  useSteamConnection,
  useSteamDisconnect,
  useSteamGames,
  useSteamImport,
} from '../services/steam';

function formatPlaytime(minutes: number): string {
  if (!minutes) return '';
  const h = Math.floor(minutes / 60);
  return h >= 1 ? `${h}h` : `${minutes}m`;
}

export default function ImportSteam() {
  const [params, setParams] = useSearchParams();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState('');

  const connection = useSteamConnection();
  const connect = useSteamConnect();
  const games = useSteamGames(connection.data?.connected === true);
  const doImport = useSteamImport(() => setSelected(new Set()));
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

  const filtered = useMemo(() => {
    const q = filter.trim().toLowerCase();
    if (!q) return games.data?.titles ?? [];
    return (games.data?.titles ?? []).filter((g) => g.title.toLowerCase().includes(q));
  }, [games.data, filter]);

  const toggle = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const allImportableSelected =
    importable.length > 0 && importable.every((g) => selected.has(g.externalGameId));

  const toggleAll = () =>
    setSelected(allImportableSelected ? new Set() : new Set(importable.map((g) => g.externalGameId)));

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
    try {
      const res = await doImport.mutateAsync([...selected]);
      if (res.imported > 0) toast.success(`Imported ${res.imported} game${res.imported === 1 ? '' : 's'}`);
      if (res.alreadyImported > 0) toast.info(`${res.alreadyImported} were already in your collection`);
    } catch {
      toast.error('Import failed. Please try again.');
    }
  };

  const connected = connection.data?.connected === true;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <SectionHeading>Import from Steam</SectionHeading>

      {!connected ? (
        <Card>
          <p className="mb-4 text-sm text-text-secondary">
            Connect your Steam account to import the games you already own into your
            collection. You'll be asked to authorise access on Steam.
          </p>
          <Button onClick={handleConnect} disabled={connect.isPending}>
            {connect.isPending ? 'Connecting…' : 'Connect Steam'}
          </Button>
        </Card>
      ) : (
        <>
          <Card className="flex items-center justify-between gap-4">
            <div className="min-w-0">
              <p className="truncate font-semibold text-text-primary">
                {connection.data?.personaName ?? 'Steam account'}
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

          {!games.isLoading && games.data?.status === 'ok' && games.data.titles.length === 0 && (
            <Card>
              <p className="text-sm text-text-secondary">
                No owned games returned. Make sure your Steam profile's game details are set
                to <strong>Public</strong> in Privacy Settings, then try again.
              </p>
            </Card>
          )}

          {games.data?.status === 'ok' && games.data.titles.length > 0 && (
            <>
              <input
                type="search"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                placeholder="Filter your owned games…"
                className="w-full rounded-md border border-border px-3 py-2 text-sm"
                aria-label="Filter owned games"
              />
              <div className="flex items-center justify-between gap-4">
                <label className="flex items-center gap-2 text-sm font-semibold text-text-secondary">
                  <input type="checkbox" checked={allImportableSelected} onChange={toggleAll} />
                  Select all not-imported ({importable.length})
                  {games.data.truncated && (
                    <span className="font-normal text-xs text-text-tertiary">
                      (showing first {games.data.titles.length})
                    </span>
                  )}
                </label>
                <Button
                  variant="primary"
                  onClick={handleImport}
                  disabled={selected.size === 0 || doImport.isPending}
                >
                  {doImport.isPending
                    ? 'Importing…'
                    : `Import selected${selected.size ? ` (${selected.size})` : ''}`}
                </Button>
              </div>

              <Card className="divide-y divide-border">
                {filtered.length === 0 ? (
                  <p className="px-3 py-2.5 text-sm text-text-tertiary">No matches for “{filter}”.</p>
                ) : (
                  filtered.map((g) => {
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
                        {g.iconUrl ? (
                          <img
                            src={g.iconUrl}
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
