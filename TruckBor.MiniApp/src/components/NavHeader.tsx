import type { ReactNode } from 'react';

interface Props {
  title: string;
  left?: ReactNode;
  right?: ReactNode;
}

export default function NavHeader({ title, left, right }: Props) {
  return (
    <div className="nav-header">
      <div className="w-10">{left}</div>
      <span style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)' }}>{title}</span>
      <div className="w-10 flex justify-end">{right}</div>
    </div>
  );
}
