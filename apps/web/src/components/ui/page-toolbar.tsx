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
          className="relative isolate no-scrollbar flex h-[92px] min-w-0 flex-1 items-stretch overflow-x-auto border border-hairline/80 bg-card/35 px-2 shadow-[0_18px_40px_hsl(var(--foreground)/0.05)] sm:rounded-[18px] dark:border-white/[0.08] dark:bg-white/[0.02]"
        >
          <span
            aria-hidden="true"
            className="pointer-events-none absolute left-8 right-8 top-[34px] h-px bg-gradient-to-r from-transparent via-hairline to-transparent"
          />
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-8 bottom-0 h-px bg-gradient-to-r from-transparent via-primary/25 to-transparent"
          />
          <div className="relative flex h-full min-w-max flex-1 items-stretch justify-around">
            {tabs.map((tab) => (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.end}
                className={({ isActive }) =>
                  cn(
                    "group relative flex min-w-[9.25rem] flex-1 flex-col items-center gap-2 px-4 pb-2 pt-3 text-center transition-[background-color,color,box-shadow] duration-200 first:rounded-l-[12px] last:rounded-r-[12px]",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "bg-primary/[0.07] font-semibold text-foreground shadow-[inset_0_0_0_1px_hsl(var(--primary)/0.22)]"
                      : "text-muted-foreground hover:bg-card/55 hover:text-foreground"
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <span
                      aria-hidden="true"
                      className={cn(
                        "pointer-events-none absolute left-1/2 top-0 h-0.5 w-12 -translate-x-1/2 rounded-full transition-[background-color,box-shadow]",
                        isActive ? "bg-primary shadow-[0_0_14px_hsl(var(--primary)/0.75)]" : "bg-transparent group-hover:bg-primary/35"
                      )}
                    />
                    <span
                      className={cn(
                        "relative z-10 flex h-9 w-9 shrink-0 items-center justify-center rounded-full border-2 transition-[background-color,border-color,color,transform,box-shadow] duration-200 group-hover:-translate-y-0.5 [&>svg]:h-3.5 [&>svg]:w-3.5",
                        isActive
                          ? "border-primary/55 bg-gradient-accent text-primary-foreground shadow-[0_0_0_4px_hsl(var(--primary)/0.12),0_8px_18px_hsl(var(--primary)/0.25)]"
                          : "border-hairline bg-background/85 text-muted-foreground group-hover:border-primary/35 group-hover:bg-card group-hover:text-foreground"
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
