import * as React from "react";
import { Plus } from "lucide-react";
import { NavLink, useLocation } from "react-router-dom";
import { titleCaseLabel } from "../../lib/title-case";
import { cn } from "../../lib/utils";
import { Button, type ButtonProps } from "./button";

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

export interface PageToolbarActionProps extends Omit<ButtonProps, "children"> {
  children: React.ReactNode;
}

/** The shared primary action treatment for collection-page toolbar rails. */
export function PageToolbarAction({ children, className, ...props }: PageToolbarActionProps) {
  return (
    <Button type="button" size="sm" className={cn("shrink-0 px-3.5", className)} {...props}>
      <Plus aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
      {children}
    </Button>
  );
}

/**
 * The first row of every collection page: 40px, section rail left, actions right.
 * The topbar already names the page, so there is no H1 here.
 */
export function PageToolbar({ tabs, left, actions, className }: PageToolbarProps) {
  const location = useLocation();

  return (
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)] border-b border-hairline/80 dark:border-white/[0.08]", className)}>
      {tabs?.length ? (
        <nav
          aria-label="Sections"
          className="no-scrollbar flex h-16 min-w-0 flex-1 items-stretch overflow-x-auto bg-transparent"
        >
          <div className="flex h-full min-w-max items-stretch gap-[calc(var(--grid-gap)*2)] px-3">
            {tabs.map((tab) => {
              const isActive = isTabActive(location.pathname, tab);
              const isComplete = tab.status === "complete";
              const tabLabel = typeof tab.label === "string" ? titleCaseLabel(tab.label) : undefined;

              return (
                <NavLink
                  key={tab.to}
                  to={tab.to}
                  end={tab.end}
                  aria-label={tabLabel ? `${tabLabel}${isComplete ? " — complete" : ""}` : undefined}
                  data-status={isComplete ? "complete" : "pending"}
                  className={cn(
                    "group relative flex h-full shrink-0 items-center px-1 pt-px text-[length:var(--type-body-sm)] font-medium leading-none transition-colors duration-200",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "font-semibold text-foreground after:absolute after:inset-x-0 after:bottom-2.5 after:h-0.5 after:bg-primary"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  <span className="whitespace-nowrap">{tabLabel ?? tab.label}</span>
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
      <div className="flex w-[clamp(15rem,28vw,26rem)] shrink-0 items-center justify-end gap-2">
        {actions}
      </div>
    </div>
  );
}

function isTabActive(pathname: string, tab: ToolbarTab) {
  return tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`);
}
