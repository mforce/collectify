import { describe, expect, it } from 'vitest';
import { digitalStoresLabel, musicFormatLabel, type DigitalStore, type MusicFormat } from './types';

describe('enum label helpers', () => {
  it('maps music formats and leaves unknown values to the call site', () => {
    expect(musicFormatLabel('Cd')).toBe('CD');
    expect(musicFormatLabel('Cassette' as MusicFormat)).toBeUndefined();
  });

  it('renders a DigitalStores bitmask as a comma-joined label list', () => {
    expect(digitalStoresLabel(16)).toBe('PlayStation Network');
    expect(digitalStoresLabel(5)).toBe('Steam, Epic'); // Steam|Epic = 1|4
    expect(digitalStoresLabel(0)).toBe('');
  });
});
