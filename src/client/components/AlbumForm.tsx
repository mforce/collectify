import { useEffect, useState } from 'react';
import { Button, CoverPreview, ExternalIdField, Field, Input, SectionHeading, Select, Textarea } from './ui';
import CoverEditor from './CoverEditor';
import CoverFormLayout from './CoverFormLayout';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import BarcodeLookup from './BarcodeLookup';
import PhotoLookup from './PhotoLookup';
import { MUSIC_FORMATS, type Album } from '../services/types';
import { MusicFormatIcon } from './FormatIcons';
import { lookupAlbumByMbid, type MusicLookupResult } from '../services/lookup';
import { useLookupProtocol } from '../hooks/useLookupProtocol';

interface Props {
  initial?: Album;
  /**
   * Lookup result to seed the form with on first mount (e.g. when the
   * user scanned a barcode on the list page). Runs the same import +
   * enrichment chain as picking from in-form search.
   */
  prefillLookup?: MusicLookupResult;
  /**
   * Soft-fallback prefill: just the barcode, no metadata. Set when the
   * list-page scanner couldn't resolve the UPC.
   */
  prefillBarcode?: string;
  submitting?: boolean;
  submitLabel?: string;
  onSubmit: (a: Album) => void;
  onDelete?: () => void;
}

const empty: Album = {
  title: '',
  artistName: '',
  format: 'Cd',
  status: 'Owned',
  listenCount: 0,
  tags: [],
};

export default function AlbumForm({ initial, prefillLookup, prefillBarcode, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [a, setA] = useState<Album>(initial ?? empty);
  const [coverEditorExpanded, setCoverEditorExpanded] = useState(!a.imagePath);
  useEffect(() => { if (initial) setA(initial); }, [initial]);

  const set = <K extends keyof Album>(k: K, v: Album[K]) => setA((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Album>) => setA((prev) => ({ ...prev, ...p }));

  const { importLookup, runById, prefillEffect, fetchState } = useLookupProtocol<'music', Album, MusicLookupResult>({
    getDraft: () => a, patchDraft: patch,
    importFields: (_draft, r) => ({ title: r.title, artistName: r.artistName, year: r.year ?? null, releaseDate: r.releaseDate ?? null, label: r.label ?? null, imagePath: r.imageUrl ?? null }),
    providerNames: ['musicbrainz'], linkageKey: (draft) => draft.musicBrainzReleaseId ?? null,
    setLinkageKey: (draft, value) => ({ ...draft, musicBrainzReleaseId: value }),
    enrich: { keyOf: (draft) => draft.musicBrainzReleaseId ?? null, run: lookupAlbumByMbid,
      fill: (draft, r) => ({ ...draft, artistName: draft.artistName || r.artistName, year: draft.year ?? r.year ?? null, releaseDate: draft.releaseDate ?? r.releaseDate ?? null, label: draft.label ?? r.label ?? null }),
      shouldRun: (r) => r.provider === 'musicbrainz' && (!r.artistName || r.year == null || !r.label),
      loadingLabel: 'Loading artist & label…', successLabel: 'Populated from MusicBrainz.', notConfiguredLabel: 'MusicBrainz lookup not configured. Set the User-Agent.' },
    byId: { label: 'MusicBrainz Release ID', entityNoun: 'release', notConfiguredHint: 'MusicBrainz lookup not configured. Set the User-Agent.', lookup: lookupAlbumByMbid },
  });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => prefillEffect(prefillLookup, prefillBarcode), []);
  const fetchByMbid = () => runById(a.musicBrainzReleaseId ?? '');

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ ...a, title: a.title.trim(), artistName: a.artistName.trim() });
      }}
    >
      <OnlineSearch
        type="music"
        label="Search online (MusicBrainz)"
        placeholder="e.g. OK Computer"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.artistName + (r.label ? ` · ${r.label}` : ''),
          image: r.imageUrl,
        })}
      />

      <BarcodeLookup
        type="music"
        onPick={importLookup}
        onBarcodeFallback={(code) => set('barcode', code)}
        fallbackLabel="Save this barcode anyway"
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.artistName + (r.label ? ` · ${r.label}` : ''),
          image: r.imageUrl,
        })}
      />

      <PhotoLookup
        type="music"
        onPick={importLookup}
        renderItem={(r) => ({
          primary: r.title + (r.year ? ` (${r.year})` : ''),
          secondary: r.artistName + (r.label ? ` · ${r.label}` : ''),
          image: r.imageUrl,
        })}
      />

      <CoverFormLayout
        fields={(
          <>
            <Field label="Title">
              <Input value={a.title} onChange={(e) => set('title', e.target.value)} required />
            </Field>
            <Field label="Artist">
              <Input value={a.artistName} onChange={(e) => set('artistName', e.target.value)} required />
            </Field>
            <Field label="Year">
              <Input type="number" value={a.year ?? ''} onChange={(e) => set('year', e.target.value ? Number(e.target.value) : null)} />
            </Field>
            <Field label="Release date"><Input type="date" value={a.releaseDate ?? ''} onChange={(e) => set('releaseDate', e.target.value || null)} /></Field>
            <Field label="Format">
              <div className="relative">
                <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-secondary">
                  <MusicFormatIcon format={a.format} className="h-4 w-4" />
                </span>
                <Select value={a.format} onChange={(e) => set('format', e.target.value as Album['format'])} className="pl-10">
                  {MUSIC_FORMATS.map((f) => (
                    <option key={f.value} value={f.value}>{f.label}</option>
                  ))}
                </Select>
              </div>
            </Field>
            <Field label="Label">
              <Input value={a.label ?? ''} onChange={(e) => set('label', e.target.value || null)} />
            </Field>
            <Field label="Genres">
              <Input value={a.genres ?? ''} onChange={(e) => set('genres', e.target.value || null)} />
            </Field>
            <Field label="Barcode">
              <Input value={a.barcode ?? ''} onChange={(e) => set('barcode', e.target.value || null)} />
            </Field>
          </>
        )}
        preview={<CoverPreview src={a.imagePath} alt={a.title ? `${a.title} cover` : ''} />}
        editor={<CoverEditor value={a.imagePath} onChange={(v) => set('imagePath', v)} expanded={coverEditorExpanded} onExpandedChange={setCoverEditorExpanded} />}
        editorExpanded={coverEditorExpanded}
      />

      <PersonalAcquisitionSection value={a} onChange={patch} />

      <SectionHeading>Listening</SectionHeading>
      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Listen count">
          <Input
            type="number"
            min="0"
            value={a.listenCount}
            onChange={(e) => set('listenCount', Number(e.target.value || 0))}
          />
        </Field>
        <Field label="Last played">
          <Input
            type="date"
            value={a.lastPlayedOn ?? ''}
            onChange={(e) => set('lastPlayedOn', e.target.value || null)}
          />
        </Field>
      </div>

      <SectionHeading>External IDs</SectionHeading>
      <div className="grid sm:grid-cols-2 gap-4">
        <div className="space-y-1">
          <ExternalIdField
            label="MusicBrainz release ID"
            value={a.musicBrainzReleaseId}
            onChange={(v) => set('musicBrainzReleaseId', v)}
            urlPrefix="https://musicbrainz.org/release/"
            placeholder="e.g. f4e51c80-99e2-39e1-8062-c9b8e2685bdf"
          />
          <div>
            <Button
              type="button"
              variant="secondary"
              onClick={fetchByMbid}
              disabled={fetchState.status === 'loading' || !(a.musicBrainzReleaseId ?? '').trim()}
              aria-label="Fetch metadata by MusicBrainz Release ID"
            >
              {fetchState.status === 'loading' ? 'Fetching…' : 'Fetch metadata'}
            </Button>
          </div>
        </div>
        <ExternalIdField
          label="Discogs ID"
          value={a.discogsId}
          onChange={(v) => set('discogsId', v)}
          urlPrefix="https://www.discogs.com/release/"
          placeholder="e.g. 12345"
        />
      </div>
      {fetchState.message && (
        <div className="text-xs text-text-secondary">{fetchState.message}</div>
      )}

      <Field label="Notes">
        <Textarea rows={3} value={a.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
      </Field>

      <div className="flex items-center justify-between">
        <Button type="submit" disabled={submitting} className="bg-music text-white hover:bg-music/85">
          {submitLabel}
        </Button>
        {onDelete && (
          <Button type="button" variant="danger" onClick={onDelete}>Delete</Button>
        )}
      </div>
    </form>
  );
}
