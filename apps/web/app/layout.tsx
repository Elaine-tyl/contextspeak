import Link from "next/link";
import "./brand.css";
import type { ReactNode } from "react";
export const metadata = {
  title: "ContextSpeak — vocabulary library",
  description: "Free local translation and a personal vocabulary library.",
};
export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      {/* Extensions such as Grammarly can add body attributes before hydration. */}
      <body suppressHydrationWarning>
        <header className="site-header">
          <Link href="/library" aria-label="ContextSpeak home">
            ContextSpeak
          </Link>
          <Link href="/progress" aria-label="Open learning progress">
            Progress
          </Link>
          <Link href="/account">Account</Link>
        </header>
        {children}
      </body>
    </html>
  );
}
