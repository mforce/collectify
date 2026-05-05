import CollectionList from '../components/CollectionList';
import { MOVIE_FORMAT_FLAGS, type Movie } from '../api/types';

export default function MoviesList() {
  return (
    <CollectionList
      type="movies"
      title="Movies"
      newPath="/movies/new"
      renderItem={(m: Movie) => {
        const fmts = MOVIE_FORMAT_FLAGS.filter((f) => ((m.formats ?? 0) & f.value) !== 0).map((f) => f.label).join(', ');
        return {
          primary: m.title,
          secondary: [m.year, m.director].filter(Boolean).join(' · '),
          tertiary: fmts || undefined,
        };
      }}
    />
  );
}
