import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { uz } from '../locales/uz';
import { ru } from '../locales/ru';
import { en } from '../locales/en';

export type Locale = 'uz' | 'ru' | 'en';

const dicts: Record<Locale, Record<string, string>> = { uz, ru, en };

interface LocaleState {
  locale: Locale;
  t: (key: string, params?: Record<string, string | number>) => string;
  setLocale: (l: Locale) => void;
}

export const useLocale = create<LocaleState>()(
  persist(
    (set, get) => ({
      locale: 'uz',
      t: (key, params) => {
        const dict = dicts[get().locale] ?? uz;
        let str = dict[key] ?? key;
        if (params) {
          for (const [k, v] of Object.entries(params)) {
            str = str.replace(`{${k}}`, String(v));
          }
        }
        return str;
      },
      setLocale: (locale) => set({ locale })
    }),
    { name: 'truckbor-locale' }
  )
);
