"""Capture and structurally audit every primary Deluno screen in a disposable app instance."""

from __future__ import annotations

import argparse
import json
from datetime import datetime
from pathlib import Path

from playwright.sync_api import sync_playwright


ROUTES = [
    ("dashboard", "/"),
    ("setup-guide", "/setup-guide"),
    ("movies", "/movies"),
    ("tv", "/tv"),
    ("tv-episodes", "/tv/episodes"),
    ("calendar", "/calendar"),
    ("activity", "/activity"),
    ("queue", "/queue"),
    ("connections", "/indexers"),
    ("indexers", "/indexers/indexers"),
    ("download-clients", "/indexers/download-clients"),
    ("library-routing", "/indexers/library-routing"),
    ("automation", "/search-cycles"),
    ("settings-overview", "/settings"),
    ("files-processing", "/settings/media-management"),
    ("destinations", "/settings/destination-rules"),
    ("media-plans", "/settings/policy-sets"),
    ("quality", "/settings/quality"),
    ("release-preferences", "/settings/custom-formats"),
    ("import-lists", "/settings/lists"),
    ("metadata", "/settings/metadata"),
    ("tags", "/settings/tags"),
    ("general", "/settings/general"),
    ("notifications", "/settings/notifications"),
    ("interface", "/settings/ui"),
    ("migration", "/settings/migration"),
    ("system", "/system"),
    ("system-api", "/system/api"),
    ("system-docs", "/system/docs"),
]


def settle(page) -> None:
    try:
        page.wait_for_load_state("networkidle", timeout=12_000)
    except Exception:
        # SignalR and polling can keep a healthy page technically busy.
        pass
    page.wait_for_timeout(400)


def bootstrap(page, base_url: str) -> None:
    page.goto(f"{base_url}/setup", wait_until="domcontentloaded")
    settle(page)
    if page.get_by_label("Display name").count():
        page.get_by_label("Display name").fill("Visual Audit")
        page.get_by_label("Username").fill("visual-audit")
        page.get_by_label("Password", exact=True).fill("visual-audit-password-123")
        page.get_by_label("Confirm password").fill("visual-audit-password-123")
        page.get_by_role("button", name="Create account").click()
        settle(page)

    # A reused audit data directory may already contain the disposable user.
    # Authenticate it rather than silently screenshotting the sign-in screen.
    if page.get_by_role("heading", name="Sign in to Deluno").count():
        page.get_by_label("Username").fill("visual-audit")
        page.get_by_label("Password", exact=True).fill("visual-audit-password-123")
        page.get_by_role("button", name="Sign in").click()
        settle(page)

    if page.get_by_role("heading", name="Sign in to Deluno").count():
        raise RuntimeError("Visual audit could not authenticate its disposable user.")


def measure(page) -> dict:
    return page.evaluate(
        """() => {
          const rect = (element) => {
            const box = element.getBoundingClientRect();
            return { top: Math.round(box.top), bottom: Math.round(box.bottom), height: Math.round(box.height) };
          };
          const main = document.querySelector('main');
          const sections = [...document.querySelectorAll('main > * > section, main > section')]
            .slice(0, 12)
            .map((element) => ({ tag: element.tagName, ...rect(element), text: element.textContent.trim().slice(0, 80) }));
          const overflow = document.documentElement.scrollWidth - window.innerWidth;
          const errors = [...document.querySelectorAll('body *')]
            .filter((element) => /Unexpected Application Error|This area could not load/i.test(element.textContent || ''))
            .map((element) => element.textContent.trim().slice(0, 140));
          return {
            viewport: { width: window.innerWidth, height: window.innerHeight },
            overflow,
            main: main ? rect(main) : null,
            sections,
            errors: [...new Set(errors)]
          };
        }"""
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    report: list[dict] = []

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": 1600, "height": 1000}, device_scale_factor=1)
        bootstrap(page, args.base_url.rstrip("/"))

        for name, route in ROUTES:
            page.goto(f"{args.base_url.rstrip('/')}{route}", wait_until="domcontentloaded")
            settle(page)
            page.screenshot(path=str(output / f"{name}.png"), full_page=True)
            report.append({"route": route, "name": name, **measure(page)})

        browser.close()

    (output / "report.json").write_text(
        json.dumps({"createdUtc": datetime.utcnow().isoformat() + "Z", "routes": report}, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
