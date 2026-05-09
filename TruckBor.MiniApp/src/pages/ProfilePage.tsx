import { motion } from 'framer-motion';
import type { User } from '../types';
import { useLocale } from '../store/locale';
import { useTheme } from '../store/theme';
import { useAuth } from '../store/auth';
import NavHeader from '../components/NavHeader';
import SegmentedControl from '../components/SegmentedControl';

interface Props {
  user: User | null;
}

export default function ProfilePage({ user }: Props) {
  const { t, locale, setLocale } = useLocale();
  const { mode, setMode } = useTheme();
  const { logout } = useAuth();

  const localeSegments = [
    { value: 'uz' as const, label: "O'z" },
    { value: 'ru' as const, label: 'Рус' },
    { value: 'en' as const, label: 'Eng' },
  ];

  const themeSegments = [
    { value: 'auto' as const,  label: t('theme_auto') },
    { value: 'dark' as const,  label: t('theme_dark') },
    { value: 'light' as const, label: t('theme_light') },
  ];

  const roleIcon: Record<string, string> = {
    Driver: '🚛',
    CargoOwner: '📦',
    Logist: '📋',
    Admin: '⭐',
  };

  const rolePill: Record<string, string> = {
    Driver: 'pill-driver',
    CargoOwner: 'pill-cargo',
    Logist: 'pill-logist',
  };

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      style={{ minHeight: '100vh' }}
    >
      <NavHeader title={t('nav_profile')} />

      <div style={{ padding: '20px 16px' }}>
        {/* User card */}
        {user ? (
          <div className="glass" style={{ padding: 20, marginBottom: 20, textAlign: 'center' }}>
            <div style={{ fontSize: 56, marginBottom: 8 }}>
              {roleIcon[user.role] ?? '👤'}
            </div>
            <div style={{ fontSize: 20, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 4 }}>
              {user.firstName} {user.lastName}
            </div>
            {user.username && (
              <div style={{ fontSize: 14, color: 'var(--text-tertiary)', marginBottom: 8 }}>
                @{user.username}
              </div>
            )}
            <div className="flex items-center justify-center gap-2">
              <span className={`pill ${rolePill[user.role] ?? 'pill-info'}`}>
                {roleIcon[user.role]} {t(`role_${user.role.toLowerCase()}`)}
              </span>
              {user.isVip && (
                <span className="pill pill-warning">⭐ VIP</span>
              )}
            </div>
            {user.balance > 0 && (
              <div style={{ marginTop: 12, fontSize: 15, fontWeight: 600, color: 'var(--accent-primary)' }}>
                💰 {user.balance.toLocaleString()} {t('currency')}
              </div>
            )}
          </div>
        ) : (
          <div className="glass" style={{ padding: 20, marginBottom: 20, textAlign: 'center' }}>
            <div style={{ fontSize: 48, marginBottom: 8 }}>👤</div>
            <div style={{ color: 'var(--text-secondary)' }}>{t('not_logged_in')}</div>
          </div>
        )}

        {/* Language */}
        <div className="section-label">{t('language')}</div>
        <div style={{ marginBottom: 20 }}>
          <SegmentedControl
            segments={localeSegments}
            value={locale}
            onChange={setLocale}
          />
        </div>

        {/* Theme */}
        <div className="section-label">{t('theme')}</div>
        <div style={{ marginBottom: 20 }}>
          <SegmentedControl
            segments={themeSegments}
            value={mode}
            onChange={setMode}
          />
        </div>

        {/* Settings list */}
        <div className="card" style={{ marginBottom: 20 }}>
          <a
            href="https://t.me/truckbor"
            target="_blank"
            rel="noreferrer"
            className="list-item"
            style={{ color: 'var(--text-primary)', textDecoration: 'none' }}
          >
            <span style={{ fontSize: 20 }}>💬</span>
            <span style={{ flex: 1, fontSize: 15 }}>{t('support')}</span>
            <span style={{ color: 'var(--text-tertiary)', fontSize: 13 }}>›</span>
          </a>
          <a
            href="https://t.me/truckborchannel"
            target="_blank"
            rel="noreferrer"
            className="list-item"
            style={{ color: 'var(--text-primary)', textDecoration: 'none' }}
          >
            <span style={{ fontSize: 20 }}>📢</span>
            <span style={{ flex: 1, fontSize: 15 }}>{t('channel')}</span>
            <span style={{ color: 'var(--text-tertiary)', fontSize: 13 }}>›</span>
          </a>
        </div>

        {/* Logout */}
        {user && (
          <button
            className="btn-danger"
            style={{ width: '100%' }}
            onClick={logout}
          >
            {t('logout')}
          </button>
        )}

        <div style={{ marginTop: 20, textAlign: 'center', fontSize: 12, color: 'var(--text-quaternary)' }}>
          TruckBor v1.0.0
        </div>
      </div>
    </motion.div>
  );
}
