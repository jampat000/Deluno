import * as React from "react";
import { ArrowRight, Check } from "lucide-react";
import { NavLink, useLocation } from "react-router-dom";
import { titleCaseLabel } from "../../lib/title-case";
import { cn } from "../../lib/utils";

export interface ToolbarTab {
  to: string;
  label: React.ReactNode;
  end?: boolean;
  icon?: React.ReactNode;
  /** A verified completion state for setup-style rails. Never inferred from navigation alone. */
  status?: "complete" | "pending";
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
  const location = useLocation();
  const activeIndex = tabs?.findIndex((tab) => isTabActive(location.pathname, tab)) ?? -1;

  return (
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)]", className)}>
      {tabs?.length ? (
        <nav
          aria-label="Sections"
          className="relative isolate no-scrollbar flex h-[var(--toolbar-height)] min-w-0 flex-1 items-center overflow-x-auto rounded-xl border border-hairline/80 bg-card/45 shadow-[0_12px_28px_hsl(var(--foreground)/0.04)] dark:border-white/[0.08] dark:bg-white/[0.02]"
        >
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-6 bottom-0 h-px bg-gradient-to-r from-transparent via-primary/25 to-transparent"
          />
          <div className="relative flex h-full min-w-max flex-1 items-stretch">
            {tabs.map((tab, index) => {
              const isActive = isTabActive(location.pathname, tab);
              const isComplete = tab.status === "complete";
              const isBeforeActive = activeIndex >= 0 && index < activeIndex;
              const connectorComplete = isComplete || isBeforeActive;

              return (
                <NavLink
                  key={tab.to}
                  to={tab.to}
                  end={tab.end}
                  className={cn(
                    "group relative flex h-full min-w-[8.5rem] flex-1 items-center gap-2 border-r border-hairline/60 px-2.5 text-left transition-[background-color,border-color,color,box-shadow,transform] duration-200 last:border-r-0 first:rounded-l-[11px] last:rounded-r-[11px]",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "bg-primary/[0.14] font-semibold text-foreground shadow-[inset_0_0_0_1px_hsl(var(--primary)/0.2)]"
                      : isComplete
                        ? "bg-success/[0.05] text-foreground hover:bg-success/[0.09]"
                        : "text-muted-foreground hover:bg-card/70 hover:text-foreground"
                  )}
                >
                  <span
                    className={cn(
                      "relative z-10 flex h-7 w-7 shrink-0 items-center justify-center rounded-md border transition-[background-color,border-color,color,transform,box-shadow] duration-200 group-hover:-translate-y-px [&>svg]:h-3.5 [&>svg]:w-3.5",
                      isActive
                        ? "border-primary/55 bg-gradient-accent text-primary-foreground shadow-[0_0_0_3px_hsl(var(--primary)/0.1),0_6px_14px_hsl(var(--primary)/0.2)]"
                        : isComplete
                          ? "border-success/35 bg-success/10 text-success"
                          : "border-hairline bg-background/70 text-muted-foreground group-hover:border-primary/35 group-hover:bg-card group-hover:text-foreground"
                    )}
                    aria-hidden="true"
                  >
                    {isComplete ? <Check aria-hidden="true" /> : tab.icon ?? <span className="h-1.5 w-1.5 rounded-full bg-current" />}
                  </span>
                  <span className="min-w-0 truncate text-[length:var(--type-body-sm)] leading-tight">{typeof tab.label === "string" ? titleCaseLabel(tab.label) : tab.label}</span>
                  {index < tabs.length - 1 ? (
                    <ArrowRight
                      aria-hidden="true"
                      className={cn(
                        "ml-auto h-3.5 w-3.5 shrink-0 transition-colors",
                        connectorComplete ? "text-success motion-safe:animate-pulse" : isActive ? "text-primary" : "text-muted-foreground/35"
                      )}
                    />
                  ) : isActive ? (
                    <span aria-hidden="true" className="ml-auto h-1.5 w-1.5 shrink-0 rounded-full bg-primary shadow-[0_0_8px_hsl(var(--primary)/0.75)]" />
                  ) : null}
                </NavLink>
              );
            })}
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

function isTabActive(pathname: string, tab: ToolbarTab) {
  return tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`);
}
