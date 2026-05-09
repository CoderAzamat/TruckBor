import { useState, useCallback } from 'react';
import { motion } from 'framer-motion';
import { useInfiniteQuery } from '@tanstack/react-query';
import { postsApi } from '../services/api';
import { useLocale } from '../store/locale';
import type { PostType } from '../types';
import NavHeader from '../components/NavHeader';
import AdCard from '../components/AdCard';
import SegmentedControl from '../components/SegmentedControl';
import { AdCardSkeleton } from '../components/Skeleton';
import EmptyState from '../components/EmptyState';
import CreateAdPage from './CreateAdPage';

const PAGE_SIZE = 10;

export default function AdsPage() {
  const { t } = useLocale();
  const [postType, setPostType] = useState<PostType | ''>('');
  const [fromCity, setFromCity] = useState('');
  const [toCity, setToCity] = useState('');
  const [showCreate, setShowCreate] = useState(false);

  const segments = [
    { value: '' as const,          label: t('all') },
    { value: 'Cargo' as PostType,     label: t('post_type_cargo') },
    { value: 'Transport' as PostType, label: t('post_type_transport') },
    { value: 'Dogruz' as PostType,    label: t('post_type_dogruz') },
  ];

  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isLoading,
    refetch,
  } = useInfiniteQuery({
    queryKey: ['posts', postType, fromCity, toCity],
    queryFn: ({ pageParam = 1 }) =>
      postsApi.list({
        postType: postType || undefined,
        fromCity: fromCity || undefined,
        toCity: toCity || undefined,
        page: pageParam as number,
        pageSize: PAGE_SIZE,
      }),
    initialPageParam: 1,
    getNextPageParam: (last, pages) =>
      last.hasMore ? pages.length + 1 : undefined,
  });

  const posts = data?.pages.flatMap((p) => p.items) ?? [];

  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    const el = e.currentTarget;
    if (el.scrollHeight - el.scrollTop < el.clientHeight + 200 && hasNextPage && !isFetchingNextPage) {
      fetchNextPage();
    }
  }, [fetchNextPage, hasNextPage, isFetchingNextPage]);

  if (showCreate) {
    return (
      <CreateAdPage
        onBack={() => setShowCreate(false)}
        onSuccess={() => { setShowCreate(false); refetch(); }}
      />
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}
    >
      <NavHeader
        title={t('nav_ads')}
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

      <div style={{ padding: '12px 16px', display: 'flex', flexDirection: 'column', gap: 10 }}>
        <SegmentedControl
          segments={segments}
          value={postType}
          onChange={(v) => setPostType(v)}
        />

        {/* Filters */}
        <div style={{ display: 'flex', gap: 8 }}>
          <input
            className="input"
            style={{ flex: 1 }}
            placeholder={t('filter_from')}
            value={fromCity}
            onChange={(e) => setFromCity(e.target.value)}
          />
          <input
            className="input"
            style={{ flex: 1 }}
            placeholder={t('filter_to')}
            value={toCity}
            onChange={(e) => setToCity(e.target.value)}
          />
        </div>
      </div>

      <div
        onScroll={handleScroll}
        style={{ flex: 1, overflowY: 'auto', padding: '0 16px' }}
      >
        {isLoading && (
          <>
            <AdCardSkeleton />
            <AdCardSkeleton />
            <AdCardSkeleton />
          </>
        )}

        {!isLoading && posts.length === 0 && (
          <EmptyState
            icon="📭"
            title={t('no_ads')}
            subtitle={t('no_ads_sub')}
            action={{ label: t('create_ad'), onClick: () => setShowCreate(true) }}
          />
        )}

        {posts.map((post) => (
          <AdCard key={post.id} post={post} />
        ))}

        {isFetchingNextPage && <AdCardSkeleton />}
      </div>
    </motion.div>
  );
}
