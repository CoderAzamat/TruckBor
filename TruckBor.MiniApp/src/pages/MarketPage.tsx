import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useQuery } from '@tanstack/react-query';
import { marketApi } from '../services/api';
import { useLocale } from '../store/locale';
import { useAuth } from '../store/auth';
import NavHeader from '../components/NavHeader';
import Skeleton from '../components/Skeleton';
import { maskCardNumber } from '../utils';

type MarketTab = 'tariffs' | 'virtual' | 'premium' | 'topup';

const MARKET_TABS: { id: MarketTab; emoji: string; key: string }[] = [
  { id: 'tariffs',  emoji: '⭐', key: 'tariffs'          },
  { id: 'virtual',  emoji: '📱', key: 'virtual_numbers'  },
  { id: 'premium',  emoji: '💎', key: 'tg_premium'       },
  { id: 'topup',    emoji: '💳', key: 'balance_topup'    },
];

const COUNTRIES = [
  { code: 'uz', flag: '🇺🇿', name: "O'zbekiston", price: '2 000' },
  { code: 'ru', flag: '🇷🇺', name: 'Rossiya',      price: '1 500' },
  { code: 'kz', flag: '🇰🇿', name: 'Qozogʻiston',  price: '1 800' },
  { code: 'uk', flag: '🇬🇧', name: 'Buyuk Britaniya', price: '3 000' },
  { code: 'in', flag: '🇮🇳', name: 'Hindiston',    price: '800'   },
  { code: 'pl', flag: '🇵🇱', name: 'Polsha',       price: '2 500' },
];

const PREMIUM_PLANS = [
  { months: 1,  label: '1 oy',    price: '99 000',  icon: '⭐' },
  { months: 3,  label: '3 oy',    price: '249 000', icon: '⭐⭐' },
  { months: 6,  label: '6 oy',    price: '449 000', icon: '⭐⭐⭐', badge: 'Ommabop' },
  { months: 12, label: '12 oy',   price: '799 000', icon: '👑', badge: 'Tejamkor' },
];

const pageVariants = {
  initial: { opacity: 0, y: 10 },
  animate: { opacity: 1, y: 0 },
  exit:    { opacity: 0 },
};

export default function MarketPage() {
  const { t } = useLocale();
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState<MarketTab>('tariffs');
  const [selectedCountry, setSelectedCountry] = useState<string | null>(null);
  const [premiumUsername, setPremiumUsername] = useState('');
  const [selectedMonths, setSelectedMonths] = useState<number | null>(null);

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
    <motion.div {...pageVariants} transition={{ duration: 0.2 }} style={{ minHeight: '100vh' }}>
      <NavHeader title={t('nav_market')} />

      {/* Market tabs */}
      <div style={{
        display: 'flex', gap: 8, padding: '12px 16px 0',
        overflowX: 'auto', scrollbarWidth: 'none',
      }}>
        {MARKET_TABS.map((tab) => {
          const isActive = tab.id === activeTab;
          return (
            <motion.button
              key={tab.id}
              whileTap={{ scale: 0.93 }}
              onClick={() => setActiveTab(tab.id)}
              style={{
                display: 'flex', alignItems: 'center', gap: 6,
                padding: '8px 14px', borderRadius: 20, border: 'none',
                background: isActive
                  ? 'linear-gradient(135deg, #378ADD, #1d5fad)'
                  : 'var(--bg-overlay)',
                color: isActive ? '#fff' : 'var(--text-secondary)',
                fontSize: 13, fontWeight: isActive ? 600 : 400,
                whiteSpace: 'nowrap', cursor: 'pointer',
                boxShadow: isActive ? '0 4px 16px rgba(55,138,221,0.35)' : 'none',
                borderColor: isActive ? 'transparent' : 'var(--border-subtle)',
                borderWidth: 0.5, borderStyle: 'solid',
                transition: 'all 0.2s',
                flexShrink: 0,
              }}
            >
              <span>{tab.emoji}</span>
              <span>{t(tab.key)}</span>
            </motion.button>
          );
        })}
      </div>

      <div style={{ padding: '16px' }}>
        <AnimatePresence mode="wait">

          {/* ── TARIFLAR ─────────────────────────────────── */}
          {activeTab === 'tariffs' && (
            <motion.div key="tariffs" {...pageVariants} transition={{ duration: 0.15 }}>
              {/* Balance badge */}
              {user && user.balance > 0 && (
                <div style={{
                  display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                  padding: '10px 14px', marginBottom: 12,
                  background: 'var(--bg-overlay)', border: '0.5px solid var(--border-default)',
                  borderRadius: 'var(--r-md)',
                }}>
                  <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>💰 Balans</span>
                  <span style={{ fontSize: 15, fontWeight: 700, color: 'var(--accent-primary)' }}>
                    {user.balance.toLocaleString()} {t('currency')}
                  </span>
                </div>
              )}

              {tariffsLoading ? (
                <>{[1,2,3].map(i => <Skeleton key={i} height={130} className="mb-3" />)}</>
              ) : (
                tariffs.map((tariff, i) => (
                  <motion.div
                    key={tariff.id}
                    initial={{ opacity: 0, y: 12 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: i * 0.05 }}
                    style={{
                      background: tariff.isRecommended
                        ? 'linear-gradient(135deg, rgba(55,138,221,0.12), rgba(29,95,173,0.08))'
                        : 'var(--bg-overlay)',
                      border: `0.5px solid ${tariff.isRecommended ? 'rgba(55,138,221,0.4)' : 'var(--border-default)'}`,
                      borderRadius: 'var(--r-lg)', padding: 16, marginBottom: 12,
                      position: 'relative', overflow: 'hidden',
                    }}
                  >
                    {tariff.isRecommended && (
                      <div style={{
                        position: 'absolute', top: 0, right: 0,
                        background: 'linear-gradient(135deg, #378ADD, #1d5fad)',
                        color: '#fff', fontSize: 10, fontWeight: 700,
                        padding: '4px 10px',
                        borderBottomLeftRadius: 10,
                      }}>
                        ✦ TOP
                      </div>
                    )}

                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 8 }}>
                      <div>
                        <div style={{ fontSize: 17, fontWeight: 700, color: 'var(--text-primary)' }}>
                          {tariff.name}
                        </div>
                        {tariff.description && (
                          <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginTop: 2 }}>
                            {tariff.description}
                          </div>
                        )}
                      </div>
                      <div style={{ textAlign: 'right' }}>
                        <div style={{
                          fontSize: 20, fontWeight: 800,
                          background: 'linear-gradient(135deg, #378ADD, #4facee)',
                          WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
                        }}>
                          {tariff.price.toLocaleString()}
                        </div>
                        <div style={{ fontSize: 11, color: 'var(--text-tertiary)' }}>
                          {t('currency')}/{tariff.durationDays} kun
                        </div>
                      </div>
                    </div>

                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginBottom: 12 }}>
                      {tariff.features.map((f: string, fi: number) => (
                        <span key={fi} style={{
                          display: 'inline-flex', alignItems: 'center', gap: 4,
                          padding: '3px 9px', borderRadius: 999,
                          background: 'rgba(55,138,221,0.1)', color: '#7ec8f8',
                          fontSize: 11, fontWeight: 500,
                        }}>
                          ✓ {f}
                        </span>
                      ))}
                    </div>

                    <button
                      className="btn-primary"
                      style={{ width: '100%', fontSize: 14, padding: '11px 16px' }}
                    >
                      {t('buy_tariff')} — {tariff.price.toLocaleString()} {t('currency')}
                    </button>
                  </motion.div>
                ))
              )}
            </motion.div>
          )}

          {/* ── VIRTUAL RAQAMLAR ─────────────────────────── */}
          {activeTab === 'virtual' && (
            <motion.div key="virtual" {...pageVariants} transition={{ duration: 0.15 }}>
              <div style={{ marginBottom: 12, fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                📱 SMS-Activate orqali istalgan mamlakatdan raqam xarid qiling. Raqam 20 daqiqa faol bo'ladi.
              </div>

              {COUNTRIES.map((country, i) => {
                const isSelected = selectedCountry === country.code;
                return (
                  <motion.button
                    key={country.code}
                    initial={{ opacity: 0, x: -8 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: i * 0.04 }}
                    whileTap={{ scale: 0.97 }}
                    onClick={() => setSelectedCountry(isSelected ? null : country.code)}
                    style={{
                      width: '100%', display: 'flex', alignItems: 'center', gap: 12,
                      padding: '12px 14px', marginBottom: 8, border: 'none', cursor: 'pointer',
                      background: isSelected ? 'rgba(55,138,221,0.12)' : 'var(--bg-overlay)',
                      borderRadius: 'var(--r-md)',
                      outline: isSelected ? '1.5px solid rgba(55,138,221,0.5)' : '0.5px solid var(--border-subtle)',
                      transition: 'all 0.2s',
                    }}
                  >
                    <span style={{ fontSize: 28 }}>{country.flag}</span>
                    <div style={{ flex: 1, textAlign: 'left' }}>
                      <div style={{ fontSize: 14, fontWeight: 600, color: 'var(--text-primary)' }}>
                        {country.name}
                      </div>
                      <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginTop: 2 }}>
                        Telegram SMS
                      </div>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <div style={{ fontSize: 14, fontWeight: 700, color: 'var(--accent-primary)' }}>
                        {country.price} {t('currency')}
                      </div>
                    </div>
                    {isSelected && (
                      <span style={{ fontSize: 18 }}>✓</span>
                    )}
                  </motion.button>
                );
              })}

              {selectedCountry && (
                <motion.div
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  style={{ marginTop: 8 }}
                >
                  <button
                    className="btn-primary"
                    style={{ width: '100%' }}
                  >
                    📱 Raqam xarid qilish
                  </button>
                </motion.div>
              )}

              <div style={{
                marginTop: 16, padding: 12, borderRadius: 'var(--r-md)',
                background: 'rgba(245,158,11,0.08)', border: '0.5px solid rgba(245,158,11,0.2)',
              }}>
                <div style={{ fontSize: 12, color: '#fcd34d', lineHeight: 1.6 }}>
                  ⚠️ Virtual raqam faqat bir marta SMS qabul qilish uchun. Balansdan to'lanadi.
                </div>
              </div>
            </motion.div>
          )}

          {/* ── TG PREMIUM ───────────────────────────────── */}
          {activeTab === 'premium' && (
            <motion.div key="premium" {...pageVariants} transition={{ duration: 0.15 }}>
              {/* Hero */}
              <div style={{
                textAlign: 'center', padding: '20px 16px',
                background: 'linear-gradient(135deg, rgba(168,85,247,0.12), rgba(55,138,221,0.08))',
                border: '0.5px solid rgba(168,85,247,0.3)',
                borderRadius: 'var(--r-xl)', marginBottom: 16,
              }}>
                <div style={{ fontSize: 48, marginBottom: 8 }}>💎</div>
                <div style={{ fontSize: 18, fontWeight: 700, color: 'var(--text-primary)', marginBottom: 4 }}>
                  Telegram Premium
                </div>
                <div style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                  Istalgan akkauntga Premium sovg'a qiling. Admin 1-24 soat ichida aktivlashtiradi.
                </div>
              </div>

              {/* Username input */}
              <div style={{ marginBottom: 12 }}>
                <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginBottom: 6 }}>
                  📌 Telegram username
                </div>
                <input
                  className="input"
                  placeholder="@username"
                  value={premiumUsername}
                  onChange={(e) => setPremiumUsername(e.target.value)}
                />
              </div>

              {/* Plan cards */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, marginBottom: 16 }}>
                {PREMIUM_PLANS.map((plan, i) => {
                  const isSelected = selectedMonths === plan.months;
                  return (
                    <motion.button
                      key={plan.months}
                      initial={{ opacity: 0, scale: 0.95 }}
                      animate={{ opacity: 1, scale: 1 }}
                      transition={{ delay: i * 0.06 }}
                      whileTap={{ scale: 0.95 }}
                      onClick={() => setSelectedMonths(isSelected ? null : plan.months)}
                      style={{
                        border: 'none', cursor: 'pointer', padding: '14px 10px',
                        borderRadius: 'var(--r-lg)', textAlign: 'center',
                        background: isSelected
                          ? 'linear-gradient(135deg, rgba(168,85,247,0.2), rgba(55,138,221,0.15))'
                          : 'var(--bg-overlay)',
                        outline: isSelected ? '1.5px solid rgba(168,85,247,0.5)' : '0.5px solid var(--border-subtle)',
                        position: 'relative', transition: 'all 0.2s',
                      }}
                    >
                      {plan.badge && (
                        <div style={{
                          position: 'absolute', top: -8, left: '50%', transform: 'translateX(-50%)',
                          background: 'linear-gradient(135deg, #a855f7, #7c3aed)',
                          color: '#fff', fontSize: 9, fontWeight: 700, padding: '2px 8px',
                          borderRadius: 999, whiteSpace: 'nowrap',
                        }}>
                          {plan.badge}
                        </div>
                      )}
                      <div style={{ fontSize: 22, marginBottom: 4 }}>{plan.icon}</div>
                      <div style={{ fontSize: 13, fontWeight: 600, color: 'var(--text-primary)' }}>
                        {plan.label}
                      </div>
                      <div style={{ fontSize: 14, fontWeight: 700, color: '#c084fc', marginTop: 4 }}>
                        {plan.price}
                      </div>
                      <div style={{ fontSize: 10, color: 'var(--text-tertiary)' }}>{t('currency')}</div>
                    </motion.button>
                  );
                })}
              </div>

              <button
                className="btn-primary"
                style={{
                  width: '100%',
                  background: 'linear-gradient(135deg, #a855f7, #7c3aed)',
                  boxShadow: '0 4px 20px rgba(168,85,247,0.4)',
                }}
                disabled={!premiumUsername || !selectedMonths}
              >
                💎 Premium buyurtma berish
              </button>

              <div style={{
                marginTop: 12, padding: 12, borderRadius: 'var(--r-md)',
                background: 'rgba(55,138,221,0.06)', border: '0.5px solid var(--border-subtle)',
              }}>
                <div style={{ fontSize: 12, color: 'var(--text-tertiary)', lineHeight: 1.6 }}>
                  ℹ️ To'lov balansdan yechiladi. Admin tasdiqlangandan so'ng Premium faollashadi.
                </div>
              </div>
            </motion.div>
          )}

          {/* ── BALANS TO'LDIRISH ────────────────────────── */}
          {activeTab === 'topup' && (
            <motion.div key="topup" {...pageVariants} transition={{ duration: 0.15 }}>
              {/* Current balance */}
              <div style={{
                textAlign: 'center', padding: '20px 16px',
                background: 'linear-gradient(135deg, rgba(55,138,221,0.1), rgba(29,95,173,0.06))',
                border: '0.5px solid var(--border-default)',
                borderRadius: 'var(--r-xl)', marginBottom: 16,
              }}>
                <div style={{ fontSize: 13, color: 'var(--text-tertiary)', marginBottom: 4 }}>
                  Joriy balans
                </div>
                <div style={{
                  fontSize: 32, fontWeight: 800,
                  background: 'linear-gradient(135deg, #378ADD, #4facee)',
                  WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
                }}>
                  {(user?.balance ?? 0).toLocaleString()}
                </div>
                <div style={{ fontSize: 14, color: 'var(--text-secondary)', marginTop: 2 }}>
                  {t('currency')}
                </div>
              </div>

              {/* Quick amounts */}
              <div style={{ marginBottom: 16 }}>
                <div className="section-label" style={{ marginBottom: 10 }}>Miqdorni tanlang</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 8 }}>
                  {[50_000, 100_000, 200_000, 300_000, 500_000, 1_000_000].map((amount) => (
                    <motion.button
                      key={amount}
                      whileTap={{ scale: 0.92 }}
                      style={{
                        border: '0.5px solid var(--border-default)',
                        background: 'var(--bg-overlay)', borderRadius: 'var(--r-md)',
                        padding: '10px 4px', cursor: 'pointer', textAlign: 'center',
                        color: 'var(--text-primary)', fontSize: 13, fontWeight: 600,
                        transition: 'all 0.15s',
                      }}
                    >
                      {(amount / 1000).toFixed(0)}K
                    </motion.button>
                  ))}
                </div>
              </div>

              {/* Payment methods */}
              <div className="section-label" style={{ marginBottom: 10 }}>To'lov usuli</div>

              {cardsLoading ? (
                <Skeleton height={80} />
              ) : (
                cards.map((card) => (
                  <div
                    key={card.id}
                    style={{
                      display: 'flex', alignItems: 'center', gap: 12,
                      padding: '12px 14px', marginBottom: 8,
                      background: 'var(--bg-overlay)',
                      border: '0.5px solid var(--border-subtle)',
                      borderRadius: 'var(--r-md)',
                    }}
                  >
                    <div style={{ fontSize: 28 }}>💳</div>
                    <div style={{ flex: 1 }}>
                      <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: 14 }}>
                        {card.bankName}
                      </div>
                      <div style={{ fontSize: 12, color: 'var(--text-tertiary)', marginTop: 2 }}>
                        {maskCardNumber(card.cardNumber)}
                      </div>
                    </div>
                    <button style={{
                      background: 'rgba(55,138,221,0.12)', color: 'var(--accent-primary)',
                      border: '0.5px solid rgba(55,138,221,0.3)',
                      borderRadius: 8, padding: '6px 12px', fontSize: 12, fontWeight: 600, cursor: 'pointer',
                    }}>
                      Nusxa
                    </button>
                  </div>
                ))
              )}

              <button className="btn-primary" style={{ width: '100%', marginTop: 8 }}>
                📸 Chek yuborish
              </button>

              <div style={{
                marginTop: 12, padding: 12, borderRadius: 'var(--r-md)',
                background: 'rgba(55,138,221,0.06)', border: '0.5px solid var(--border-subtle)',
              }}>
                <div style={{ fontSize: 12, color: 'var(--text-tertiary)', lineHeight: 1.6 }}>
                  💡 To'lovni amalga oshiring va chek rasmini yuboring. Admin 5-30 daqiqa ichida tasdiqlayd.
                </div>
              </div>
            </motion.div>
          )}

        </AnimatePresence>
      </div>
    </motion.div>
  );
}
