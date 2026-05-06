import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TagInput } from './ui';

describe('TagInput', () => {
  it('commits a typed tag on Enter, lowercased', async () => {
    const onChange = vi.fn();
    render(<TagInput value={[]} onChange={onChange} />);

    const input = screen.getByLabelText('Add tag');
    await userEvent.type(input, 'Sci-Fi{Enter}');

    expect(onChange).toHaveBeenCalledWith(['sci-fi']);
  });

  it('commits on comma', async () => {
    const onChange = vi.fn();
    render(<TagInput value={[]} onChange={onChange} />);

    await userEvent.type(screen.getByLabelText('Add tag'), 'heist,');

    expect(onChange).toHaveBeenCalledWith(['heist']);
  });

  it('does not add a duplicate tag (case-insensitive)', async () => {
    const onChange = vi.fn();
    render(<TagInput value={['sci-fi']} onChange={onChange} />);

    await userEvent.type(screen.getByLabelText('Add tag'), 'SCI-FI{Enter}');

    expect(onChange).not.toHaveBeenCalled();
  });

  it('removes a tag via its remove button', async () => {
    const onChange = vi.fn();
    render(<TagInput value={['sci-fi', 'heist']} onChange={onChange} />);

    await userEvent.click(screen.getByLabelText('Remove tag sci-fi'));

    expect(onChange).toHaveBeenCalledWith(['heist']);
  });

  it('Backspace on empty input pops the last tag', async () => {
    const onChange = vi.fn();
    render(<TagInput value={['sci-fi', 'heist']} onChange={onChange} />);

    const input = screen.getByLabelText('Add tag');
    input.focus();
    await userEvent.keyboard('{Backspace}');

    expect(onChange).toHaveBeenCalledWith(['sci-fi']);
  });

  it('quick-add buttons appear for unselected suggestions', async () => {
    const onChange = vi.fn();
    render(
      <TagInput
        value={['sci-fi']}
        onChange={onChange}
        suggestions={['sci-fi', 'heist', 'drama']}
      />,
    );

    // Already-selected suggestion is filtered out.
    expect(screen.queryByRole('button', { name: '+ sci-fi' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: '+ heist' }));
    expect(onChange).toHaveBeenCalledWith(['sci-fi', 'heist']);
  });
});
