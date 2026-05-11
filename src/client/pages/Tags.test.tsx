import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import TagsPage from './Tags';

// Swap the data layer with controllable test doubles.
const mockMutate = vi.fn();
const mockUseTags = vi.fn();
const mockUseDeleteTag = vi.fn();

vi.mock('../services/tags', () => ({
  useTags: () => mockUseTags(),
  useDeleteTag: () => mockUseDeleteTag(),
}));

const renderPage = () =>
  render(
    <MemoryRouter>
      <TagsPage />
    </MemoryRouter>,
  );

describe('TagsPage', () => {
  beforeEach(() => {
    mockUseDeleteTag.mockReturnValue({ mutate: mockMutate, isPending: false, error: null });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });

  afterEach(() => {
    mockMutate.mockReset();
    mockUseTags.mockReset();
    mockUseDeleteTag.mockReset();
    vi.restoreAllMocks();
  });

  it('shows the empty state when the user has no tags', () => {
    mockUseTags.mockReturnValue({ data: [], isLoading: false, error: null });
    renderPage();
    expect(screen.getByText(/don't have any tags yet/i)).toBeInTheDocument();
  });

  it('lists each tag with its own delete button', () => {
    mockUseTags.mockReturnValue({
      data: [{ id: 1, name: 'sci-fi' }, { id: 2, name: 'heist' }],
      isLoading: false,
      error: null,
    });

    renderPage();

    expect(screen.getByText('sci-fi')).toBeInTheDocument();
    expect(screen.getByText('heist')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete tag sci-fi')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete tag heist')).toBeInTheDocument();
  });

  it('confirms then dispatches deletion when the user clicks delete', async () => {
    mockUseTags.mockReturnValue({
      data: [{ id: 7, name: 'sci-fi' }],
      isLoading: false,
      error: null,
    });

    renderPage();

    await userEvent.click(screen.getByLabelText('Delete tag sci-fi'));

    expect(window.confirm).toHaveBeenCalledOnce();
    // The page now passes onSuccess / onError options for toast wiring;
    // assert on the id positional arg and ignore the options object.
    expect(mockMutate).toHaveBeenCalledOnce();
    expect(mockMutate.mock.calls[0][0]).toBe(7);
  });

  it('skips deletion when the user cancels the confirm dialog', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    mockUseTags.mockReturnValue({
      data: [{ id: 7, name: 'sci-fi' }],
      isLoading: false,
      error: null,
    });

    renderPage();

    await userEvent.click(screen.getByLabelText('Delete tag sci-fi'));

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('renders a loading state while tags are fetching', () => {
    mockUseTags.mockReturnValue({ data: undefined, isLoading: true, error: null });
    renderPage();
    expect(screen.getByText('Loading…')).toBeInTheDocument();
  });

  it('surfaces a fetch error', () => {
    mockUseTags.mockReturnValue({ data: undefined, isLoading: false, error: new Error('nope') });
    renderPage();
    expect(screen.getByText(/failed to load tags/i)).toBeInTheDocument();
  });
});
