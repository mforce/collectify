import CollectionList from '../components/CollectionList';
import { MovieFormatIcon } from '../components/FormatIcons';
import { MOVIE_FORMAT_FLAGS, type Movie } from '../services/types';

export default function MoviesList() {
  return (
    <CollectionList
      type="movies"
      title="Movies"
      newPath="/movies/new"
      category="movies"
      renderItem={(m: Movie) => {
        const fmts = MOVIE_FORMAT_FLAGS.filter((f) => ((m.formats ?? 0) & f.value) !== 0);
        return {
          primary: m.title,
          secondary: m.director,
          tertiary:
            fmts.length > 0 ? (
              <span className="inline-flex items-center gap-1">
                {fmts.map((f) => (
                  <span key={f.key} className="inline-flex items-center gap-0.5" title={f.label}>
                    <MovieFormatIcon format={f.key} className="h-3.5 w-3.5" />
                    <span>{f.label}</span>
                  </span>
                ))}
              </span>
            ) : undefined,
        };
      }}
    />
  );
}
