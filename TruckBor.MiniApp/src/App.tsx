import { useEffect, useState } from 'react';
import { AnimatePresence } from 'framer-motion';
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

  /* ── apply Telegram theme ── */
  useEffect(() => {
    applyFromTelegram(colorScheme);
  }, [colorScheme, applyFromTelegram]);

  /* ── Telegram WebApp is already set up by useTelegram hook ── */
  useEffect(() => {
    if (!webApp) return;
    // Additional setup if needed
  }, [webApp]);

  /* ── auth ── */
  useEffect(() => {
    if (!initData) { setBooting(false); return; }
    authApi.telegramAuth(initData)
      .then(({ token, user }) => { setToken(token); setUser(user); })
      .catch(() => {})
      .finally(() => setBooting(false));
  }, [initData]); // eslint-disable-line react-hooks/exhaustive-deps

  if (booting) return <Splash />;

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

function Splash() {
  return (
    <div className="flex items-center justify-center min-h-screen" style={{ background: 'var(--bg-base)' }}>
      <div className="flex flex-col items-center gap-4">
        <div className="text-5xl">🚛</div>
        <div className="text-lg font-semibold" style={{ color: 'var(--text-secondary)' }}>TruckBor</div>
        <div className="skeleton w-32 h-1 mt-2" />
      </div>
    </div>
  );
}
