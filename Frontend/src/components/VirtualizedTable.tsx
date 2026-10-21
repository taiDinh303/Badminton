import { useMemo, useRef, useState } from "react";
import { clsx } from "clsx";
import { ArrowUp, ArrowDown, ArrowUpDown, Inbox } from "lucide-react";

/**
 * Lightweight virtualized table (docs 07 §6: sticky header, virtualize > 100
 * rows, sortable columns). Only ~30 rows in the DOM at a time.
 */

export interface Column<T> {
  key: string;
  label: string;
  width?: number; // px, optional
  sortable?: boolean;
  align?: "left" | "right" | "center";
  render: (row: T) => React.ReactNode;
}

export interface VirtualizedTableProps<T> {
  rows: T[];
  columns: Column<T>[];
  rowKey: (row: T) => string | number;
  rowHeight?: number;
  headerHeight?: number;
  maxHeight?: number;
  onRowClick?: (row: T) => void;
  emptyLabel?: string;
  className?: string;
}

export function VirtualizedTable<T>({
  rows,
  columns,
  rowKey,
  rowHeight = 48,
  headerHeight = 44,
  maxHeight = 480,
  onRowClick,
  emptyLabel = "Không có dữ liệu.",
  className,
}: VirtualizedTableProps<T>) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [scrollTop, setScrollTop] = useState(0);
  const [sortKey, setSortKey] = useState<string | null>(null);
  const [sortDir, setSortDir] = useState<1 | -1>(1);

  const totalW = useMemo(() => {
    const fixed = columns.reduce((sum, c) => sum + (c.width ?? 0), 0);
    return fixed > 0 ? fixed : 800;
  }, [columns]);

  const sorted = useMemo(() => {
    if (!sortKey) return rows;
    const arr = [...rows];
    arr.sort((a, b) => {
      const av = (a as Record<string, unknown>)[sortKey];
      const bv = (b as Record<string, unknown>)[sortKey];
      if (typeof av === "number" && typeof bv === "number")
        return (av - bv) * sortDir;
      return String(av ?? "").localeCompare(String(bv ?? "")) * sortDir;
    });
    return arr;
  }, [rows, sortKey, sortDir]);

  const overscan = 6;
  const totalH = sorted.length * rowHeight;
  const startIdx = Math.max(0, Math.floor(scrollTop / rowHeight) - overscan);
  const endIdx = Math.min(
    sorted.length,
    Math.ceil((scrollTop + maxHeight) / rowHeight) + overscan,
  );
  const visible = sorted.slice(startIdx, endIdx);
  const offsetTop = startIdx * rowHeight;

  function toggleSort(key: string, sortable: boolean) {
    if (!sortable) return;
    if (sortKey === key) {
      if (sortDir === 1) setSortDir(-1);
      else {
        setSortKey(null);
        setSortDir(1);
      }
    } else {
      setSortKey(key);
      setSortDir(1);
    }
  }

  function SortIcon({ colKey }: { colKey: string }) {
    if (sortKey !== colKey)
      return <ArrowUpDown className="h-3 w-3 opacity-40" />;
    return sortDir === 1 ? (
      <ArrowUp className="h-3 w-3" />
    ) : (
      <ArrowDown className="h-3 w-3" />
    );
  }

  return (
    <div
      className={clsx(
        "flex flex-col overflow-hidden rounded-xl border border-border bg-card shadow-sm shadow-black/[0.03]",
        className,
      )}
    >
      <div
        ref={scrollRef}
        className="overflow-auto"
        style={{ maxHeight }}
        onScroll={(e) => setScrollTop((e.target as HTMLDivElement).scrollTop)}
      >
        {sorted.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 py-16 text-sm text-muted">
            <Inbox className="h-6 w-6 opacity-50" />
            {emptyLabel}
          </div>
        ) : (
          <>
            {/* Header */}
            <div
              className="sticky top-0 z-10 flex border-b border-border bg-bg/80 text-[11px] font-semibold tracking-wide text-muted uppercase backdrop-blur"
              style={{ height: headerHeight, minWidth: totalW }}
            >
              {columns.map((c) => (
                <div
                  key={c.key}
                  className={clsx(
                    "flex items-center gap-1 px-3",
                    c.sortable && "cursor-pointer select-none hover:text-fg",
                    c.align === "right" && "justify-end",
                    c.align === "center" && "justify-center",
                    sortKey === c.key && "text-fg",
                  )}
                  style={{ width: c.width, flexShrink: 0 }}
                  onClick={() => toggleSort(c.key, !!c.sortable)}
                  role={c.sortable ? "button" : undefined}
                  tabIndex={c.sortable ? 0 : undefined}
                >
                  <span className="truncate">{c.label}</span>
                  {c.sortable && <SortIcon colKey={c.key} />}
                </div>
              ))}
            </div>

            <div style={{ height: totalH, position: "relative", minWidth: totalW }}>
              <div
                style={{
                  position: "absolute",
                  top: offsetTop,
                  left: 0,
                  right: 0,
                  width: totalW,
                }}
              >
                {visible.map((row, i) => (
                  <div
                    key={rowKey(row)}
                    onClick={onRowClick ? () => onRowClick(row) : undefined}
                    className={clsx(
                      "flex items-stretch border-b border-border/60 text-sm transition-colors",
                      i % 2 === 1 && "bg-bg/40",
                      "hover:bg-primary/[0.04]",
                      onRowClick && "cursor-pointer",
                    )}
                    style={{ height: rowHeight }}
                  >
                    {columns.map((c) => (
                      <div
                        key={c.key}
                        className={clsx(
                          "flex min-w-0 items-center overflow-hidden px-3",
                          c.align === "right" && "justify-end",
                          c.align === "center" && "justify-center",
                        )}
                        style={{ width: c.width, flexShrink: 0 }}
                      >
                        {c.render(row)}
                      </div>
                    ))}
                  </div>
                ))}
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
