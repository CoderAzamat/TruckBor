import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MapContainer, TileLayer, Marker, Popup, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import { useQuery } from '@tanstack/react-query';
import { mapApi } from '../services/api';
import { useLocale } from '../store/locale';
import type { PostType } from '../types';
import NavHeader from '../components/NavHeader';
import SegmentedControl from '../components/SegmentedControl';

// fix default icon
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

const makeIcon = (emoji: string) =>
  L.divIcon({
    html: `<div style="font-size:24px;line-height:1">${emoji}</div>`,
    className: 'custom-marker',
    iconSize: [30, 30],
    iconAnchor: [15, 15],
  });

const ICONS: Record<string, L.DivIcon> = {
  Cargo: makeIcon('📦'),
  Transport: makeIcon('🚛'),
  Dogruz: makeIcon('📫'),
};

interface Bounds {
  minLat: number; maxLat: number;
  minLng: number; maxLng: number;
}

function BoundsWatcher({ onChange }: { onChange: (b: Bounds) => void }) {
  const map = useMapEvents({
    moveend: () => {
      const b = map.getBounds();
      onChange({
        minLat: b.getSouth(), maxLat: b.getNorth(),
        minLng: b.getWest(),  maxLng: b.getEast(),
      });
    },
    zoomend: () => {
      const b = map.getBounds();
      onChange({
        minLat: b.getSouth(), maxLat: b.getNorth(),
        minLng: b.getWest(),  maxLng: b.getEast(),
      });
    },
  });
  return null;
}

export default function MapPage() {
  const { t } = useLocale();
  const [postType, setPostType] = useState<PostType | ''>('');
  const [bounds, setBounds] = useState<Bounds>({
    minLat: 37.0, maxLat: 46.0,
    minLng: 56.0, maxLng: 73.0,
  });

  const segments = [
    { value: '' as const,            label: t('all') },
    { value: 'Cargo' as PostType,     label: '📦' },
    { value: 'Transport' as PostType, label: '🚛' },
    { value: 'Dogruz' as PostType,    label: '📫' },
  ];

  const { data: posts = [] } = useQuery({
    queryKey: ['map-posts', bounds, postType],
    queryFn: () => mapApi.postsInBounds({ ...bounds, postType: postType || undefined }),
    staleTime: 60_000,
  });

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.2 }}
      style={{ height: '100vh', display: 'flex', flexDirection: 'column' }}
    >
      <NavHeader title={t('nav_map')} />

      {/* Type filter */}
      <div style={{ padding: '8px 16px', background: 'var(--bg-base)', zIndex: 10 }}>
        <SegmentedControl segments={segments} value={postType} onChange={setPostType} />
      </div>

      <div style={{ flex: 1, position: 'relative' }}>
        <MapContainer
          center={[41.2995, 69.2401]} // Tashkent
          zoom={7}
          style={{ width: '100%', height: '100%' }}
          zoomControl={false}
        >
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; OpenStreetMap'
          />
          <BoundsWatcher onChange={setBounds} />
          {posts.map((post) => (
            <Marker
              key={post.id}
              position={[post.fromLat ?? 41.2995, post.fromLng ?? 69.2401]}
              icon={ICONS[post.postType] ?? ICONS.Cargo}
            >
              <Popup>
                <div style={{ minWidth: 160 }}>
                  <div style={{ fontWeight: 600, marginBottom: 4 }}>
                    {post.fromCity} → {post.toCity}
                  </div>
                  {post.cargoType && <div>📦 {post.cargoType}</div>}
                  {post.vehicleType && <div>🚛 {post.vehicleType}</div>}
                  {post.price && <div>💰 {post.price}</div>}
                </div>
              </Popup>
            </Marker>
          ))}
        </MapContainer>

        {/* Post count badge */}
        <div style={{
          position: 'absolute',
          top: 12,
          right: 12,
          zIndex: 1000,
          background: 'var(--bg-elevated)',
          border: '0.5px solid var(--border-default)',
          borderRadius: 20,
          padding: '4px 12px',
          fontSize: 13,
          fontWeight: 600,
          color: 'var(--text-primary)',
          backdropFilter: 'blur(10px)',
        }}>
          {posts.length} {t('posts_on_map')}
        </div>
      </div>
    </motion.div>
  );
}
