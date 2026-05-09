import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { motion } from 'framer-motion';
import { postsApi } from '../services/api';
import { useLocale } from '../store/locale';
import type { PostType } from '../types';
import NavHeader from '../components/NavHeader';
import SegmentedControl from '../components/SegmentedControl';

const schema = z.object({
  postType:     z.enum(['Cargo', 'Transport', 'Dogruz']),
  fromCity:     z.string().min(2),
  toCity:       z.string().min(2),
  cargoType:    z.string().optional(),
  weight:       z.string().optional(),
  vehicleType:  z.string().optional(),
  price:        z.string().optional(),
  contactPhone: z.string().min(9),
  description:  z.string().optional(),
});

type FormData = z.infer<typeof schema>;

interface Props {
  onBack: () => void;
  onSuccess: () => void;
}

export default function CreateAdPage({ onBack, onSuccess }: Props) {
  const { t } = useLocale();

  const { register, handleSubmit, watch, setValue, formState: { errors, isSubmitting } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { postType: 'Cargo' },
  });

  const postType = watch('postType');

  const typeSegments = [
    { value: 'Cargo' as PostType,     label: `📦 ${t('post_type_cargo')}` },
    { value: 'Transport' as PostType, label: `🚛 ${t('post_type_transport')}` },
    { value: 'Dogruz' as PostType,    label: `📫 ${t('post_type_dogruz')}` },
  ];

  const onSubmit = async (data: FormData) => {
    await postsApi.create(data);
    onSuccess();
  };

  return (
    <motion.div
      initial={{ opacity: 0, x: 40 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: 40 }}
      transition={{ duration: 0.2 }}
      style={{ minHeight: '100vh' }}
    >
      <NavHeader
        title={t('create_ad')}
        left={
          <button
            onClick={onBack}
            style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 4 }}
          >
            <span style={{ fontSize: 24, color: 'var(--text-primary)' }}>‹</span>
          </button>
        }
      />

      <form onSubmit={handleSubmit(onSubmit)} style={{ padding: '16px' }}>
        {/* Post type */}
        <div style={{ marginBottom: 16 }}>
          <div className="section-label">{t('post_type')}</div>
          <SegmentedControl
            segments={typeSegments}
            value={postType}
            onChange={(v) => setValue('postType', v)}
          />
        </div>

        {/* From city */}
        <div style={{ marginBottom: 12 }}>
          <label className="section-label">{t('from_city')}</label>
          <input
            className="input"
            placeholder={t('from_city_placeholder')}
            {...register('fromCity')}
          />
          {errors.fromCity && (
            <div style={{ fontSize: 12, color: 'var(--danger)', marginTop: 4 }}>
              {t('required_field')}
            </div>
          )}
        </div>

        {/* To city */}
        <div style={{ marginBottom: 12 }}>
          <label className="section-label">{t('to_city')}</label>
          <input
            className="input"
            placeholder={t('to_city_placeholder')}
            {...register('toCity')}
          />
          {errors.toCity && (
            <div style={{ fontSize: 12, color: 'var(--danger)', marginTop: 4 }}>
              {t('required_field')}
            </div>
          )}
        </div>

        {/* Cargo specific */}
        {(postType === 'Cargo' || postType === 'Dogruz') && (
          <>
            <div style={{ marginBottom: 12 }}>
              <label className="section-label">{t('cargo_type')}</label>
              <input
                className="input"
                placeholder={t('cargo_type_placeholder')}
                {...register('cargoType')}
              />
            </div>
            <div style={{ marginBottom: 12 }}>
              <label className="section-label">{t('weight')}</label>
              <input
                className="input"
                placeholder={t('weight_placeholder')}
                {...register('weight')}
              />
            </div>
          </>
        )}

        {/* Transport specific */}
        {(postType === 'Transport' || postType === 'Dogruz') && (
          <div style={{ marginBottom: 12 }}>
            <label className="section-label">{t('vehicle_type')}</label>
            <input
              className="input"
              placeholder={t('vehicle_type_placeholder')}
              {...register('vehicleType')}
            />
          </div>
        )}

        {/* Price */}
        <div style={{ marginBottom: 12 }}>
          <label className="section-label">{t('price')}</label>
          <input
            className="input"
            placeholder={t('price_placeholder')}
            {...register('price')}
          />
        </div>

        {/* Phone */}
        <div style={{ marginBottom: 12 }}>
          <label className="section-label">{t('contact_phone')}</label>
          <input
            className="input"
            type="tel"
            placeholder="+998 90 123 45 67"
            {...register('contactPhone')}
          />
          {errors.contactPhone && (
            <div style={{ fontSize: 12, color: 'var(--danger)', marginTop: 4 }}>
              {t('required_field')}
            </div>
          )}
        </div>

        {/* Description */}
        <div style={{ marginBottom: 24 }}>
          <label className="section-label">{t('description')}</label>
          <textarea
            className="input"
            rows={3}
            placeholder={t('description_placeholder')}
            style={{ resize: 'none' }}
            {...register('description')}
          />
        </div>

        <button
          type="submit"
          className="btn-primary"
          style={{ width: '100%' }}
          disabled={isSubmitting}
        >
          {isSubmitting ? '⏳ …' : `✅ ${t('publish_ad')}`}
        </button>
      </form>
    </motion.div>
  );
}
