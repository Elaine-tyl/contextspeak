"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { MouseEvent, ReactNode } from "react";
import { updateQuery, usePageQuery } from "../src/page-query";

type Section =
  "library" | "reviews" | "practice" | "games" | "progress" | "account";

const icon = (path: ReactNode) => (
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.8"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    {path}
  </svg>
);

// Review is orange, listening/speaking is blue, everything else uses the main green.
const tones: Record<Section, "brand" | "speak" | "review"> = {
  library: "brand",
  reviews: "review",
  practice: "speak",
  games: "brand",
  progress: "brand",
  account: "brand",
};

const items: {
  id: Section;
  label: string;
  href: string;
  view?: string;
  icon: ReactNode;
}[] = [
  {
    id: "library",
    label: "Library",
    href: "/library",
    view: "",
    icon: icon(
      <path d="M4 5.5A1.5 1.5 0 0 1 5.5 4H11v16H5.5A1.5 1.5 0 0 1 4 18.5zM13 4h5.5A1.5 1.5 0 0 1 20 5.5v13a1.5 1.5 0 0 1-1.5 1.5H13z" />,
    ),
  },
  {
    id: "reviews",
    label: "Review",
    href: "/library?view=reviews",
    view: "reviews",
    icon: icon(
      <>
        <path d="M20 12a8 8 0 1 1-2.34-5.66" />
        <path d="M20 4v4h-4" />
      </>,
    ),
  },
  {
    id: "practice",
    label: "Listen & speak",
    href: "/library?view=practice",
    view: "practice",
    icon: icon(
      <>
        <rect x="9" y="3" width="6" height="11" rx="3" />
        <path d="M5 11a7 7 0 0 0 14 0M12 18v3" />
      </>,
    ),
  },
  {
    id: "games",
    label: "Games",
    href: "/games",
    icon: icon(
      <>
        <rect x="3" y="7" width="18" height="11" rx="4" />
        <path d="M8 11v3M6.5 12.5h3M15.5 12h.01M17.5 14h.01" />
      </>,
    ),
  },
  {
    id: "progress",
    label: "Progress",
    href: "/progress",
    icon: icon(<path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />),
  },
  {
    id: "account",
    label: "Account",
    href: "/account",
    icon: icon(
      <>
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21a8 8 0 0 1 16 0" />
      </>,
    ),
  },
];

function currentSection(pathname: string, view: string | null): Section | null {
  if (pathname === "/library") {
    if (view === "reviews") return "reviews";
    if (view === "practice") return "practice";
    return "library";
  }
  if (pathname.startsWith("/library/")) return "library";
  if (pathname.startsWith("/practice")) return "practice";
  if (pathname.startsWith("/games") || pathname.startsWith("/world"))
    return "games";
  if (pathname.startsWith("/progress")) return "progress";
  if (pathname.startsWith("/account") || pathname.startsWith("/pair"))
    return "account";
  return null;
}

export function AppNav() {
  const pathname = usePathname() ?? "";
  const { params } = usePageQuery();
  const active = currentSection(pathname, params.get("view"));
  // The library switches views through its own query store, so stay on the page.
  function open(event: MouseEvent, view: string | undefined) {
    // Preserve native new-tab/window behavior for modified link clicks.
    if (
      view === undefined ||
      pathname !== "/library" ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    )
      return;
    event.preventDefault();
    updateQuery({ view }, true);
    window.scrollTo(0, 0);
  }
  return (
    <nav className="app-nav" aria-label="Main">
      <Link
        className="app-brand"
        href="/library"
        aria-label="ContextSpeak home"
      >
        <span className="app-brand-mark" aria-hidden="true">
          C
        </span>
        ContextSpeak
      </Link>
      <ul className="app-nav-list">
        {items.map((item) => (
          <li key={item.id}>
            <Link
              className={`app-nav-link tone-${tones[item.id]}`}
              href={item.href}
              aria-current={active === item.id ? "page" : undefined}
              onClick={(event) => open(event, item.view)}
            >
              {item.icon}
              {item.label}
            </Link>
          </li>
        ))}
      </ul>
      <p className="app-nav-footer">
        Look up words while you read. Review them here when they are due.
      </p>
    </nav>
  );
}
