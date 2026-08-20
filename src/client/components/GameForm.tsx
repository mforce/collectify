import { useEffect, useState } from 'react';
import { Button, CoverPreview, ExternalIdField, Field, Input, SearchableSelect, SectionHeading, Select, Textarea } from './ui';
import CoverEditor from './CoverEditor';
import CoverFormLayout from './CoverFormLayout';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import BarcodeLookup from './BarcodeLookup';
import PhotoLookup from './PhotoLookup';
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
import { lookupGameByIgdbId, type GameLookupResult } from '../services/lookup';
import { useLookupProtocol } from '../hooks/useLookupProtocol';

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
  const [coverEditorExpanded, setCoverEditorExpanded] = useState(!g.imagePath);
  useEffect(() => { if (initial) setG(initial); }, [initial]);

  const set = <K extends keyof Game>(k: K, v: Game[K]) => setG((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Game>) => setG((prev) => ({ ...prev, ...p }));

  // Fill-only import: never overwrite a value the user (or a Steam import)
  // already set. Each field takes the IGDB value ONLY when the current one is
  // empty. This mirrors the backfill's Apply() so the edit page and the
  // background sweep agree on the no-clobber contract. IgdbId is the one
  // exception — it's always written (it's the linkage key, not display data).
  //
  // Platform is handled specially: IGDB's `platform` is just its FIRST-listed
  // platform, which may not match the user's own (a PC game whose IGDB entry
  // lists Xbox Series first). If the user already picked a platform, keep it;
  // only adopt the result's when theirs is unset (Other) or the result is
  // platformless. The real match signal is `platforms` (the full set).
  const { importLookup, runById, prefillEffect, fetchState } = useLookupProtocol<'games', Game, GameLookupResult>({
    getDraft: () => g, patchDraft: patch,
    importFields: (draft, r) => ({
      title: draft.title.trim() ? draft.title : r.title,
      platform: draft.platform && draft.platform !== 'Other' ? draft.platform : (r.platform ?? draft.platform),
      year: draft.year ?? r.year ?? null, publisher: draft.publisher ?? r.publisher ?? null,
      developer: draft.developer ?? r.developer ?? null,
      description: draft.description ? draft.description : r.description ?? null,
      imagePath: draft.imagePath ? draft.imagePath : r.imageUrl ?? null,
    }),
    providerNames: ['igdb'], linkageKey: (draft) => draft.igdbId ?? null,
    setLinkageKey: (draft, value) => ({ ...draft, igdbId: value }),
    byId: { label: 'IGDB ID', lookup: lookupGameByIgdbId },
  });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => prefillEffect(prefillLookup, prefillBarcode), []);

  // Platform label for a search result. IGDB's `platform` is only its
  // FIRST-listed platform, which easily misleads (a PC game's PC entry may
  // list Xbox Series first). Prefer a platform that matches the one the user
  // has set on the form, then the first whose label exists, so the dropdown
  // reads "PC" for a PC game instead of "Xbox Series X|S".
  const resultPlatformLabel = (r: GameLookupResult): string | null => {
    const set = r.platforms?.length ? r.platforms : (r.platform ? [r.platform] : []);
    if (set.length === 0) return null;
    const match = g.platform !== 'Other' && set.includes(g.platform)
      ? g.platform
      : set[0];
    return gamePlatformLabel(match);
  };

  const fetchByIgdbId = () => runById(g.igdbId ?? '');

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
        platform={g.platform === 'Other' ? undefined : g.platform}
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: [r.developer, resultPlatformLabel(r)].filter(Boolean).join(' · ') || r.description?.slice(0, 120),
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
          secondary: [r.developer, resultPlatformLabel(r)].filter(Boolean).join(' · ') || r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <PhotoLookup
        type="games"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: [r.developer, resultPlatformLabel(r)].filter(Boolean).join(' · ') || r.description?.slice(0, 120),
          image: r.imageUrl,
        })}
      />

      <CoverFormLayout
        fields={(
          <>
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
                <p className="mt-1 text-xs text-amber-400">
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
          </>
        )}
        preview={<CoverPreview src={g.imagePath} alt={g.title ? `${g.title} cover` : ''} />}
        editor={<CoverEditor value={g.imagePath} onChange={(v) => set('imagePath', v)} expanded={coverEditorExpanded} onExpandedChange={setCoverEditorExpanded} />}
        editorExpanded={coverEditorExpanded}
      />

      <div className="grid sm:grid-cols-2 gap-4 items-end">
        <label className="flex items-center gap-2 text-sm text-text-primary">
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
        <div className="text-xs text-text-secondary">{fetchState.message}</div>
      )}

      <Field label="Notes">
        <Textarea rows={3} value={g.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
      </Field>

      <div className="flex items-center justify-between">
        <Button type="submit" disabled={submitting} className="bg-games text-white hover:bg-games/85">
          {submitLabel}
        </Button>
        {onDelete && (
          <Button type="button" variant="danger" onClick={onDelete}>Delete</Button>
        )}
      </div>
    </form>
  );
}
