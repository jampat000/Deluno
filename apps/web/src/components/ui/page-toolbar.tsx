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
          className="relative isolate no-scrollbar flex h-[calc(var(--control-height)+12px)] min-w-0 flex-1 items-stretch overflow-x-auto rounded-[22px] border border-hairline/90 bg-surface-1/80 p-1.5 shadow-[0_18px_40px_hsl(var(--foreground)/0.06)] dark:border-white/[0.09] dark:bg-white/[0.025]"
        >
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-8 top-0 h-px bg-gradient-to-r from-transparent via-primary/55 to-transparent"
          />
          <div className="relative flex min-w-max items-stretch">
            {tabs.map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.end}
                className={({ isActive }) =>
                  cn(
                    "group relative flex min-h-[calc(var(--control-height)+4px)] shrink-0 items-center gap-3 border-r border-hairline/60 px-4 text-[length:var(--type-body-sm)] font-medium transition-[background-color,color,box-shadow] duration-200 first:rounded-l-[15px] last:border-r-0 last:rounded-r-[15px] dark:border-white/[0.07]",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "bg-card/95 font-semibold text-foreground shadow-[0_10px_24px_hsl(var(--foreground)/0.08)]"
                      : "text-muted-foreground hover:bg-card/55 hover:text-foreground"
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      aria-hidden="true"
                      className={cn(
                        "absolute inset-y-2 left-0 w-0.5 rounded-full transition-[background-color,box-shadow]",
                        isActive ? "bg-primary shadow-[0_0_12px_hsl(var(--primary)/0.75)]" : "bg-transparent"
                      )}
                    />
                    <span
                      className={cn(
                        "flex h-8 w-8 shrink-0 items-center justify-center rounded-xl border transition-[background-color,border-color,color,transform] duration-200 [&>svg]:h-3.5 [&>svg]:w-3.5",
                        isActive
                          ? "border-primary/30 bg-gradient-accent text-primary-foreground shadow-[0_6px_16px_hsl(var(--primary)/0.22)]"
                          : "border-transparent bg-transparent text-muted-foreground group-hover:border-hairline/80 group-hover:bg-card group-hover:text-foreground"
                      )}
                      aria-hidden="true"
                    >
                      {tab.icon ?? <span className="h-1.5 w-1.5 rounded-full bg-current" />}
                    </span>
                    <span className="whitespace-nowrap">{typeof tab.label === "string" ? titleCaseLabel(tab.label) : tab.label}</span>
                    <span
                      aria-hidden="true"
                      className={cn(
                        "pointer-events-none absolute inset-x-4 bottom-0 h-0.5 rounded-full bg-gradient-to-r from-transparent via-primary to-transparent transition-opacity",
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
