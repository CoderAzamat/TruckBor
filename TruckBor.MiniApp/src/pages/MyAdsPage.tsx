import { useState } from 'react';
import { motion } from 'framer-motion';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { postsApi } from '../services/api';
import { useLocale } from '../store/locale';
import NavHeader from '../components/NavHeader';
import AdCard from '../components/AdCard';
import { AdCardSkeleton } from '../components/Skeleton';
import EmptyState from '../components/EmptyState';
import CreateAdPage from './CreateAdPage';

export default function MyAdsPage() {
  const { t } = useLocale();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);

  const { data: posts = [], isLoading } = useQuery({
    queryKey: ['my-posts'],
    queryFn: postsApi.mine,
  });

  const handleDelete = (id: number) => {
    qc.setQueryData<typeof posts>(['my-posts'], (old) => old?.filter((p) => p.id !== id));
  };

  if (showCreate) {
    return (
      <CreateAdPage
        onBack={() => setShowCreate(false)}
        onSuccess={() => { setShowCreate(false); qc.invalidateQueries({ queryKey: ['my-posts'] }); }}
      />
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      style={{ minHeight: '100vh' }}
    >
      <NavHeader
        title={t('nav_myads')}
        right={
          <button
            onClick={() => setShowCreate(true)}
            style={{
              background: 'var(--accent-primary)',
              border: 'none',
              borderRadius: 8,
              color: '#fff',
              fontSize: 20,
              width: 32,
              height: 32,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            +
          </button>
        }
      />

      <div style={{ padding: '12px 16px' }}>
        {isLoading && (
          <>
            <AdCardSkeleton />
            <AdCardSkeleton />
          </>
        )}

        {!isLoading && posts.length === 0 && (
          <EmptyState
            icon="📌"
            title={t('no_myads')}
            subtitle={t('no_myads_sub')}
            action={{ label: t('create_ad'), onClick: () => setShowCreate(true) }}
          />
        )}

        {posts.map((post) => (
          <AdCard
            key={post.id}
            post={post}
            showDeleteBtn
            onDelete={handleDelete}
          />
        ))}
      </div>
    </motion.div>
  );
}
