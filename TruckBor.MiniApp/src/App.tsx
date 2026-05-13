import { useEffect, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { useTelegram } from './hooks/useTelegram';
import { useAuth } from './store/auth';
import { useTheme } from './store/theme';
import { authApi } from './services/api';
import TabBar from './components/TabBar';
import MapPage from './pages/MapPage';
import AdsPage from './pages/AdsPage';
import MyAdsPage from './pages/MyAdsPage';
import MarketPage from './pages/MarketPage';
import ProfilePage from './pages/ProfilePage';

export type Tab = 'map' | 'ads' | 'myads' | 'market' | 'profile';

export default function App() {
  const [tab, setTab] = useState<Tab>('ads');
  const { webApp, initData, colorScheme } = useTelegram();
  const { user, setUser, setToken } = useAuth();
  const { applyFromTelegram } = useTheme();
  const [booting, setBooting] = useState(true);

  /* ── Apply Telegram theme ── */
  useEffect(() => {
    applyFromTelegram(colorScheme);
  }, [colorScheme, applyFromTelegram]);

  useEffect(() => {
    if (!webApp) return;
    webApp.expand?.();
    webApp.enableClosingConfirmation?.();
  }, [webApp]);

  /* ── Auth ── */
  useEffect(() => {
    if (!initData) { setBooting(false); return; }
    authApi.telegramAuth(initData)
      .then(({ token, user }) => { setToken(token); setUser(user); })
      .catch(() => {})
      .finally(() => setBooting(false));
  }, [initData]); // eslint-disable-line react-hooks/exhaustive-deps

  if (booting) return <SplashScreen />;

  return (
    <div className="page" style={{ paddingBottom: 'calc(env(safe-area-inset-bottom,0) + 84px)' }}>
      <AnimatePresence mode="wait">
        {tab === 'map'     && <MapPage     key="map" />}
        {tab === 'ads'     && <AdsPage     key="ads" />}
        {tab === 'myads'   && <MyAdsPage   key="myads" />}
        {tab === 'market'  && <MarketPage  key="market" />}
        {tab === 'profile' && <ProfilePage key="profile" user={user} />}
      </AnimatePresence>
      <TabBar active={tab} onChange={setTab} />
    </div>
  );
}

function SplashScreen() {
  return (
    <div
      style={{
        position: 'fixed', inset: 0, zIndex: 100,
        background: 'var(--bg-base)',
        display: 'flex', flexDirection: 'column',
        alignItems: 'center', justifyContent: 'center', gap: 20,
      }}
    >
      {/* Glow circle */}
      <motion.div
        style={{
          position: 'absolute',
          width: 300, height: 300,
          borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(55,138,221,0.18) 0%, transparent 70%)',
        }}
        animate={{ scale: [1, 1.15, 1], opacity: [0.6, 1, 0.6] }}
        transition={{ duration: 2.5, repeat: Infinity, ease: 'easeInOut' }}
      />

      {/* Truck icon */}
      <motion.div
        initial={{ scale: 0.5, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', stiffness: 300, damping: 20, delay: 0.1 }}
        style={{ fontSize: 72, lineHeight: 1, position: 'relative' }}
      >
        🚛
      </motion.div>

      {/* Title */}
      <motion.div
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.35, duration: 0.4 }}
        style={{ textAlign: 'center' }}
      >
        <div style={{
          fontSize: 28, fontWeight: 800, letterSpacing: -0.5,
          background: 'linear-gradient(135deg, #378ADD, #4facee)',
          WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
        }}>
          TruckBor
        </div>
        <div style={{ fontSize: 13, color: 'var(--text-tertiary)', marginTop: 4 }}>
          Yuk tashish platformasi
        </div>
      </motion.div>

      {/* Progress bar */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 0.6 }}
        style={{
          width: 120, height: 3,
          background: 'var(--border-subtle)',
          borderRadius: 999, overflow: 'hidden',
        }}
      >
        <motion.div
          style={{
            height: '100%',
            background: 'linear-gradient(90deg, #378ADD, #4facee)',
            borderRadius: 999,
          }}
          initial={{ width: '0%' }}
          animate={{ width: '100%' }}
          transition={{ duration: 1.2, ease: 'easeInOut', delay: 0.7 }}
        />
      </motion.div>
    </div>
  );
}
