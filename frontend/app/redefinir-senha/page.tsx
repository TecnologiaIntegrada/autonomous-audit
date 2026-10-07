"use client";

import { FormEvent, Suspense, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { AuthShell } from "@/components/AuthShell";
import { VoltarAoLoginButton } from "@/components/VoltarAoLoginButton";
import { apiJson, guardarJwt, type LoginApiResponse } from "@/lib/api";
import { alertaEmail } from "@/lib/contato";

function RedefinirSenhaForm() {
  const router = useRouter();
  const params = useSearchParams();
  const [email, setEmail] = useState(params.get("email") ?? "");
  const [codigo, setCodigo] = useState("");
  const [senha, setSenha] = useState("");
  const [senhaConfirmacao, setSenhaConfirmacao] = useState("");
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function enviar(event: FormEvent) {
    event.preventDefault();
    setErro("");
    const alerta = alertaEmail(email);
    if (alerta) {
      setErro(alerta);
      return;
    }
    if (senha !== senhaConfirmacao) {
      setErro("A confirmação da senha não confere.");
      return;
    }
    setEnviando(true);
    try {
      const resposta = await apiJson<LoginApiResponse>(
        "/v1/auth/redefinir-senha",
        {
          method: "POST",
          body: JSON.stringify({ email, codigo, senha, senhaConfirmacao }),
        },
        { skipAuth: true },
      );
      if (!resposta.token) {
        setErro("Senha alterada, mas a API não devolveu o token.");
        return;
      }
      guardarJwt(resposta.token);
      router.replace("/dashboard");
    } catch (falha) {
      setErro(falha instanceof Error ? falha.message : "Não foi possível redefinir a senha.");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthShell reiniciarAoInicio>
      <span className="section-kicker">NOVA SENHA</span>
      <h2>Redefinir senha</h2>
      <form noValidate onSubmit={enviar}>
        <div className="field">
          <label htmlFor="email">E-mail</label>
          <div className="input-wrap">
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                setErro("");
              }}
              onBlur={() => {
                const alerta = alertaEmail(email);
                if (alerta) setErro(alerta);
              }}
              aria-invalid={erro ? true : undefined}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="codigo">Código de 6 dígitos</label>
          <div className="input-wrap">
            <input
              id="codigo"
              inputMode="numeric"
              maxLength={6}
              value={codigo}
              onChange={(e) => setCodigo(e.target.value)}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="senha">Nova senha</label>
          <div className="input-wrap">
            <input
              id="senha"
              type="password"
              minLength={8}
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="senhaConfirmacao">Repita a senha</label>
          <div className="input-wrap">
            <input
              id="senhaConfirmacao"
              type="password"
              minLength={8}
              value={senhaConfirmacao}
              onChange={(e) => setSenhaConfirmacao(e.target.value)}
              required
            />
          </div>
        </div>
        {erro ? (
          <p className="feedback" role="alert">
            {erro}
          </p>
        ) : null}
        <button className="submit" type="submit" disabled={enviando}>
          <span>{enviando ? "Salvando…" : "Salvar senha e entrar"}</span>
          <span aria-hidden="true">↗</span>
        </button>
      </form>
      <p>
        <a className="text-button" href="/recuperar-senha">
          Pedir novo código
        </a>
        {" · "}
        <VoltarAoLoginButton />
      </p>
    </AuthShell>
  );
}

export default function RedefinirSenhaPage() {
  return (
    <Suspense fallback={<main className="shell" />}>
      <RedefinirSenhaForm />
    </Suspense>
  );
}
