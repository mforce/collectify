import CollectionList from '../components/CollectionList';
import { MusicFormatIcon } from '../components/FormatIcons';
import { musicFormatLabel, type Album } from '../services/types';

export default function MusicList() {
  return (
    <CollectionList
      type="music"
      title="Music"
      newPath="/music/new"
      category="music"
      renderItem={(a: Album) => {
        const label = musicFormatLabel(a.format);
        return {
          primary: a.title,
          secondary: [a.artistName, a.year].filter(Boolean).join(' · '),
          tertiary:
            label ? (
              <span className="inline-flex items-center gap-1">
                <MusicFormatIcon format={a.format} className="h-3.5 w-3.5" />
                {label}
              </span>
            ) : undefined,
        };
      }}
    />
  );
}
