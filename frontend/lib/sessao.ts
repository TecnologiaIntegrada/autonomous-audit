import { signOut } from "next-auth/react";
import { limparJwt, limparRegistroGoogle } from "@/lib/api";

function apagarCookie(nome: string) {
  const expirado = "Thu, 01 Jan 1970 00:00:00 GMT";
  const host = window.location.hostname;
  const sufixos = [
    "path=/",
    "path=/; secure; samesite=lax",
    `path=/; domain=${host}`,
    `path=/; domain=${host}; secure; samesite=lax`,
  ];
  if (host.includes(".")) {
    sufixos.push(`path=/; domain=.${host}`);
    sufixos.push(`path=/; domain=.${host}; secure; samesite=lax`);
  }
  for (const extra of sufixos) {
    document.cookie = `${nome}=; expires=${expirado}; max-age=0; ${extra}`;
  }
}

export function limparCookiesAuth() {
  if (typeof document === "undefined") {
    return;
  }
  for (const parte of document.cookie.split(";")) {
    const nome = parte.split("=")[0]?.trim();
    if (!nome) {
      continue;
    }
    const chave = nome.toLowerCase();
    if (chave.includes("next-auth") || chave.includes("session-token")) {
      apagarCookie(nome);
    }
  }
}

export async function encerrarSessao() {
  limparJwt();
  limparRegistroGoogle();
  limparCookiesAuth();
  try {
    await Promise.race([
      signOut({ redirect: false }),
      new Promise((resolve) => window.setTimeout(resolve, 1500)),
    ]);
  } catch {
    /* a limpeza local continua mesmo se o NextAuth falhar */
  }
  limparCookiesAuth();
  window.location.replace("/?sair=1");
}
