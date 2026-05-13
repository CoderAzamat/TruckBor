import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatMoney(amount: number | string, currency = "so'm"): string {
  const n = typeof amount === 'string' ? parseFloat(amount) : amount;
  if (isNaN(n)) return `0 ${currency}`;
  const formatted = n.toLocaleString('en-US').replace(/,/g, ' ');
  return currency ? `${formatted} ${currency}` : formatted;
}

export function maskPhone(phone: string): string {
  if (!phone) return '';
  const cleaned = phone.replace(/\D/g, '');
  if (cleaned.length >= 11) {
    return `+${cleaned.slice(0, 3)} ** *** **${cleaned.slice(-2)}`;
  }
  return `${phone.slice(0, 4)}***`;
}

export function formatPhone(phone: string): string {
  if (!phone) return '';
  const c = phone.replace(/\D/g, '');
  if (c.length === 12 && c.startsWith('998')) {
    return `+${c.slice(0, 3)} ${c.slice(3, 5)} ${c.slice(5, 8)} ${c.slice(8, 10)} ${c.slice(10)}`;
  }
  return phone;
}

export function timeAgo(date: string | Date, locale: string = 'uz'): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  const diff = (Date.now() - d.getTime()) / 1000;
  const dict: Record<string, { now: string; m: string; h: string; d: string; ago: string }> = {
    uz:  { now: "hozir",   m: "daq",  h: "soat", d: "kun", ago: '' },
    uzc: { now: "ҳозир",   m: "дақ",  h: "соат", d: "кун", ago: '' },
    ru:  { now: "сейчас",  m: "мин",  h: "ч",    d: "дн",  ago: 'назад' },
    en:  { now: "now",     m: "min",  h: "h",    d: "d",   ago: 'ago' },
    tr:  { now: "şimdi",   m: "dk",   h: "sa",   d: "gün", ago: 'önce' },
  };
  const L = dict[locale] ?? dict['uz'];

  const sfx = (s: string) => L.ago ? `${s} ${L.ago}` : s;
  if (diff < 60) return L.now;
  if (diff < 3600) return sfx(`${Math.floor(diff / 60)} ${L.m}`);
  if (diff < 86400) return sfx(`${Math.floor(diff / 3600)} ${L.h}`);
  if (diff < 604800) return sfx(`${Math.floor(diff / 86400)} ${L.d}`);
  return d.toLocaleDateString();
}

export function maskCardNumber(card: string): string {
  const c = card.replace(/\s/g, '');
  if (c.length !== 16) return card;
  return `${c.slice(0, 4)} **** **** ${c.slice(12)}`;
}
