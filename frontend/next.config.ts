import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  i18n: {
    defaultLocale: "it",
    locales: ["it", "en"],
    localeDetection: false,
  },
};

export default nextConfig;
