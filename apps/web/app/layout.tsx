import type { ReactNode } from "react";
export const metadata = {
  title: "ContextSpeak — vocabulary library",
  description: "Free local translation and a personal vocabulary library.",
};
export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      {/* Extensions such as Grammarly can add body attributes before hydration. */}
      <body suppressHydrationWarning>{children}</body>
    </html>
  );
}
