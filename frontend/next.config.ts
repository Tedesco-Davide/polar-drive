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
  
  // Proxy: legge variabili a runtime (non a build-time)
  async rewrites() {

    const apiUrl = process.env.API_BACKEND_URL || 'http://host.docker.internal:8080';

    console.log('🔧 [next.config] Proxy API →', apiUrl);

    return {
      // Queste routes vengono gestite prima dei file in pages/api
      afterFiles: [
        // Escludi routes che hanno handler custom in pages/api
        {
          source: '/api/:path*',
          has: [
            {
              type: 'header',
              key: 'x-use-proxy',
              value: 'true',
            },
          ],
          destination: `${apiUrl}/api/:path*`,
        },
      ],
      // fallback: applicati solo se nessun file pages/api corrisponde
      fallback: [
        {
          source: '/api/:path*',
          destination: `${apiUrl}/api/:path*`,
        },
      ],
    };
  },

  // Timeout per upload grandi
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