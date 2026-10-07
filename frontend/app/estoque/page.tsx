"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function EstoqueRedirect() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/recibos");
  }, [router]);
  return null;
}
