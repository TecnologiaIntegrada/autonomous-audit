"use client";

import { useState } from "react";
import { encerrarSessao } from "@/lib/sessao";

export function VoltarAoLoginButton() {
  const [saindo, setSaindo] = useState(false);

  return (
    <button
      type="button"
      className="text-button"
      disabled={saindo}
      onClick={() => {
        setSaindo(true);
        void encerrarSessao();
      }}
    >
      {saindo ? "Saindo…" : "Voltar ao login"}
    </button>
  );
}
