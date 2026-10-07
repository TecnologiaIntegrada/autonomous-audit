import { getServerSession } from "next-auth";
import { redirect } from "next/navigation";
import { ContinuarGoogleClient } from "@/app/auth/continuar/continuar-google";
import { authOptions } from "@/lib/auth";

export default async function ContinuarGooglePage() {
  const session = await getServerSession(authOptions);
  if (!session?.idToken) {
    redirect("/");
  }

  return <ContinuarGoogleClient idToken={session.idToken} />;
}
