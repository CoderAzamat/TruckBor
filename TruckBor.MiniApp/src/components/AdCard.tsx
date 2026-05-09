import { useState } from 'react';
import type { Post } from '../types';
import { useLocale } from '../store/locale';
import { formatMoney, timeAgo } from '../utils';
import { postsApi } from '../services/api';

interface Props {
  post: Post;
  onDelete?: (id: number) => void;
  showDeleteBtn?: boolean;
}

const TYPE_CLASS: Record<string, string> = {
  Cargo: 'pill-cargo',
  Transport: 'pill-transport',
  Dogruz: 'pill-dogruz',
};

const TYPE_ICON: Record<string, string> = {
  Cargo: '📦',
  Transport: '🚛',
  Dogruz: '📫',
};

export default function AdCard({ post, onDelete, showDeleteBtn }: Props) {
  const { t, locale } = useLocale();
  const [phone, setPhone] = useState<string | null>(null);
  const [loadingPhone, setLoadingPhone] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const pillClass = TYPE_CLASS[post.postType] ?? 'pill-info';
  const icon = TYPE_ICON[post.postType] ?? '📋';

  const handleShowPhone = async () => {
    if (phone) return;
    setLoadingPhone(true);
    try {
      const { phone: p } = await postsApi.showPhone(post.id);
      setPhone(p);
    } catch {
      // ignore / show upgrade prompt
    } finally {
      setLoadingPhone(false);
    }
  };

  const handleDelete = async () => {
    if (!onDelete) return;
    setDeleting(true);
    try {
      await postsApi.delete(post.id);
      onDelete(post.id);
    } catch {
      setDeleting(false);
    }
  };

  return (
    <div className="card" style={{ padding: 16, marginBottom: 10 }}>
      {/* Header row */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <span className={`pill ${pillClass}`}>
            {icon} {t(`post_type_${post.postType.toLowerCase()}`)}
          </span>
          {post.isVerified && (
            <span className="pill pill-success">✓ {t('verified')}</span>
          )}
        </div>
        <span style={{ fontSize: 11, color: 'var(--text-quaternary)' }}>
          {timeAgo(post.createdAt, locale)}
        </span>
      </div>

      {/* Route */}
      <div className="flex items-center gap-2 mb-2">
        <span style={{ fontSize: 20 }}>📍</span>
        <div>
          <div style={{ fontWeight: 600, fontSize: 15, color: 'var(--text-primary)' }}>
            {post.fromCity}
          </div>
          <div style={{ fontSize: 12, color: 'var(--text-tertiary)' }}>↓</div>
          <div style={{ fontWeight: 600, fontSize: 15, color: 'var(--text-primary)' }}>
            {post.toCity}
          </div>
        </div>
      </div>

      {/* Details */}
      <div className="flex flex-wrap gap-2 mb-3">
        {post.cargoType && (
          <span className="pill pill-info">📦 {post.cargoType}</span>
        )}
        {post.weight && (
          <span className="pill pill-info">⚖️ {post.weight}</span>
        )}
        {post.vehicleType && (
          <span className="pill pill-info">🚛 {post.vehicleType}</span>
        )}
        {post.price && (
          <span className="pill pill-warning">💰 {formatMoney(post.price)}</span>
        )}
      </div>

      {post.description && (
        <p style={{ fontSize: 13, color: 'var(--text-secondary)', marginBottom: 12 }}>
          {post.description}
        </p>
      )}

      {/* Footer */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2" style={{ fontSize: 12, color: 'var(--text-quaternary)' }}>
          <span>👁 {post.viewCount}</span>
        </div>
        <div className="flex gap-2">
          {showDeleteBtn && (
            <button
              className="btn-danger"
              style={{ padding: '8px 14px', fontSize: 13, borderRadius: 10 }}
              onClick={handleDelete}
              disabled={deleting}
            >
              {deleting ? '…' : t('delete')}
            </button>
          )}
          {phone ? (
            <a
              href={`tel:${phone}`}
              className="btn-primary"
              style={{ padding: '8px 14px', fontSize: 13, borderRadius: 10, textDecoration: 'none' }}
            >
              📞 {phone}
            </a>
          ) : (
            <button
              className="btn-primary"
              style={{ padding: '8px 14px', fontSize: 13, borderRadius: 10 }}
              onClick={handleShowPhone}
              disabled={loadingPhone}
            >
              {loadingPhone ? '…' : `📞 ${t('show_phone')}`}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
