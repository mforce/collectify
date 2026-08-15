import { Link } from 'react-router-dom';
import CollectionList from '../components/CollectionList';
import { gamePlatformLabel, type Game } from '../services/types';

export default function GamesList() {
  return (
    <div>
      <div className="mb-2 flex justify-end">
        <Link
          to="/import/steam"
          className="text-xs font-semibold text-brand underline transition-colors hover:text-brand-hover"
        >
          Import from Steam ↗
        </Link>
      </div>
      <CollectionList
        type="games"
        title="Games"
        newPath="/games/new"
        category="games"
        renderItem={(g: Game) => {
          // Prefer the canonical label; fall back to the legacy free-text
          // when the row is still pending re-classification so the card
          // doesn't just say "Other" for everything mid-migration.
          const platform =
            g.platform && g.platform !== 'Other'
              ? gamePlatformLabel(g.platform)
              : g.platformLegacy ?? null;
          return {
            primary: g.title,
            secondary: [platform, g.year].filter(Boolean).join(' · '),
            tertiary: g.isDigital ? `Digital${g.digitalStore ? ` · ${g.digitalStore}` : ''}` : 'Physical',
          };
        }}
      />
    </div>
  );
}
