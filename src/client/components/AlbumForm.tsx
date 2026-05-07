import { useEffect, useState } from 'react';
import { Button, ExternalIdField, Field, Input, SectionHeading, Select, Textarea } from './ui';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import OnlineSearch from './OnlineSearch';
import { MUSIC_FORMATS, type Album } from '../services/types';
import { lookupAlbumByMbid, type LookupByIdOutcome, type MusicLookupResult } from '../services/lookup';

interface Props {
  initial?: Album;
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

export default function AlbumForm({ initial, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [a, setA] = useState<Album>(initial ?? empty);
  const [fetchState, setFetchState] = useState<{ status: 'idle' | 'loading'; message?: string }>({ status: 'idle' });
  useEffect(() => { if (initial) setA(initial); }, [initial]);

  const set = <K extends keyof Album>(k: K, v: Album[K]) => setA((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Album>) => setA((prev) => ({ ...prev, ...p }));

  const importLookup = (r: MusicLookupResult) => {
    patch({
      title: r.title,
      artistName: r.artistName,
      year: r.year ?? null,
      label: r.label ?? null,
      imagePath: r.imageUrl ?? null,
      musicBrainzReleaseId: r.provider === 'musicbrainz' ? r.providerKey : a.musicBrainzReleaseId ?? null,
    });
  };

  const runLookup = async (
    id: string,
    label: string,
    lookup: (id: string) => Promise<LookupByIdOutcome<MusicLookupResult>>,
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
        setFetchState({ status: 'idle', message: 'Populated from MusicBrainz.' });
      } else if (outcome.kind === 'not-configured') {
        setFetchState({ status: 'idle', message: 'MusicBrainz lookup not configured. Set the User-Agent.' });
      } else {
        setFetchState({ status: 'idle', message: `No release with ${label} ${trimmed}.` });
      }
    } catch (err) {
      setFetchState({ status: 'idle', message: (err as Error).message ?? 'Lookup failed.' });
    }
  };

  const fetchByMbid = () => runLookup(a.musicBrainzReleaseId ?? '', 'MusicBrainz Release ID', lookupAlbumByMbid);

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

      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Title">
          <Input value={a.title} onChange={(e) => set('title', e.target.value)} required />
        </Field>
        <Field label="Artist">
          <Input value={a.artistName} onChange={(e) => set('artistName', e.target.value)} required />
        </Field>
        <Field label="Year">
          <Input type="number" value={a.year ?? ''} onChange={(e) => set('year', e.target.value ? Number(e.target.value) : null)} />
        </Field>
        <Field label="Format">
          <Select value={a.format} onChange={(e) => set('format', e.target.value as Album['format'])}>
            {MUSIC_FORMATS.map((f) => (
              <option key={f.value} value={f.value}>{f.label}</option>
            ))}
          </Select>
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
      </div>

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
        <div className="text-xs text-slate-400">{fetchState.message}</div>
      )}

      <Field label="Notes">
        <Textarea rows={3} value={a.notes ?? ''} onChange={(e) => set('notes', e.target.value || null)} />
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
