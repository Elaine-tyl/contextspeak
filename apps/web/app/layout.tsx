import "./brand.css";
import "./motion.css";
import type { ReactNode } from "react";
import { AppNav } from "./AppNav";
import { SiteMotion } from "./SiteMotion";
export const metadata = {
  title: "ContextSpeak — vocabulary library",
  description: "Free local translation and a personal vocabulary library.",
};
export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      {/* Extensions such as Grammarly can add body attributes before hydration. */}
      <body suppressHydrationWarning>
        <SiteMotion>
          <AppNav />
          <div className="app-main">{children}</div>
        </SiteMotion>
      </body>
    </html>
  );
}
