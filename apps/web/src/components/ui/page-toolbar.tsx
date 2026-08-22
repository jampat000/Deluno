import * as React from "react";
import { NavLink } from "react-router-dom";
import { cn } from "../../lib/utils";

export interface ToolbarTab {
  to: string;
  label: React.ReactNode;
  end?: boolean;
  icon?: React.ReactNode;
}

interface PageToolbarProps {
  /** Sub-pages of this area. Rendered as a compact section rail. */
  tabs?: readonly ToolbarTab[];
  /** Optional context line that gives a long tab rail an intentional identity. */
  context?: { label: React.ReactNode; description?: React.ReactNode };
  /**
   * The same slot as `tabs`, for a page whose sections are local state rather
   * than routes — a detail page switching between Episodes and History has no
   * URL per section, but the control still belongs where tabs live.
   * Ignored when `tabs` is set.
   */
  left?: React.ReactNode;
  /** At most two: one primary ("New …") and one secondary. */
  actions?: React.ReactNode;
  className?: string;
}

/**
 * The first row of every collection page: 40px, section rail left, actions right.
 * The topbar already names the page, so there is no H1 here.
 */
export function PageToolbar({ tabs, context, left, actions, className }: PageToolbarProps) {
  return (
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)]", className)}>
      {tabs?.length ? (
        <div className="flex min-w-0 flex-1 flex-col gap-2">
          {context ? (
            <div className="flex min-w-0 items-center gap-2 px-1">
              <span className="inline-flex shrink-0 items-center gap-2 text-[length:var(--type-micro)] font-bold uppercase tracking-[0.14em] text-primary">
                <span className="h-1.5 w-1.5 rounded-full bg-primary shadow-[0_0_0_3px_hsl(var(--primary)/0.12)]" aria-hidden="true" />
                {context.label}
              </span>
              {context.description ? <span className="truncate text-[length:var(--type-caption)] text-muted-foreground">{context.description}</span> : null}
            </div>
          ) : null}
          <nav
            aria-label="Sections"
            className="no-scrollbar flex h-[calc(var(--control-height)+2px)] max-w-full items-center gap-1 overflow-x-auto rounded-xl border border-hairline bg-card p-1 shadow-card dark:border-white/[0.07]"
          >
            {tabs.map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.end}
                className={({ isActive }) =>
                  cn(
                    "group relative flex h-full shrink-0 items-center gap-2 rounded-lg px-3 text-[length:var(--type-body-sm)] font-medium transition-colors after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:rounded-full after:bg-primary after:transition-opacity",
                    "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive ? "bg-primary/[0.10] font-semibold text-primary shadow-sm after:opacity-100" : "text-muted-foreground hover:bg-surface-2 hover:text-foreground after:opacity-0"
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    {tab.icon ? <span className={cn("flex h-5 w-5 shrink-0 items-center justify-center rounded-md [&>svg]:h-4 [&>svg]:w-4", isActive ? "bg-primary/15" : "bg-surface-2 group-hover:bg-card")} aria-hidden="true">{tab.icon}</span> : null}
                    {tab.label}
                  </>
                )}
              </NavLink>
            ))}
          </nav>
        </div>
      ) : left ? (
        <div className="flex min-w-0 items-center gap-2">{left}</div>
      ) : (
        <span />
      )}
      {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
    </div>
  );
}
