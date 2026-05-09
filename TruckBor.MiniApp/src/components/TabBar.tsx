import type { Tab } from '../App';
import { useLocale } from '../store/locale';

const TABS: { id: Tab; icon: string; labelKey: string }[] = [
  { id: 'map',     icon: '🗺️',  labelKey: 'nav_map' },
  { id: 'ads',     icon: '📋',  labelKey: 'nav_ads' },
  { id: 'myads',   icon: '📌',  labelKey: 'nav_myads' },
  { id: 'market',  icon: '💳',  labelKey: 'nav_market' },
  { id: 'profile', icon: '👤',  labelKey: 'nav_profile' },
];

interface Props {
  active: Tab;
  onChange: (t: Tab) => void;
}

export default function TabBar({ active, onChange }: Props) {
  const { t } = useLocale();

  return (
    <div className="tab-bar">
      <div className="flex">
        {TABS.map((tab) => {
          const isActive = tab.id === active;
          return (
            <button
              key={tab.id}
              onClick={() => onChange(tab.id)}
              className="flex flex-col items-center justify-center gap-0.5 flex-1 py-1 px-2 rounded-xl transition-all duration-150"
              style={{
                background: isActive ? 'var(--bg-overlay-active)' : 'transparent',
                color: isActive ? 'var(--accent-primary)' : 'var(--text-tertiary)',
                border: 'none',
                cursor: 'pointer',
              }}
            >
              <span style={{ fontSize: 20, lineHeight: 1 }}>{tab.icon}</span>
              <span style={{ fontSize: 10, fontWeight: 500 }}>{t(tab.labelKey)}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
