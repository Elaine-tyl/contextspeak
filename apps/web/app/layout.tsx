import type { ReactNode } from "react";
export const metadata = {
  title: "ContextSpeak — vocabulary library",
  description: "A local fixture-backed capture-to-library development slice.",
};
export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
