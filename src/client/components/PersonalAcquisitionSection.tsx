import {
  COLLECTION_STATUSES,
  CONDITIONS,
  type CollectionItemBase,
  type CollectionStatus,
  type Condition,
} from '../api/types';
import { useTags } from '../api/tags';
import { Field, Input, RatingInput, SectionHeading, Select, TagInput, Textarea } from './ui';

interface Props<T extends CollectionItemBase> {
  value: T;
  onChange: (patch: Partial<T>) => void;
}

/**
 * The shared "Personal" + "Acquisition" + "Tags" sections rendered by every
 * collection form. The host form passes its full state and a partial-update
 * callback; this component only touches the shared CollectionItemBase fields.
 */
export default function PersonalAcquisitionSection<T extends CollectionItemBase>({ value, onChange }: Props<T>) {
  const tags = useTags();
  const suggestions = (tags.data ?? []).map((t) => t.name);

  return (
    <>
      <SectionHeading>Personal</SectionHeading>

      <Field label="Description">
        <Textarea
          rows={2}
          value={value.description ?? ''}
          onChange={(e) => onChange({ description: e.target.value || null } as Partial<T>)}
        />
      </Field>

      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Status">
          <Select
            value={value.status}
            onChange={(e) => onChange({ status: e.target.value as CollectionStatus } as Partial<T>)}
          >
            {COLLECTION_STATUSES.map((s) => (
              <option key={s.value} value={s.value}>{s.label}</option>
            ))}
          </Select>
        </Field>

        <Field label="Condition">
          <Select
            value={value.condition ?? ''}
            onChange={(e) => onChange({ condition: (e.target.value || null) as Condition | null } as Partial<T>)}
          >
            <option value="">— Not set —</option>
            {CONDITIONS.map((c) => (
              <option key={c.value} value={c.value}>{c.label}</option>
            ))}
          </Select>
        </Field>
      </div>

      <Field label="Personal rating">
        <RatingInput
          value={value.personalRating}
          onChange={(n) => onChange({ personalRating: n } as Partial<T>)}
        />
      </Field>

      <Field label="Tags">
        <TagInput
          value={value.tags ?? []}
          onChange={(next) => onChange({ tags: next } as Partial<T>)}
          suggestions={suggestions}
        />
      </Field>

      <SectionHeading>Acquisition</SectionHeading>

      <div className="grid sm:grid-cols-2 gap-4">
        <Field label="Acquired on">
          <Input
            type="date"
            value={value.acquiredOn ?? ''}
            onChange={(e) => onChange({ acquiredOn: e.target.value || null } as Partial<T>)}
          />
        </Field>

        <Field label="Source">
          <Input
            value={value.acquisitionSource ?? ''}
            placeholder="e.g. Amazon, local store"
            onChange={(e) => onChange({ acquisitionSource: e.target.value || null } as Partial<T>)}
          />
        </Field>

        <Field label="Price">
          <Input
            type="number"
            step="0.01"
            min="0"
            value={value.acquisitionPrice ?? ''}
            onChange={(e) => onChange({ acquisitionPrice: e.target.value ? Number(e.target.value) : null } as Partial<T>)}
          />
        </Field>

        <Field label="Currency (ISO 4217)">
          <Input
            value={value.acquisitionCurrency ?? ''}
            placeholder="USD"
            maxLength={3}
            onChange={(e) => onChange({ acquisitionCurrency: e.target.value ? e.target.value.toUpperCase() : null } as Partial<T>)}
          />
        </Field>
      </div>
    </>
  );
}
