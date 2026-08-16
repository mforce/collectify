import CollectionList from '../components/CollectionList';
import { musicFormatLabel, type Album } from '../services/types';

export default function MusicList() {
  return (
    <CollectionList
      type="music"
      title="Music"
      newPath="/music/new"
      category="music"
      renderItem={(a: Album) => ({
        primary: a.title,
        secondary: [a.artistName, a.year].filter(Boolean).join(' · '),
        tertiary: musicFormatLabel(a.format),
      })}
    />
  );
}
