"use client";

import { encerrarSessao } from "@/lib/sessao";

export function SignOutButton() {
  return (
    <button type="button" className="ghost-btn" onClick={() => void encerrarSessao()}>
      Sair
    </button>
  );
}
