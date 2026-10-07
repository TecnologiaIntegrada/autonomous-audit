"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { VoltarAoLoginButton } from "@/components/VoltarAoLoginButton";
import { apiJson, guardarJwt, guardarRegistroGoogle, ApiError, type GoogleAuthApiResponse } from "@/lib/api";

export function ContinuarGoogleClient({ idToken }: { idToken: string }) {
  const router = useRouter();
  const [mensagem, setMensagem] = useState("Confirmando sua conta Google…");

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        const resposta = await apiJson<GoogleAuthApiResponse>(
          "/v1/auth/google",
          {
            method: "POST",
            body: JSON.stringify({ idToken }),
          },
          { skipAuth: true },
        );
        if (cancelado) {
          return;
        }
        if (resposta.status === "pronto" && resposta.token) {
          guardarJwt(resposta.token);
          router.replace("/dashboard");
          return;
        }
        if (resposta.status === "cadastro_pendente" && resposta.registroToken && resposta.email) {
          guardarRegistroGoogle({
            registroToken: resposta.registroToken,
            email: resposta.email,
            nome: resposta.nome ?? "",
            foto: resposta.foto,
          });
          router.replace("/cadastro");
          return;
        }
        setMensagem("Não foi possível concluir o login Google.");
      } catch (erro) {
        if (erro instanceof ApiError && erro.status === 409) {
          setMensagem(
            `${erro.message} Entre com e-mail e senha, ou recupere o acesso.`,
          );
          return;
        }
        setMensagem(erro instanceof Error ? erro.message : "Falha ao falar com a API.");
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [idToken, router]);

  return (
    <main className="shell">
      <section className="login-panel" style={{ gridColumn: "1 / -1" }}>
        <div className="form-wrap">
          <span className="section-kicker">AUTONOMOUS AUDIT</span>
          <h2>Confirmando conta Google</h2>
          <p className="feedback" role="status">
            {mensagem}
          </p>
          <p>
            <VoltarAoLoginButton />
            {" · "}
            <a className="text-button" href="/recuperar-senha">
              Recuperar senha
            </a>
          </p>
        </div>
      </section>
    </main>
  );
}
