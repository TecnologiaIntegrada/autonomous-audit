"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { apiBaseUrl } from "@/lib/api";

export default function CapturaPage() {
  const params = useParams<{ token: string }>();
  const token = params.token;
  const [session, setSession] = useState("");
  const [erro, setErro] = useState("");
  const [fotos, setFotos] = useState(0);
  const [pronto, setPronto] = useState(false);
  const [abrindo, setAbrindo] = useState(true);

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        const response = await fetch(`${apiBaseUrl}/v1/recibos/captura/${token}/abrir`, { method: "POST" });
        const text = await response.text();
        if (!response.ok) {
          throw new Error(text || "QR expirado. Gere um novo no computador.");
        }
        const body = JSON.parse(text) as { sessionToken: string };
        if (!cancelado) {
          setSession(body.sessionToken);
        }
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof Error ? falha.message : "Não foi possível abrir a captura.");
        }
      } finally {
        if (!cancelado) {
          setAbrindo(false);
        }
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [token]);

  async function enviar(files: FileList | null) {
    if (!files?.length || !session) {
      return;
    }
    setErro("");
    try {
      for (const file of Array.from(files)) {
        if (file.size > 50 * 1024 * 1024) {
          throw new Error(`${file.name}: cada arquivo deve ter no máximo 50 MB.`);
        }
        const form = new FormData();
        form.append("arquivo", file);
        const response = await fetch(`${apiBaseUrl}/v1/recibos/captura/${token}/anexos`, {
          method: "POST",
          headers: { "X-Captura-Sessao": session },
          body: form,
        });
        if (!response.ok) {
          throw new Error(await response.text());
        }
        setFotos((n) => n + 1);
      }
    } catch (falha) {
      setErro(falha instanceof Error ? falha.message : "Falha ao enviar a foto.");
    }
  }

  async function concluir() {
    const response = await fetch(`${apiBaseUrl}/v1/recibos/captura/${token}/concluir`, {
      method: "POST",
      headers: { "X-Captura-Sessao": session },
    });
    if (!response.ok && response.status !== 204) {
      setErro(await response.text());
      return;
    }
    setPronto(true);
  }

  return (
    <main className="captura-page">
      <h1>Captura do recibo</h1>
      {abrindo ? <p>Abrindo sessão…</p> : null}
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      {pronto ? (
        <p>Fotos enviadas. Você já pode voltar ao computador.</p>
      ) : session ? (
        <>
          <p>{fotos} foto(s) enviada(s). Fotografe cada página na ordem.</p>
          <input type="file" accept="image/*,application/pdf" capture="environment" multiple onChange={(e) => void enviar(e.target.files)} />
          <button className="primary-button" type="button" onClick={() => void concluir()}>
            Concluir envio
          </button>
          <p className="muted">Ao concluir, o computador envia o recibo para análise automaticamente.</p>
        </>
      ) : null}
    </main>
  );
}
