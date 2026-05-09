import { create } from 'zustand';
import { persist } from 'zustand/middleware';

type Mode = 'dark' | 'light' | 'auto';

interface ThemeState {
  mode: Mode;
  resolved: 'dark' | 'light';
  setMode: (m: Mode) => void;
  applyFromTelegram: (scheme?: 'dark' | 'light') => void;
}

function apply(resolved: 'dark' | 'light') {
  const root = document.documentElement;
  if (resolved === 'light') root.classList.add('light');
  else root.classList.remove('light');
}

function resolve(mode: Mode, tgScheme?: 'dark' | 'light'): 'dark' | 'light' {
  if (mode === 'auto') return tgScheme ?? (window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark');
  return mode;
}

export const useTheme = create<ThemeState>()(
  persist(
    (set, get) => ({
      mode: 'auto',
      resolved: 'dark',
      setMode: (mode) => {
        const resolved = resolve(mode);
        apply(resolved);
        set({ mode, resolved });
      },
      applyFromTelegram: (scheme) => {
        const resolved = resolve(get().mode, scheme);
        apply(resolved);
        set({ resolved });
      }
    }),
    { name: 'truckbor-theme' }
  )
);
