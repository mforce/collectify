import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button, CoverPreview, Field, Input } from './ui';

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
});
