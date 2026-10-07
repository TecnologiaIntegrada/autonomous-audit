"use client";

import { FormEvent, useState } from "react";
import { AuthShell } from "@/components/AuthShell";
import { VoltarAoLoginButton } from "@/components/VoltarAoLoginButton";
import { alertaEmail } from "@/lib/contato";
import { apiJson } from "@/lib/api";

export default function RecuperarSenhaPage() {
  const [email, setEmail] = useState("");
  const [mensagem, setMensagem] = useState("");
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  async function enviar(event: FormEvent) {
    event.preventDefault();
    setErro("");
    setMensagem("");
    const alerta = alertaEmail(email);
    if (alerta) {
      setErro(alerta);
      return;
    }
    setEnviando(true);
    try {
      const resposta = await apiJson<{ mensagem: string }>(
        "/v1/auth/recuperar-senha",
        {
          method: "POST",
          body: JSON.stringify({ email }),
        },
        { skipAuth: true },
      );
      setMensagem(resposta.mensagem);
    } catch (falha) {
      setErro(falha instanceof Error ? falha.message : "Não foi possível enviar o e-mail.");
    } finally {
      setEnviando(false);
    }
  }

  return (
    <AuthShell reiniciarAoInicio>
      <span className="section-kicker">RECUPERAR ACESSO</span>
      <h2>Esqueceu sua senha?</h2>
      <p style={{ color: "var(--muted)", marginTop: 0 }}>
        Informe o e-mail principal. Se houver cadastro, enviamos um código de 6 dígitos válido por 15 minutos.
      </p>
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
        {erro ? (
          <p className="feedback" role="alert">
            {erro}
          </p>
        ) : null}
        {mensagem ? (
          <p className="feedback" role="status">
            {mensagem}
          </p>
        ) : null}
        <button className="submit" type="submit" disabled={enviando}>
          <span>{enviando ? "Enviando…" : "Enviar código"}</span>
          <span aria-hidden="true">↗</span>
        </button>
      </form>
      <p>
        <a className="text-button" href="/redefinir-senha">
          Já tenho o código
        </a>
        {" · "}
        <VoltarAoLoginButton />
      </p>
    </AuthShell>
  );
}
