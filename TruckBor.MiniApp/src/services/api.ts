import axios, { type AxiosError } from 'axios';
import type { User, Post, Tariff, Card, PaginatedResult } from '@/types';
import type { PostType } from '@/types';

const API_BASE = import.meta.env.VITE_API_URL || '';

const api = axios.create({
  baseURL: API_BASE,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' }
});

api.interceptors.request.use((config) => {
  const initData = window.Telegram?.WebApp?.initData;
  if (initData) config.headers['X-Telegram-Init-Data'] = initData;
  const token = localStorage.getItem('tb_token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (r) => r,
  (e: AxiosError) => {
    if (e.response?.status === 401) localStorage.removeItem('tb_token');
    return Promise.reject(e);
  }
);

// ── Auth ─────────────────────────────────────────────────────
export const authApi = {
  telegramAuth: async (initData: string) => {
    const { data } = await api.post<{ token: string; user: User }>('/api/miniapp/auth', { initData });
    if (data.token) localStorage.setItem('tb_token', data.token);
    return data;
  },
  me: async () => (await api.get<User>('/api/miniapp/me')).data
};

// ── Posts ─────────────────────────────────────────────────────
export const postsApi = {
  list: async (params: {
    postType?: PostType;
    fromCity?: string;
    toCity?: string;
    vehicleType?: string;
    page?: number;
    pageSize?: number;
  }) => (await api.get<PaginatedResult<Post>>('/api/miniapp/posts', { params })).data,

  mine: async () => (await api.get<Post[]>('/api/miniapp/posts/mine')).data,

  create: async (payload: {
    postType: PostType;
    fromCity: string;
    toCity: string;
    cargoType?: string;
    weight?: string;
    vehicleType?: string;
    price?: string;
    contactPhone: string;
    description?: string;
  }) => (await api.post<Post>('/api/miniapp/posts', payload)).data,

  delete: async (id: number) => api.delete(`/api/miniapp/posts/${id}`),

  showPhone: async (id: number) =>
    (await api.post<{ phone: string }>(`/api/miniapp/posts/${id}/phone`)).data,

  incrementView: async (id: number) =>
    api.post(`/api/miniapp/posts/${id}/view`).catch(() => {})
};

// ── Map ───────────────────────────────────────────────────────
export const mapApi = {
  postsInBounds: async (params: {
    minLat: number; maxLat: number;
    minLng: number; maxLng: number;
    postType?: PostType;
  }) => (await api.get<Post[]>('/api/miniapp/map/posts', { params })).data
};

// ── Market ────────────────────────────────────────────────────
export const marketApi = {
  tariffs: async () => (await api.get<Tariff[]>('/api/miniapp/tariffs')).data,
  cards: async () => (await api.get<Card[]>('/api/miniapp/cards')).data
};

export default api;
