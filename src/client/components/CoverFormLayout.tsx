import type { ReactNode } from 'react';

interface Props {
  fields: ReactNode;
  preview: ReactNode;
  editor: ReactNode;
  editorExpanded?: boolean;
}

/**
 * Responsive cover/form layout used by all media forms.
 *
 * The preview stays compact beside the main fields on desktop, but the
 * expanded editor gets a full-width row so URL/file controls never have
 * to squeeze into the narrow poster column.
 */
export default function CoverFormLayout({ fields, preview, editor, editorExpanded = false }: Props) {
  return (
    <div data-testid="cover-form-layout" className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_9rem] items-start">
      <div data-testid="cover-form-fields" className="order-2 grid grid-cols-1 sm:grid-cols-2 gap-4 w-full lg:order-1">
        {fields}
      </div>
      <div data-testid="cover-preview-column" className="order-1 w-28 sm:w-36 shrink-0 space-y-2 lg:order-2">
        {preview}
        {!editorExpanded && editor}
      </div>
      {editorExpanded && (
        <div data-testid="cover-editor-row" className="order-3 w-full min-w-0 lg:col-span-2">
          {editor}
        </div>
      )}
    </div>
  );
}
