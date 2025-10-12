import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  i18n: {
    defaultLocale: "it",
    locales: ["it", "en"],
    localeDetection: false,
  },
  // Necessario per il Dockerfile che copia .next/standalone
  output: "standalone",
  // Salta gli errori ESLint nel build
  eslint: {
    ignoreDuringBuilds: true, 
  },
};

export default nextConfig;
