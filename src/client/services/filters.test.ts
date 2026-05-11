import { describe, expect, it } from 'vitest';
import { activeFilterCount, filtersToParams } from './filters';

describe('filtersToParams', () => {
  it('omits undefined / null / empty / NaN values', () => {
    const params = filtersToParams({
      yearFrom: undefined,
      yearTo: null,
      director: '',
      ratingMin: Number.NaN,
      studio: 'Warner',
    });

    expect(params.toString()).toBe('studio=Warner');
  });

  it('emits one entry per array element so the server sees a repeated query param', () => {
    const params = filtersToParams({ tag: ['scifi', 'noir'] });
    // URLSearchParams stringifies arrays as repeated keys, which is
    // exactly what the [FromQuery(Name="tag")] string[] binder expects.
    expect(params.getAll('tag')).toEqual(['scifi', 'noir']);
  });

  it('round-trips booleans and numbers as strings', () => {
    const params = filtersToParams({ digital: true, ratingMin: 7 });
    expect(params.get('digital')).toBe('true');
    expect(params.get('ratingMin')).toBe('7');
  });

  it('drops empty strings inside arrays', () => {
    const params = filtersToParams({ tag: ['scifi', '', 'noir'] });
    expect(params.getAll('tag')).toEqual(['scifi', 'noir']);
  });
});

describe('activeFilterCount', () => {
  it('counts only set scalar fields', () => {
    expect(activeFilterCount({ a: 'x', b: undefined, c: 0 })).toBe(2);
  });

  it('treats empty arrays as inactive but non-empty as a single bucket', () => {
    expect(activeFilterCount({ tag: [] })).toBe(0);
    expect(activeFilterCount({ tag: ['scifi'] })).toBe(1);
    expect(activeFilterCount({ tag: ['scifi', 'noir'] })).toBe(1);
  });

  it('ignores empty strings', () => {
    expect(activeFilterCount({ director: '', studio: 'Warner' })).toBe(1);
  });
});
