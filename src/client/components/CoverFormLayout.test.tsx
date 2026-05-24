import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import CoverFormLayout from './CoverFormLayout';

describe('CoverFormLayout', () => {
  it('keeps collapsed cover actions under the compact preview', () => {
    render(
      <CoverFormLayout
        fields={<input aria-label="Title" />}
        preview={<div>Preview</div>}
        editor={<button type="button">Change cover</button>}
      />,
    );

    expect(screen.getByTestId('cover-form-layout')).toHaveClass('lg:grid-cols-[minmax(0,1fr)_9rem]');
    expect(screen.getByTestId('cover-preview-column')).toHaveClass('w-28', 'sm:w-36', 'shrink-0');
    expect(screen.getByTestId('cover-preview-column')).toContainElement(screen.getByRole('button', { name: /change cover/i }));
    expect(screen.queryByTestId('cover-editor-row')).not.toBeInTheDocument();
  });

  it('renders the expanded editor in a full-width row', () => {
    render(
      <CoverFormLayout
        fields={<input aria-label="Title" />}
        preview={<div>Preview</div>}
        editor={<div>Expanded editor</div>}
        editorExpanded
      />,
    );

    expect(screen.getByTestId('cover-preview-column')).not.toHaveTextContent('Expanded editor');
    expect(screen.getByTestId('cover-editor-row')).toHaveClass('w-full', 'min-w-0', 'lg:col-span-2');
    expect(screen.getByTestId('cover-editor-row')).toHaveTextContent('Expanded editor');
  });
});
