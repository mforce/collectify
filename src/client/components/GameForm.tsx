import { useEffect, useState } from 'react';
import { Button, CoverPreview, ExternalIdField, Field, Input, SearchableSelect, SectionHeading, Select, Textarea } from './ui';
import CoverEditor from './CoverEditor';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import BarcodeLookup from './BarcodeLookup';
import {
  COMPLETION_STATUSES,
  DIGITAL_STORES,
  GAME_PLATFORMS,
  gamePlatformLabel,
  type CompletionStatus,
  type DigitalStore,
  type Game,
  type GamePlatform,
} from '../services/types';
import { lookupGameByIgdbId, type GameLookupResult, type LookupByIdOutcome } from '../services/lookup';

interface Props {
  initial?: Game;
  /**
   * Lookup result to seed the form with on first mount (e.g. when the
   * user scanned a barcode on the list page). Runs the same import as
   * picking from in-form search.
   */
  prefillLookup?: GameLookupResult;
  /**
   * Soft-fallback prefill: just the barcode, no metadata. Set when the
   * list-page scanner couldn't resolve the UPC.
   */
  prefillBarcode?: string;
  submitting?: boolean;
  submitLabel?: string;
  onSubmit: (g: Game) => void;
  onDelete?: () => void;
}

const empty: Game = {
  title: '',
  platform: 'Other',
  isDigital: false,
  status: 'Owned',
  completionStatus: 'NotStarted',
  tags: [],
};

export default function GameForm({ initial, prefillLookup, prefillBarcode, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [g, setG] = useState<Game>(initial ?? empty);
  const [fetchState, setFetchState] = useState<{ status: 'idle' | 'loading'; message?: string }>({ status: 'idle' });
  useEffect(() => { if (initial) setG(initial); }, [initial]);

  const set = <K extends keyof Game>(k: K, v: Game[K]) => setG((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Game>) => setG((prev) => ({ ...prev, ...p }));

  const importLookup = (r: GameLookupResult) => {
    patch({
      title: r.title,
      year: r.year ?? null,
      // IGDB returns a canonical GamePlatform value (or null when none
      // of the listed platforms mapped). Keep what the user already had
      // when the result is platformless, so we don't overwrite a good
      // selection with Other.
      platform: r.platform ?? g.platform,
      publisher: r.publisher ?? null,
      developer: r.developer ?? null,
      imagePath: r.imageUrl ?? null,
      igdbId: r.provider === 'igdb' ? r.providerKey : g.igdbId ?? null,
    });
  };

  // Seed once on mount when a prefill arrives via navigation state.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { if (prefillLookup) importLookup(prefillLookup); }, []);

  // Soft-fallback prefill (list-page scan with no provider candidates):
  // drop the barcode into the field so the user can finish via title
  // search. Skipped when a full prefillLookup landed; that owns it.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (prefillBarcode && !prefillLookup) set('barcode', prefillBarcode);
  }, []);

  const runLookup = async (
    id: string,
    label: string,
    lookup: (id: string) => Promise<LookupByIdOutcome<GameLookupResult>>,
  ) => {
    const trimmed = id.trim();
    if (!trimmed) {
      setFetchState({ status: 'idle', message: `Enter a ${label} first.` });
      return;
    }
    setFetchState({ status: 'loading' });
    try {
      const outcome = await lookup(trimmed);
      if (outcome.kind === 'found') {
        importLookup(outcome.result);
        setFetchState({ status: 'idle', message: 'Populated from IGDB.' });
      } else if (outcome.kind === 'not-configured') {
        setFetchState({ status: 'idle', message: 'IGDB lookup not configured. Set the Twitch client id and secret.' });
      } else {
        setFetchState({ status: 'idle', message: `No game with ${label} ${trimmed}.` });
      }
    } catch (err) {
      setFetchState({ status: 'idle', message: (err as Error).message ?? 'Lookup failed.' });
    }
  };

  const fetchByIgdbId = () => runLookup(g.igdbId ?? '', 'IGDB ID', lookupGameByIgdbId);

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ ...g, title: g.title.trim() });
      }}
    >
      <OnlineSearch
        type="games"
        label="Search online (IGDB)"
        placeholder="e.g. The Witcher 3"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: [r.developer, r.platform ? gamePlatformLabel(r.platform) : null].filter(Boolean).join(' · ') || r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <BarcodeLookup
        type="games"
        onPick={importLookup}
        onBarcodeFallback={(code) => set('barcode', code)}
        fallbackLabel="Save this barcode anyway"
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: [r.developer, r.platform ? gamePlatformLabel(r.platform) : null].filter(Boolean).join(' · ') || r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <div className="flex flex-col-reverse sm:flex-row gap-4 items-start">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 flex-1 w-full">
          <Field label="Title">
            <Input value={g.title} onChange={(e) => set('title', e.target.value)} required />
          </Field>
          <Field label="Platform">
            <SearchableSelect
              value={g.platform}
              onChange={(v) => set('platform', v as GamePlatform)}
              options={GAME_PLATFORMS}
              placeholder="Type to search platforms…"
            />
            {g.platformLegacy && (
              // Stickier than a generic placeholder -- surfaces the
              // original free-text so the user can see what they had
              // typed and pick the closest canonical value. Saving the
              // form clears this field.
              <p className="mt-1 text-xs text-amber-300">
                Original: <span className="font-mono">{g.platformLegacy}</span> — pick a platform above to replace it.
              </p>
            )}
          </Field>
          <Field label="Year">
            <Input type="number" value={g.year ?? ''} onChange={(e) => set('year', e.target.value ? Number(e.target.value) : null)} />
          </Field>
          <Field label="Publisher">
            <Input value={g.publisher ?? ''} onChange={(e) => set('publisher', e.target.value || null)} />
          </Field>
          <Field label="Developer">
            <Input value={g.developer ?? ''} onChange={(e) => set('developer', e.target.value || null)} />
          </Field>
          <Field label="Barcode">
            <Input value={g.barcode ?? ''} onChange={(e) => set('barcode', e.target.value || null)} />
          </Field>
        </div>
        <div className="w-28 sm:w-36 shrink-0 space-y-2">
          <CoverPreview src={g.imagePath} alt={g.title ? `${g.title} cover` : ''} />
          <CoverEditor value={g.imagePath} onChange={(v) => set('imagePath', v)} />
        </div>
      </div>

      <div className="grid sm:grid-cols-2 gap-4 items-end">
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input
            type="checkbox"
            checked={g.isDigital}
            onChange={(e) => set('isDigital', e.target.checked)}
          />
          Digital copy
        </label>
        {g.isDigital && (
          <Field label="Store">
            <Select
              value={g.digitalStore ?? ''}
              onChange={(e) => set('digitalStore', (e.target.value || null) as DigitalStore | null)}
            >
              <option value="">— Select —</option>
              {DIGITAL_STORES.map((s) => (
                <option key={s.value} value={s.value}>{s.label}</option>
              ))}
            </Select>
          </Field>
        )}
      </div>

      <PersonalAcquisitionSection value={g} onChange={patch} />

      <SectionHeading>Playing</SectionHeading>
      <div className="grid sm:grid-cols-3 gap-4">
        <Field label="Completion">
          <Select
            value={g.completionStatus}
            onChange={(e) => set('completionStatus', e.target.value as CompletionStatus)}
          >
            {COMPLETION_STATUSES.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </Select>
        </Field>
        <Field label="Hours played">
          <Input
            type="number"
            min="0"
            value={g.hoursPlayed ?? ''}
            onChange={(e) => set('hoursPlayed', e.target.value ? Number(e.target.value) : null)}
          />
        </Field>
        <Field label="Last played">
          <Input
            type="date"
            value={g.lastPlayedOn ?? ''}
            onChange={(e) => set('lastPlayedOn', e.target.value || null)}
          />
        </Field>
      </div>

      <SectionHeading>External IDs</SectionHeading>
      <div className="grid sm:grid-cols-2 gap-4">
        <div className="space-y-1">
          <ExternalIdField
            label="IGDB ID"
            value={g.igdbId}
            onChange={(v) => set('igdbId', v)}
            urlPrefix="https://www.igdb.com/games/"
            placeholder="e.g. 1942"
          />
          <div>
            <Button
              type="button"
              variant="secondary"
              onClick={fetchByIgdbId}
              disabled={fetchState.status === 'loading' || !(g.igdbId ?? '').trim()}
              aria-label="Fetch metadata by IGDB ID"
            >
              {fetchState.status === 'loading' ? 'Fetching…' : 'Fetch metadata'}
            </Button>
          </div>
        </div>
      </div>
      {fetchState.message && (
        <div className="text-xs text-slate-400">{fetchState.message}</div>
      )}

      <Field label="Notes">
        <Textarea rows={3} value={g.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
      </Field>

      <div className="flex items-center justify-between">
        <Button type="submit" disabled={submitting}>{submitLabel}</Button>
        {onDelete && (
          <Button type="button" variant="danger" onClick={onDelete}>Delete</Button>
        )}
      </div>
    </form>
  );
}
