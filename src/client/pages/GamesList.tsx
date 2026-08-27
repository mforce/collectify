import { Link } from 'react-router-dom';
import CollectionList from '../components/CollectionList';
import { PlatformIcon } from '../components/FormatIcons';
import { digitalStoresLabel, gamePlatformLabel, type Game } from '../services/types';

export default function GamesList() {
  return (
    <div>
      <div className="mb-2 flex justify-end">
        <Link
          to="/import/steam"
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand underline transition-colors hover:text-brand-hover"
        >
          <img
            src="/brand/steam-logo.svg"
            alt=""
            className="inline h-3.5 w-3.5"
            aria-hidden
          />
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
          const meta = platform ?? '';
          const showIcon = Boolean(g.platform && g.platform !== 'Other');
          return {
            primary: g.title,
            // Only emit a secondary row when there's metadata to show; an empty
            // React fragment is truthy and would render a blank row in every
            // card variant (regression guard: previous code returned the falsy
            // empty string '' for unclassified games).
            secondary: meta
              ? (
                <>
                  {showIcon && (
                    <PlatformIcon platform={g.platform} className="mr-1 inline h-3.5 w-3.5 align-[-2px] text-text-secondary" />
                  )}
                  {meta}
                </>
              )
              : undefined,
            tertiary: g.digitalStores
              ? `Digital · ${digitalStoresLabel(g.digitalStores)}`
              : 'Physical',
          };
        }}
      />
    </div>
  );
}
