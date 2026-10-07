"use client";

import { signIn } from "next-auth/react";

export function GoogleSignInButton() {
  return (
    <button
      type="button"
      className="google-btn"
      onClick={() => signIn("google", { callbackUrl: "/auth/continuar" })}
    >
      Continuar com Google
    </button>
  );
}
