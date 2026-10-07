"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function NovaCompraRedirect() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/recibos/novo");
  }, [router]);
  return null;
}
