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
    proxyClientMaxBodySize: '100mb',
  },
  
  // ✅ Proxy: legge variabili al RUNTIME (non al build-time)
  async rewrites() {
    // Legge da variabile d'ambiente al runtime
    const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';

    console.log('🔧 [next.config] Proxy API →', apiUrl);

    return {
      // afterFiles: applicati DOPO aver controllato pages/api routes
      // Quindi uploadoutagezip e uploadconsentzip (in pages/api) hanno priorita'
      afterFiles: [
        {
          source: '/api/:path*',
          destination: `${apiUrl}/api/:path*`,
        },
      ],
    };
  },

  // ✅ Timeout per upload grandi (es. ZIP files)
  serverExternalPackages: [],

  async headers() {
    return [
      {
        source: '/api/:path*',
        headers: [
          { key: 'Connection', value: 'keep-alive' },
        ],
      },
    ];
  },
};

export default nextConfig;