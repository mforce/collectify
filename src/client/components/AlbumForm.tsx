import { useEffect, useState } from 'react';
import { Button, Field, Input, SectionHeading, Select, Textarea } from './ui';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import { MUSIC_FORMATS, type Album } from '../services/types';

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
  useEffect(() => { if (initial) setA(initial); }, [initial]);

  const set = <K extends keyof Album>(k: K, v: Album[K]) => setA((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Album>) => setA((prev) => ({ ...prev, ...p }));

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ ...a, title: a.title.trim(), artistName: a.artistName.trim() });
      }}
    >
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
