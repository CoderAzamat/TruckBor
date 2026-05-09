import { motion } from 'framer-motion';
import { useQuery } from '@tanstack/react-query';
import { marketApi } from '../services/api';
import { useLocale } from '../store/locale';
import NavHeader from '../components/NavHeader';
import Skeleton from '../components/Skeleton';
import { maskCardNumber } from '../utils';

export default function MarketPage() {
  const { t } = useLocale();

  const { data: tariffs = [], isLoading: tariffsLoading } = useQuery({
    queryKey: ['tariffs'],
    queryFn: marketApi.tariffs,
    staleTime: 5 * 60_000,
  });

  const { data: cards = [], isLoading: cardsLoading } = useQuery({
    queryKey: ['cards'],
    queryFn: marketApi.cards,
    staleTime: 5 * 60_000,
  });

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      style={{ minHeight: '100vh' }}
    >
      <NavHeader title={t('nav_market')} />

      <div style={{ padding: '16px 16px' }}>
        {/* Tariffs */}
        <div className="section-label">{t('tariffs')}</div>

        {tariffsLoading ? (
          <>
            <Skeleton height={100} className="mb-3" />
            <Skeleton height={100} className="mb-3" />
          </>
        ) : (
          tariffs.map((tariff) => (
            <div key={tariff.id} className="card" style={{ padding: 16, marginBottom: 12 }}>
              <div className="flex items-center justify-between mb-2">
                <span style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-primary)' }}>
                  {tariff.name}
                </span>
                <span style={{
                  fontSize: 18, fontWeight: 800,
                  background: 'linear-gradient(135deg, var(--accent-gradient-from), var(--accent-gradient-to))',
                  WebkitBackgroundClip: 'text',
                  WebkitTextFillColor: 'transparent',
                }}>
                  {tariff.price.toLocaleString()} {t('currency')}
                </span>
              </div>
              <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 12 }}>
                {tariff.description}
              </div>
              <div className="flex flex-wrap gap-2 mb-12">
                {tariff.features.map((f, i) => (
                  <span key={i} className="pill pill-success">✓ {f}</span>
                ))}
              </div>
              <button className="btn-primary w-full" style={{ width: '100%' }}>
                {t('buy_tariff')}
              </button>
            </div>
          ))
        )}

        {/* Payment cards */}
        <div className="section-label" style={{ marginTop: 20 }}>{t('payment_cards')}</div>

        {cardsLoading ? (
          <Skeleton height={80} />
        ) : (
          cards.map((card) => (
            <div
              key={card.id}
              className="card"
              style={{
                padding: '14px 16px',
                marginBottom: 10,
                display: 'flex',
                alignItems: 'center',
                gap: 12,
              }}
            >
              <div style={{ fontSize: 28 }}>💳</div>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>
                  {card.bankName}
                </div>
                <div style={{ fontSize: 13, color: 'var(--text-secondary)', marginTop: 2 }}>
                  {maskCardNumber(card.cardNumber)}
                </div>
              </div>
              {card.isActive && (
                <span className="pill pill-success">{t('active')}</span>
              )}
            </div>
          ))
        )}
      </div>
    </motion.div>
  );
}
