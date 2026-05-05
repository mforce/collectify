import CollectionList from '../components/CollectionList';
import type { Game } from '../api/types';

export default function GamesList() {
  return (
    <CollectionList
      type="games"
      title="Games"
      newPath="/games/new"
      renderItem={(g: Game) => ({
        primary: g.title,
        secondary: [g.platform, g.year].filter(Boolean).join(' · '),
        tertiary: g.isDigital ? `Digital${g.digitalStore ? ` · ${g.digitalStore}` : ''}` : 'Physical',
      })}
    />
  );
}
