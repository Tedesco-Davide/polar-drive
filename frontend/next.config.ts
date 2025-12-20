import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactStrictMode: true,
  i18n: {
    defaultLocale: "it",
    locales: ["it", "en"],
    localeDetection: false,
  },
  output: "standalone",
  experimental: {
    optimizePackageImports: ['lucide-react', '@radix-ui/react-icons'],
  },
  
  // ✅ Proxy: legge variabili al RUNTIME (non al build-time)
  async rewrites() {
    // Legge da variabile d'ambiente al runtime
    const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';
    
    console.log('🔧 [next.config] Proxy API →', apiUrl);
    
    return [
      {
        source: '/api/:path*',
        destination: `${apiUrl}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;