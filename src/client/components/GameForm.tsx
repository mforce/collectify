import { useEffect, useState } from 'react';
import { Button, CoverPreview, ExternalIdField, Field, Input, SectionHeading, Select, Textarea } from './ui';
import PersonalAcquisitionSection from './PersonalAcquisitionSection';
import {
  COMPLETION_STATUSES,
  DIGITAL_STORES,
  type CompletionStatus,
  type DigitalStore,
  type Game,
} from '../services/types';

interface Props {
  initial?: Game;
  submitting?: boolean;
  submitLabel?: string;
  onSubmit: (g: Game) => void;
  onDelete?: () => void;
}

const empty: Game = {
  title: '',
  isDigital: false,
  status: 'Owned',
  completionStatus: 'NotStarted',
  tags: [],
};

export default function GameForm({ initial, submitting, submitLabel = 'Save', onSubmit, onDelete }: Props) {
  const [g, setG] = useState<Game>(initial ?? empty);
  useEffect(() => { if (initial) setG(initial); }, [initial]);

  const set = <K extends keyof Game>(k: K, v: Game[K]) => setG((prev) => ({ ...prev, [k]: v }));
  const patch = (p: Partial<Game>) => setG((prev) => ({ ...prev, ...p }));

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({ ...g, title: g.title.trim() });
      }}
    >
      <div className="flex flex-col-reverse sm:flex-row gap-4 items-start">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 flex-1 w-full">
          <Field label="Title">
            <Input value={g.title} onChange={(e) => set('title', e.target.value)} required />
          </Field>
          <Field label="Platform">
            <Input value={g.platform ?? ''} onChange={(e) => set('platform', e.target.value || null)} placeholder="e.g. PS5, Switch, PC" />
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
        <CoverPreview
          src={g.imagePath}
          alt={g.title ? `${g.title} cover` : ''}
          className="w-28 sm:w-36 shrink-0"
        />
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
        <ExternalIdField
          label="IGDB ID"
          value={g.igdbId}
          onChange={(v) => set('igdbId', v)}
          urlPrefix="https://www.igdb.com/games/"
          placeholder="e.g. hades"
        />
      </div>

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
