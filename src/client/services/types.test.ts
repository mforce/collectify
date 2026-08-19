import { describe, expect, it } from 'vitest';
import { digitalStoreLabel, musicFormatLabel, type DigitalStore, type MusicFormat } from './types';

describe('enum label helpers', () => {
  it('maps music formats and leaves unknown values to the call site', () => {
    expect(musicFormatLabel('Cd')).toBe('CD');
    expect(musicFormatLabel('Cassette' as MusicFormat)).toBeUndefined();
  });

  it('maps digital stores and leaves unknown values to the call site', () => {
    expect(digitalStoreLabel('Psn')).toBe('PlayStation Network');
    expect(digitalStoreLabel('Itch' as DigitalStore)).toBeUndefined();
  });
});
