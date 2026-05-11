import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button, CoverPreview, Field, Input, SearchableSelect, type SearchableOption } from './ui';

const platforms: SearchableOption[] = [
  { value: 'Pc', label: 'PC', group: 'Computer' },
  { value: 'Mac', label: 'Mac', group: 'Computer' },
  { value: 'Ps5', label: 'PlayStation 5', group: 'PlayStation' },
  { value: 'Switch', label: 'Switch', group: 'Nintendo' },
  { value: 'Switch2', label: 'Switch 2', group: 'Nintendo' },
  { value: 'Other', label: 'Other' },
];

describe('Button', () => {
  it('renders children and fires onClick when enabled', async () => {
    const onClick = vi.fn();
    render(<Button onClick={onClick}>Save</Button>);

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('does not fire onClick when disabled', async () => {
    const onClick = vi.fn();
    render(
      <Button onClick={onClick} disabled>
        Save
      </Button>,
    );

    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onClick).not.toHaveBeenCalled();
  });
});

describe('Field', () => {
  it('renders the label and the wrapped control together', () => {
    render(
      <Field label="Title">
        <Input placeholder="enter title" />
      </Field>,
    );

    expect(screen.getByText('Title')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('enter title')).toBeInTheDocument();
  });
});

describe('CoverPreview', () => {
  it('renders an <img> with the given src and alt when src is set', () => {
    render(<CoverPreview src="/covers/abc1234567890def" alt="Inception poster" />);

    const img = screen.getByRole('img', { name: 'Inception poster' });
    expect(img).toHaveAttribute('src', '/covers/abc1234567890def');
  });

  it.each([null, undefined, ''])('renders nothing when src is %p', (src) => {
    const { container } = render(<CoverPreview src={src as string | null | undefined} alt="x" />);
    expect(container).toBeEmptyDOMElement();
  });

  it('opens a lightbox when clicked and closes it on Escape', async () => {
    render(<CoverPreview src="/covers/abc" alt="Inception poster" />);

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /Inception poster/i }));

    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    // The lightbox renders a second img with the same src.
    const dialogImg = dialog.querySelector('img');
    expect(dialogImg).toHaveAttribute('src', '/covers/abc');

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('closes the lightbox when the backdrop is clicked', async () => {
    render(<CoverPreview src="/covers/abc" alt="Inception poster" />);

    await userEvent.click(screen.getByRole('button', { name: /Inception poster/i }));
    await userEvent.click(screen.getByRole('dialog'));

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});

describe('SearchableSelect', () => {
  it('shows the selected option label when closed', () => {
    render(<SearchableSelect value="Ps5" onChange={vi.fn()} options={platforms} />);

    expect(screen.getByRole('combobox')).toHaveValue('PlayStation 5');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('opens the listbox on focus and renders group headers', async () => {
    render(<SearchableSelect value="Pc" onChange={vi.fn()} options={platforms} />);

    await userEvent.click(screen.getByRole('combobox'));

    const list = screen.getByRole('listbox');
    expect(list).toBeInTheDocument();
    // Group headers from the options' `group` field.
    expect(within(list).getByText('Computer')).toBeInTheDocument();
    expect(within(list).getByText('PlayStation')).toBeInTheDocument();
  });

  it('filters by case-insensitive label substring as the user types', async () => {
    render(<SearchableSelect value="Pc" onChange={vi.fn()} options={platforms} />);
    await userEvent.click(screen.getByRole('combobox'));

    await userEvent.type(screen.getByRole('combobox'), 'switch');

    const options = screen.getAllByRole('option').map((el) => el.textContent?.trim());
    expect(options).toEqual(expect.arrayContaining(['Switch', 'Switch 2']));
    expect(options).not.toEqual(expect.arrayContaining(['PC']));
  });

  it('matches against the group name too (e.g. typing "nintendo" pulls Switch)', async () => {
    render(<SearchableSelect value="Pc" onChange={vi.fn()} options={platforms} />);
    await userEvent.click(screen.getByRole('combobox'));

    await userEvent.type(screen.getByRole('combobox'), 'nintendo');

    const options = screen.getAllByRole('option').map((el) => el.textContent?.trim());
    expect(options).toEqual(expect.arrayContaining(['Switch', 'Switch 2']));
    expect(options).not.toEqual(expect.arrayContaining(['PC', 'Mac', 'PlayStation 5']));
  });

  it('fires onChange with the picked option value and closes', async () => {
    const onChange = vi.fn();
    render(<SearchableSelect value="Pc" onChange={onChange} options={platforms} />);
    await userEvent.click(screen.getByRole('combobox'));

    await userEvent.click(screen.getByRole('option', { name: 'PlayStation 5' }));

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(onChange).toHaveBeenCalledWith('Ps5');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('supports keyboard navigation: ArrowDown + Enter selects', async () => {
    const onChange = vi.fn();
    render(<SearchableSelect value="Pc" onChange={onChange} options={platforms} />);
    const input = screen.getByRole('combobox');
    await userEvent.click(input);
    // Filter so the active index lands somewhere predictable.
    await userEvent.type(input, 'PlayStation');
    await userEvent.keyboard('{Enter}');

    expect(onChange).toHaveBeenCalledWith('Ps5');
  });

  it('shows a "No matches" hint when the filter eliminates everything', async () => {
    render(<SearchableSelect value="Pc" onChange={vi.fn()} options={platforms} />);
    await userEvent.click(screen.getByRole('combobox'));

    await userEvent.type(screen.getByRole('combobox'), 'qzx-not-a-real-platform');

    expect(screen.getByText(/no matches/i)).toBeInTheDocument();
  });

  it('Escape closes without firing onChange', async () => {
    const onChange = vi.fn();
    render(<SearchableSelect value="Pc" onChange={onChange} options={platforms} />);
    await userEvent.click(screen.getByRole('combobox'));

    await userEvent.keyboard('{Escape}');

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(onChange).not.toHaveBeenCalled();
  });
});
