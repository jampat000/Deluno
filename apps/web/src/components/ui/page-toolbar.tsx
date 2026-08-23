import * as React from "react";
import { Check } from "lucide-react";
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

  return (
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)]", className)}>
      {tabs?.length ? (
        <nav
          aria-label="Sections"
          className="relative isolate no-scrollbar flex h-[var(--toolbar-height)] min-w-0 flex-1 items-center overflow-x-auto rounded-xl border border-hairline/80 bg-card/45 px-1.5 shadow-[0_12px_28px_hsl(var(--foreground)/0.04)] dark:border-white/[0.08] dark:bg-white/[0.02]"
        >
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-8 bottom-0 h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent"
          />
          <div className="relative mx-auto flex h-full min-w-max items-center gap-1">
            {tabs.map((tab) => {
              const isActive = isTabActive(location.pathname, tab);
              const isComplete = tab.status === "complete";

              return (
                <NavLink
                  key={tab.to}
                  to={tab.to}
                  end={tab.end}
                  className={cn(
                    "group relative flex h-[calc(var(--toolbar-height)-0.5rem)] shrink-0 items-center gap-2 rounded-lg border border-transparent px-3.5 text-left transition-[background-color,border-color,color,box-shadow,transform] duration-200",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "border-primary/20 bg-primary/[0.12] font-semibold text-foreground shadow-[0_4px_14px_hsl(var(--primary)/0.08)] after:absolute after:inset-x-3 after:bottom-0.5 after:h-0.5 after:rounded-full after:bg-primary"
                      : isComplete
                        ? "bg-success/[0.05] text-foreground hover:bg-success/[0.09]"
                        : "text-muted-foreground hover:bg-card/70 hover:text-foreground"
                  )}
                >
                  <span
                    className={cn(
                      "relative z-10 flex h-6 w-6 shrink-0 items-center justify-center rounded-md border transition-[background-color,border-color,color,transform,box-shadow] duration-200 group-hover:-translate-y-px [&>svg]:h-3.5 [&>svg]:w-3.5",
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
                  <span className="whitespace-nowrap text-[length:var(--type-body-sm)] leading-tight">{typeof tab.label === "string" ? titleCaseLabel(tab.label) : tab.label}</span>
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
