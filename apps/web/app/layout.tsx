import "./brand.css";
import type { ReactNode } from "react";
import { AppNav } from "./AppNav";
export const metadata = {
  title: "ContextSpeak — vocabulary library",
  description: "Free local translation and a personal vocabulary library.",
};
export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      {/* Extensions such as Grammarly can add body attributes before hydration. */}
      <body suppressHydrationWarning>
        <div className="app-shell">
          <AppNav />
          <div className="app-main">{children}</div>
        </div>
      </body>
    </html>
  );
}
