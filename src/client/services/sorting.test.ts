import { describe, expect, it } from 'vitest';
import { readSortState, serializeSortParams, sortOptions, writeSortState, type SortState } from './sorting';

describe('sortOptions', () => {
  it('sortOptions_ForEachType_ContainsSharedAndOnlyItsOwnExtras', () => {
    const shared = ['title', 'year', 'addedAt', 'personalRating'];

    const movieValues = sortOptions('movies').map((o) => o.value);
    const musicValues = sortOptions('music').map((o) => o.value);
    const gameValues = sortOptions('games').map((o) => o.value);

    for (const key of shared) {
      expect(movieValues).toContain(key);
      expect(musicValues).toContain(key);
      expect(gameValues).toContain(key);
    }

    expect(movieValues).toEqual(expect.arrayContaining(['watchStatus', 'watchCount']));
    expect(musicValues).toEqual(expect.arrayContaining(['listenCount']));
    expect(gameValues).toEqual(expect.arrayContaining(['hoursPlayed', 'completionStatus']));

    // Cross-type extras must never leak onto another type's option list.
    expect(movieValues).not.toEqual(expect.arrayContaining(['listenCount', 'hoursPlayed', 'completionStatus']));
    expect(musicValues).not.toEqual(expect.arrayContaining(['watchStatus', 'watchCount', 'hoursPlayed', 'completionStatus']));
    expect(gameValues).not.toEqual(expect.arrayContaining(['watchStatus', 'watchCount', 'listenCount']));
  });
});

describe('readSortState', () => {
  it('readSortState_AbsentValuesUsesAddedAtDescending', () => {
    const { state, needsReplace } = readSortState('movies', new URLSearchParams());

    expect(state).toEqual({ field: 'addedAt', direction: 'desc' });
    expect(needsReplace).toBe(false);
  });

  it('readSortState_InvalidOrRepeatedValuesReturnsCanonicalDefaultsAndNeedsReplace', () => {
    const malformedQueryStrings = [
      'sort=bogus',
      'dir=bogus',
      'sort=title&sort=year',
      'dir=asc&dir=desc',
      'sort=',
      'dir=',
    ];

    for (const qs of malformedQueryStrings) {
      const { state, needsReplace } = readSortState('movies', new URLSearchParams(qs));
      expect(state).toEqual({ field: 'addedAt', direction: 'desc' });
      expect(needsReplace).toBe(true);
    }
  });
});

describe('writeSortState', () => {
  it('writeSortState_PreservesQueryFiltersAndUnrelatedParams', () => {
    const current = new URLSearchParams('q=heat&director=Nolan&sort=title&dir=asc');

    const next = writeSortState(current, { field: 'year', direction: 'desc' } satisfies SortState<'movies'>);

    expect(next.get('q')).toBe('heat');
    expect(next.get('director')).toBe('Nolan');
    expect(next.get('sort')).toBe('year');
    expect(next.get('dir')).toBe('desc');
  });
});

describe('serializeSortParams', () => {
  it('serializeSortParams_EmitsCanonicalSortAndDirection', () => {
    const params = serializeSortParams({ field: 'personalRating', direction: 'asc' } satisfies SortState<'games'>);

    expect(params.toString()).toBe('sort=personalRating&dir=asc');
  });
});
