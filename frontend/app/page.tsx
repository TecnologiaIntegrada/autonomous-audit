import { Suspense } from "react";
import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { LoginScreen } from "@/components/LoginScreen";
import { authOptions } from "@/lib/auth";

export default async function HomePage({
  searchParams,
}: {
  searchParams: Promise<{ sair?: string }>;
}) {
  const params = await searchParams;
  if (params.sair !== "1") {
    const session = await getServerSession(authOptions);
    if (session) {
      redirect("/auth/continuar");
    }
  }

  return (
    <Suspense fallback={<main className="shell" />}>
      <LoginScreen />
    </Suspense>
  );
}
