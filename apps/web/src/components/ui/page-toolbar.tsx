import * as React from "react";
import { NavLink } from "react-router-dom";
import { titleCaseLabel } from "../../lib/title-case";
import { cn } from "../../lib/utils";

export interface ToolbarTab {
  to: string;
  label: React.ReactNode;
  end?: boolean;
  icon?: React.ReactNode;
}

interface PageToolbarProps {
  /** Sub-pages of this area. Rendered as a consistent navigation rail. */
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
          className="relative isolate no-scrollbar flex h-[84px] min-w-0 flex-1 items-stretch overflow-x-auto rounded-[24px] border border-hairline/90 bg-surface-1/80 p-1.5 shadow-[0_18px_40px_hsl(var(--foreground)/0.06)] dark:border-white/[0.09] dark:bg-white/[0.025]"
        >
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-8 top-0 h-px bg-gradient-to-r from-transparent via-primary/55 to-transparent"
          />
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-8 bottom-1 h-px bg-gradient-to-r from-transparent via-primary/25 to-transparent"
          />
          <div className="relative flex h-full min-w-max items-stretch">
            {tabs.map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.end}
                className={({ isActive }) =>
                  cn(
                    "group relative flex min-w-[9.25rem] flex-1 flex-col items-center justify-center gap-1.5 border-r border-hairline/60 px-5 py-2 text-center transition-[background-color,color,box-shadow] duration-200 first:rounded-l-[18px] last:border-r-0 last:rounded-r-[18px] dark:border-white/[0.07]",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "bg-gradient-to-b from-primary/[0.14] via-card/55 to-transparent font-semibold text-foreground shadow-[inset_0_0_0_1px_hsl(var(--primary)/0.22),0_10px_24px_hsl(var(--foreground)/0.08)]"
                      : "text-muted-foreground hover:bg-card/55 hover:text-foreground"
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      aria-hidden="true"
                      className={cn(
                        "pointer-events-none absolute inset-x-4 top-0 h-0.5 rounded-full transition-[background-color,box-shadow]",
                        isActive ? "bg-primary shadow-[0_0_14px_hsl(var(--primary)/0.75)]" : "bg-transparent group-hover:bg-primary/35"
                      )}
                    />
                    <span
                      className={cn(
                        "flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border transition-[background-color,border-color,color,transform] duration-200 group-hover:-translate-y-0.5 [&>svg]:h-3.5 [&>svg]:w-3.5",
                        isActive
                          ? "border-primary/30 bg-gradient-accent text-primary-foreground shadow-[0_6px_16px_hsl(var(--primary)/0.22)]"
                          : "border-hairline/70 bg-surface-2/55 text-muted-foreground group-hover:border-primary/25 group-hover:bg-card group-hover:text-foreground"
                      )}
                      aria-hidden="true"
                    >
                      {tab.icon ?? <span className="h-1.5 w-1.5 rounded-full bg-current" />}
                    </span>
                    <span className="whitespace-nowrap text-[length:var(--type-body-sm)] leading-tight">{typeof tab.label === "string" ? titleCaseLabel(tab.label) : tab.label}</span>
                    <span
                      aria-hidden="true"
                      className={cn(
                        "pointer-events-none absolute bottom-0 left-1/2 h-1 w-10 -translate-x-1/2 rounded-full bg-gradient-to-r from-transparent via-primary to-transparent transition-opacity",
                        isActive ? "opacity-100" : "opacity-0"
                      )}
                    />
                  </>
                )}
              </NavLink>
            ))}
          </div>
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
