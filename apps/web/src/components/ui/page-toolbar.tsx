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
export function PageToolbar({ tabs, left, actions, className }: PageToolbarProps) {
  return (
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)]", className)}>
      {tabs?.length ? (
        <nav
          aria-label="Sections"
          className="no-scrollbar flex h-[calc(var(--control-height)+6px)] min-w-0 flex-1 items-center gap-0.5 overflow-x-auto rounded-[14px] border border-hairline bg-surface-2/75 p-1.5 shadow-card dark:border-white/[0.08]"
        >
          {tabs.map((tab) => (
            <NavLink
              key={tab.to}
              to={tab.to}
              end={tab.end}
              className={({ isActive }) =>
                cn(
                  "group relative flex h-full shrink-0 items-center gap-2 rounded-[10px] px-3.5 text-[length:var(--type-body-sm)] font-medium transition-all after:absolute after:inset-x-3 after:bottom-0 after:h-0.5 after:rounded-full after:bg-primary after:transition-opacity",
                  "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                  isActive
                    ? "bg-card font-semibold text-foreground shadow-card ring-1 ring-inset ring-primary/25 after:opacity-100"
                    : "text-muted-foreground after:opacity-0 hover:bg-surface-1/80 hover:text-foreground"
                )
              }
            >
              {({ isActive }) => (
                <>
                  {tab.icon ? (
                    <span
                      className={cn(
                        "flex h-6 w-6 shrink-0 items-center justify-center rounded-md border [&>svg]:h-3.5 [&>svg]:w-3.5",
                        isActive ? "border-primary/25 bg-primary/12 text-primary" : "border-transparent bg-surface-1/70 text-muted-foreground group-hover:border-hairline/70 group-hover:bg-card group-hover:text-foreground"
                      )}
                      aria-hidden="true"
                    >
                      {tab.icon}
                    </span>
                  ) : null}
                  {tab.label}
                </>
              )}
            </NavLink>
          ))}
        </nav>
      ) : left ? (
        <div className="flex min-w-0 items-center gap-2">{left}</div>
      ) : (
        <span />
      )}
      {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
    </div>
  );
}
