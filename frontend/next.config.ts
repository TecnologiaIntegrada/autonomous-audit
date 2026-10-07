import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  images: {
    remotePatterns: [
      {
        protocol: "https",
        hostname: "lh3.googleusercontent.com",
      },
    ],
  },
  async redirects() {
    return [
      { source: "/compras/nova", destination: "/recibos/novo", permanent: false },
      { source: "/estoque", destination: "/recibos", permanent: false },
    ];
  },
};

export default nextConfig;
