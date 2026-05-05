import CollectionList from '../components/CollectionList';
import { MUSIC_FORMATS, type Album } from '../api/types';

export default function MusicList() {
  return (
    <CollectionList
      type="music"
      title="Music"
      newPath="/music/new"
      renderItem={(a: Album) => ({
        primary: a.title,
        secondary: [a.artistName, a.year].filter(Boolean).join(' · '),
        tertiary: MUSIC_FORMATS.find((f) => f.value === a.format)?.label,
      })}
    />
  );
}
