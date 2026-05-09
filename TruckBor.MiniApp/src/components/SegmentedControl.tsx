interface Segment<T extends string> {
  value: T;
  label: string;
}

interface Props<T extends string> {
  segments: Segment<T>[];
  value: T;
  onChange: (v: T) => void;
}

export default function SegmentedControl<T extends string>({ segments, value, onChange }: Props<T>) {
  return (
    <div
      style={{
        display: 'flex',
        background: 'var(--bg-overlay)',
        border: '0.5px solid var(--border-default)',
        borderRadius: 'var(--r-md)',
        padding: 3,
        gap: 2,
      }}
    >
      {segments.map((seg) => {
        const active = seg.value === value;
        return (
          <button
            key={seg.value}
            onClick={() => onChange(seg.value)}
            style={{
              flex: 1,
              padding: '8px 4px',
              borderRadius: 'calc(var(--r-md) - 3px)',
              fontSize: 13,
              fontWeight: active ? 600 : 400,
              border: 'none',
              cursor: 'pointer',
              transition: 'all 0.15s',
              background: active
                ? 'linear-gradient(135deg, var(--accent-gradient-from), var(--accent-gradient-to))'
                : 'transparent',
              color: active ? '#fff' : 'var(--text-secondary)',
            }}
          >
            {seg.label}
          </button>
        );
      })}
    </div>
  );
}
