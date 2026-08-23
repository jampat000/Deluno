import * as React from "react";
import { Check, Plus } from "lucide-react";
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
    <div className={cn("flex min-h-[var(--toolbar-height)] items-center justify-between gap-[var(--grid-gap)]", className)}>
      {tabs?.length ? (
        <nav
          aria-label="Sections"
          className="relative isolate no-scrollbar flex h-[var(--toolbar-height)] min-w-0 flex-1 items-center overflow-x-auto border-y border-hairline/80 bg-gradient-to-r from-primary/[0.025] via-card/25 to-primary/[0.025] dark:border-white/[0.08] dark:via-white/[0.02]"
        >
          <span aria-hidden="true" className="pointer-events-none absolute left-0 top-1/2 h-px w-[18%] bg-gradient-to-r from-transparent to-foreground/[0.08]" />
          <span aria-hidden="true" className="pointer-events-none absolute right-0 top-1/2 h-px w-[18%] bg-gradient-to-l from-transparent to-foreground/[0.08]" />
          <span
            aria-hidden="true"
            className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-primary/20 to-transparent"
          />
          <div className="relative mx-auto grid h-full min-w-max grid-flow-col auto-cols-[clamp(10.5rem,13vw,12.25rem)] items-stretch">
            {tabs.map((tab) => {
              const isActive = isTabActive(location.pathname, tab);
              const isComplete = tab.status === "complete";

              return (
                <NavLink
                  key={tab.to}
                  to={tab.to}
                  end={tab.end}
                  className={cn(
                    "group relative flex h-full items-center justify-center gap-2 border-x border-t border-transparent px-3 text-center transition-[background-color,border-color,color,box-shadow,transform] duration-200",
                    "focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
                    isActive
                      ? "border-primary/20 bg-gradient-to-b from-primary/[0.14] via-primary/[0.05] to-transparent font-semibold text-foreground shadow-[0_-10px_24px_hsl(var(--primary)/0.08)] after:absolute after:inset-x-4 after:bottom-[-1px] after:h-0.5 after:rounded-full after:bg-primary"
                      : isComplete
                        ? "text-foreground/80 hover:bg-success/[0.05]"
                        : "text-muted-foreground hover:bg-card/60 hover:text-foreground"
                  )}
                >
                  <span
                    className={cn(
                      "relative z-10 flex h-5 w-5 shrink-0 items-center justify-center transition-[color,transform,filter] duration-200 group-hover:-translate-y-px [&>svg]:h-4 [&>svg]:w-4",
                      isActive
                        ? "text-primary drop-shadow-[0_0_8px_hsl(var(--primary)/0.55)]"
                        : isComplete
                          ? "text-success"
                          : "text-muted-foreground group-hover:text-foreground"
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
      <div className="flex w-[clamp(15rem,28vw,26rem)] shrink-0 items-center justify-end gap-2">
        {actions}
      </div>
    </div>
  );
}

function isTabActive(pathname: string, tab: ToolbarTab) {
  return tab.end ? pathname === tab.to : pathname === tab.to || pathname.startsWith(`${tab.to}/`);
}
