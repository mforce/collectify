import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Button, Field, Input } from './ui';

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
