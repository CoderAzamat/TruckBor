interface Props {
  icon?: string;
  title: string;
  subtitle?: string;
  action?: { label: string; onClick: () => void };
}

export default function EmptyState({ icon = '📭', title, subtitle, action }: Props) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-8 text-center">
      <div style={{ fontSize: 48, marginBottom: 12 }}>{icon}</div>
      <div style={{ fontSize: 17, fontWeight: 600, color: 'var(--text-primary)', marginBottom: 6 }}>
        {title}
      </div>
      {subtitle && (
        <div style={{ fontSize: 14, color: 'var(--text-tertiary)', marginBottom: 20 }}>
          {subtitle}
        </div>
      )}
      {action && (
        <button className="btn-primary" style={{ padding: '12px 24px' }} onClick={action.onClick}>
          {action.label}
        </button>
      )}
    </div>
  );
}
