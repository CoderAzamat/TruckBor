import { motion } from 'framer-motion';
import type { Tab } from '../App';
import { useLocale } from '../store/locale';

/* ── Tabler-style SVG icon paths ─────────────────────────── */
const Icons: Record<Tab, JSX.Element> = {
  map: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21"/>
      <line x1="9" y1="3" x2="9" y2="18"/><line x1="15" y1="6" x2="15" y2="21"/>
    </svg>
  ),
  ads: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      <path d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2"/>
      <rect x="9" y="3" width="6" height="4" rx="2"/>
      <line x1="9" y1="12" x2="15" y2="12"/><line x1="9" y1="16" x2="13" y2="16"/>
    </svg>
  ),
  myads: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      <path d="M19 21l-7-5-7 5V5a2 2 0 012-2h10a2 2 0 012 2z"/>
    </svg>
  ),
  market: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="7" width="20" height="14" rx="2"/>
      <path d="M16 7V5a2 2 0 00-2-2h-4a2 2 0 00-2 2v2"/>
      <line x1="12" y1="12" x2="12" y2="16"/><line x1="10" y1="14" x2="14" y2="14"/>
    </svg>
  ),
  profile: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
      <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2"/>
      <circle cx="12" cy="7" r="4"/>
    </svg>
  ),
};

const TABS: { id: Tab; labelKey: string }[] = [
  { id: 'map',     labelKey: 'nav_map'     },
  { id: 'ads',     labelKey: 'nav_ads'     },
  { id: 'myads',   labelKey: 'nav_myads'   },
  { id: 'market',  labelKey: 'nav_market'  },
  { id: 'profile', labelKey: 'nav_profile' },
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
            <motion.button
              key={tab.id}
              onClick={() => onChange(tab.id)}
              whileTap={{ scale: 0.88 }}
              transition={{ type: 'spring', stiffness: 500, damping: 30 }}
              style={{
                position: 'relative',
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 3,
                flex: 1,
                padding: '6px 2px',
                border: 'none',
                background: 'transparent',
                cursor: 'pointer',
                color: isActive ? 'var(--accent-primary)' : 'var(--text-tertiary)',
                transition: 'color 0.2s ease',
              }}
            >
              {/* Active indicator */}
              {isActive && (
                <motion.div
                  layoutId="tab-indicator"
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: '15%',
                    right: '15%',
                    height: 2,
                    borderRadius: '0 0 4px 4px',
                    background: 'var(--accent-primary)',
                    boxShadow: '0 0 8px var(--accent-glow)',
                  }}
                  transition={{ type: 'spring', stiffness: 500, damping: 35 }}
                />
              )}

              {/* Active background pill */}
              {isActive && (
                <motion.div
                  layoutId="tab-bg"
                  style={{
                    position: 'absolute',
                    inset: 2,
                    borderRadius: 10,
                    background: 'var(--bg-overlay-active)',
                  }}
                  transition={{ type: 'spring', stiffness: 500, damping: 35 }}
                />
              )}

              <span style={{ position: 'relative', zIndex: 1, display: 'flex' }}>
                {Icons[tab.id]}
              </span>
              <span
                style={{
                  position: 'relative',
                  zIndex: 1,
                  fontSize: 10,
                  fontWeight: isActive ? 600 : 400,
                  letterSpacing: 0.2,
                  lineHeight: 1,
                }}
              >
                {t(tab.labelKey)}
              </span>
            </motion.button>
          );
        })}
      </div>
    </div>
  );
}
