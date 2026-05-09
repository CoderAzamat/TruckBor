interface Props {
  height?: number | string;
  width?: number | string;
  className?: string;
}

export default function Skeleton({ height = 80, width = '100%', className = '' }: Props) {
  return (
    <div
      className={`skeleton ${className}`}
      style={{ height, width }}
    />
  );
}

export function AdCardSkeleton() {
  return (
    <div className="card" style={{ padding: 16, marginBottom: 10 }}>
      <div className="flex items-center gap-2 mb-3">
        <Skeleton height={22} width={80} />
        <Skeleton height={22} width={60} />
      </div>
      <Skeleton height={16} width="70%" className="mb-2" />
      <Skeleton height={16} width="50%" className="mb-3" />
      <div className="flex gap-2">
        <Skeleton height={22} width={70} />
        <Skeleton height={22} width={70} />
      </div>
    </div>
  );
}
