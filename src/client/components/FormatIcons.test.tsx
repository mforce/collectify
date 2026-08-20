import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import { MovieFormatIcon, MusicFormatIcon, PlatformIcon } from './FormatIcons';

describe('FormatIcons', () => {
  it('renders an svg for each movie format', () => {
    for (const format of ['Dvd', 'BluRay', 'UhdBluRay', 'Vhs', 'Digital'] as const) {
      const { container } = render(<MovieFormatIcon format={format} />);
      expect(container.querySelector('svg'), format).not.toBeNull();
    }
  });

  it('renders nothing for movie format None (graceful, no blank box)', () => {
    const { container } = render(<MovieFormatIcon format="None" />);
    expect(container.querySelector('svg')).toBeNull();
  });

  it('renders an svg for each music format', () => {
    for (const format of ['Cd', 'Vinyl', 'Other'] as const) {
      const { container } = render(<MusicFormatIcon format={format} />);
      expect(container.querySelector('svg'), format).not.toBeNull();
    }
  });

  it('renders a platform icon for every GamePlatform value and falls back to a generic console for uncurated ones', () => {
    const platforms = [
      'Other', 'Pc', 'Mac', 'Mobile',
      'XboxOriginal', 'Xbox360', 'XboxOne', 'XboxSeriesXS',
      'Ps1', 'Ps2', 'Ps3', 'Ps4', 'Ps5', 'Psp', 'PsVita',
      'Nes', 'Snes', 'N64', 'GameCube', 'Wii', 'WiiU', 'Switch', 'Switch2',
      'GameBoy', 'GameBoyColor', 'GameBoyAdvance', 'NintendoDs', 'Nintendo3Ds',
      'SegaGenesis', 'SegaSaturn', 'SegaDreamcast',
    ] as const;
    for (const p of platforms) {
      const { container, unmount } = render(<PlatformIcon platform={p} />);
      expect(container.querySelector('svg'), p).not.toBeNull();
      unmount();
    }
  });

  it('honors a custom className on the svg (size override)', () => {
    const { container } = render(<PlatformIcon platform="Ps5" className="h-4 w-4" />);
    const svg = container.querySelector('svg');
    expect(svg?.getAttribute('class')).toBe('h-4 w-4');
  });

  it('uses a distinct generic console silhouette for uncurated platforms (fallback is exercised)', () => {
    // Ps5 maps to the curated 'tower' silhouette; Other has no entry and must
    // fall through to the generic console. If a platform were ever dropped from
    // PLATFORM_SHAPE, its icon would silently collapse to the generic fallback —
    // this assertion pins that these two do NOT share a path.
    const curated = render(<PlatformIcon platform="Ps5" />);
    const fallback = render(<PlatformIcon platform="Other" />);
    const curatedPath = curated.container.querySelector('path')?.getAttribute('d');
    const fallbackPath = fallback.container.querySelector('path')?.getAttribute('d');
    expect(curatedPath).not.toBeNull();
    expect(curatedPath).not.toBe(fallbackPath);
  });
});
