import type { NextConfig } from "next";
const config: NextConfig = {
  transpilePackages: ["@contextspeak/contracts"],
  devIndicators: false,
};
export default config;
