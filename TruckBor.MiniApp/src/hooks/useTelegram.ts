import { useEffect, useRef, useState } from 'react';

declare global {
  interface Window {
    Telegram?: { WebApp: any };
  }
}

export function useTelegram() {
  const [webApp, setWebApp] = useState<any>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const tg = window.Telegram?.WebApp;
    if (tg) {
      tg.ready();
      tg.expand();
      tg.enableClosingConfirmation();
      setWebApp(tg);
    }
    setReady(true);
  }, []);

  return {
    webApp,
    ready,
    initData: webApp?.initData ?? '',
    initDataUnsafe: webApp?.initDataUnsafe ?? {},
    user: webApp?.initDataUnsafe?.user,
    colorScheme: webApp?.colorScheme as 'dark' | 'light' | undefined,
    platform: webApp?.platform ?? 'unknown',

    close: () => webApp?.close?.(),
    expand: () => webApp?.expand?.(),
    openLink: (url: string) => webApp?.openLink?.(url) ?? window.open(url, '_blank'),
    openTelegramLink: (url: string) => webApp?.openTelegramLink?.(url),
    showAlert: (msg: string) => webApp?.showAlert?.(msg) ?? alert(msg),

    showConfirm: (msg: string): Promise<boolean> =>
      new Promise((resolve) => {
        if (webApp?.showConfirm) webApp.showConfirm(msg, resolve);
        else resolve(window.confirm(msg));
      }),

    haptic: {
      light: () => webApp?.HapticFeedback?.impactOccurred?.('light'),
      medium: () => webApp?.HapticFeedback?.impactOccurred?.('medium'),
      heavy: () => webApp?.HapticFeedback?.impactOccurred?.('heavy'),
      success: () => webApp?.HapticFeedback?.notificationOccurred?.('success'),
      warning: () => webApp?.HapticFeedback?.notificationOccurred?.('warning'),
      error: () => webApp?.HapticFeedback?.notificationOccurred?.('error'),
      select: () => webApp?.HapticFeedback?.selectionChanged?.()
    }
  };
}

export function useBackButton(visible: boolean, onClick: () => void) {
  const cbRef = useRef(onClick);
  cbRef.current = onClick;
  useEffect(() => {
    const tg = window.Telegram?.WebApp?.BackButton;
    if (!tg) return;
    const handler = () => cbRef.current();
    if (visible) { tg.onClick(handler); tg.show(); }
    else tg.hide();
    return () => tg.offClick(handler);
  }, [visible]);
}

export function useMainButton(opts: {
  visible: boolean;
  text?: string;
  onClick?: () => void;
  disabled?: boolean;
  loading?: boolean;
  color?: string;
}) {
  const cbRef = useRef(opts.onClick);
  cbRef.current = opts.onClick;
  useEffect(() => {
    const mb = window.Telegram?.WebApp?.MainButton;
    if (!mb) return;
    if (opts.visible) {
      if (opts.text) mb.setText(opts.text);
      if (opts.color) mb.color = opts.color;
      if (opts.disabled) mb.disable(); else mb.enable();
      if (opts.loading) mb.showProgress(); else mb.hideProgress();
      const handler = () => cbRef.current?.();
      mb.onClick(handler);
      mb.show();
      return () => mb.offClick(handler);
    } else {
      mb.hide();
    }
  }, [opts.visible, opts.text, opts.disabled, opts.loading, opts.color]);
}
