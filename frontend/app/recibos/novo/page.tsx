"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ApiError } from "@/lib/api";
import {
  RECIBO_ACCEPT,
  RECIBO_FORMATOS,
  recibosApi,
  validarArquivoRecibo,
  type CapturaCriada,
  type CapturaStatus,
  type ReciboAnexo,
  type ReciboLista,
} from "@/lib/recibos";

export default function NovoReciboPage() {
  const router = useRouter();
  const [locais, setLocais] = useState<File[]>([]);
  const [recibo, setRecibo] = useState<ReciboLista | null>(null);
  const [anexos, setAnexos] = useState<ReciboAnexo[]>([]);
  const [qr, setQr] = useState<CapturaCriada | null>(null);
  const [captura, setCaptura] = useState<CapturaStatus | null>(null);
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [cota, setCota] = useState({ usado: 0, limite: 1024 * 1024 * 1024 });
  const locaisRef = useRef<File[]>([]);
  const reciboRef = useRef<ReciboLista | null>(null);
  const enviandoRef = useRef(false);

  locaisRef.current = locais;
  reciboRef.current = recibo;

  useEffect(() => {
    void recibosApi
      .resumo()
      .then((dados) => {
        setCota({
          usado: dados.armazenamentoUsadoBytes ?? 0,
          limite: dados.armazenamentoLimiteBytes || 1024 * 1024 * 1024,
        });
      })
      .catch(() => undefined);
    void recibosApi.limparRascunhosAnteriores().catch(() => undefined);
  }, []);

  async function enviar() {
    if (enviandoRef.current) {
      return;
    }
    enviandoRef.current = true;
    setErro("");
    setEnviando(true);
    try {
      const atual = reciboRef.current;
      const arquivos = locaisRef.current;
      if (atual) {
        for (const file of arquivos) {
          await recibosApi.anexar(atual.id, file);
        }
        await recibosApi.processar(atual.id);
        router.push(`/recibos/${atual.id}`);
        return;
      }
      if (arquivos.length === 0) {
        setErro("Selecione imagens ou um PDF de até 50 MB.");
        enviandoRef.current = false;
        return;
      }
      const criado = await recibosApi.criarComArquivos(arquivos);
      router.push(`/recibos/${criado.id}`);
    } catch (falha) {
      enviandoRef.current = false;
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível enviar o recibo.");
    } finally {
      setEnviando(false);
    }
  }

  useEffect(() => {
    if (!recibo || !qr) {
      return;
    }
    const timer = window.setInterval(async () => {
      try {
        const status = await recibosApi.capturaStatus(recibo.id);
        setCaptura(status);
        const detalhe = await recibosApi.obter(recibo.id);
        setAnexos(detalhe.anexos);
        const total = locaisRef.current.length + detalhe.anexos.length;
        if (status.concluida && total > 0 && !enviandoRef.current) {
          await enviar();
        }
      } catch {
        /* polling silencioso */
      }
    }, 2000);
    return () => window.clearInterval(timer);
  }, [recibo, qr]);

  function adicionarArquivos(files: FileList | null) {
    if (!files?.length) {
      return;
    }
    const aceitos: File[] = [];
    for (const file of Array.from(files)) {
      const invalid = validarArquivoRecibo(file);
      if (invalid) {
        setErro(invalid);
        return;
      }
      aceitos.push(file);
    }
    const totalNovo = locais.reduce((soma, file) => soma + file.size, 0) + aceitos.reduce((soma, file) => soma + file.size, 0);
    if (cota.usado + totalNovo > cota.limite) {
      setErro("Limite de 1 GB de armazenamento atingido. Exclua recibos para anexar novos arquivos.");
      return;
    }
    setErro("");
    setLocais((atual) => [...atual, ...aceitos]);
  }

  async function gerarQr() {
    setErro("");
    try {
      const criado = recibo ?? (await recibosApi.criarRascunho());
      setRecibo(criado);
      setQr(await recibosApi.captura(criado.id));
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível gerar o QR.");
    }
  }

  const totalAnexos = locais.length + anexos.length;
  const cotaEsgotada = cota.usado >= cota.limite;

  return (
    <AppShell titulo="Adicionar Recibo / NF">
      <p>
        Envie uma ou mais imagens, ou um PDF (até 50 MB cada). {RECIBO_FORMATOS} Cada imagem é
        analisada como uma compra (recibo ou nota fiscal); se o mesmo documento ocupar mais de uma
        foto, o reconhecimento agrupa essas páginas.
      </p>
      {cotaEsgotada ? (
        <p className="feedback" role="alert">
          Limite de 1 GB de armazenamento atingido. Exclua recibos para anexar novos arquivos.
        </p>
      ) : null}
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}

      <div className="split-grid">
        <section className="panel-block">
          <h2>Arquivos</h2>
          <input
            type="file"
            multiple
            accept={RECIBO_ACCEPT}
            disabled={enviando || cotaEsgotada}
            onChange={(e) => {
              adicionarArquivos(e.target.files);
              e.target.value = "";
            }}
          />
          <ul className="anexo-lista">
            {locais.map((file) => (
              <li key={`${file.name}-${file.size}-${file.lastModified}`}>
                <span>{file.name}</span>
                <button type="button" onClick={() => setLocais((atual) => atual.filter((item) => item !== file))}>
                  Remover
                </button>
              </li>
            ))}
            {anexos.map((anexo) => (
              <li key={anexo.id}>
                <span>
                  {anexo.ordem}. {anexo.nomeArquivo}
                </span>
              </li>
            ))}
          </ul>
          <button className="primary-button" type="button" disabled={enviando || totalAnexos === 0 || cotaEsgotada} onClick={() => void enviar()}>
            {enviando ? "Enviando…" : "Enviar recibo"}
          </button>
        </section>

        <section className="panel-block">
          <h2>Captura no celular</h2>
          {qr ? (
            <>
              <img className="qr-img" src={`data:image/png;base64,${qr.qrPngBase64}`} alt="QR para captura do recibo" />
              <p className="muted">
                {captura?.concluida
                  ? "Captura concluída. Enviando recibo…"
                  : captura?.consumida
                    ? "Celular conectado."
                    : "Aguardando o celular."}{" "}
                {captura ? `${captura.anexos} foto(s) recebida(s).` : null}
              </p>
            </>
          ) : (
            <button className="ghost-button dark" type="button" disabled={cotaEsgotada} onClick={() => void gerarQr()}>
              Gerar QR
            </button>
          )}
        </section>
      </div>
    </AppShell>
  );
}
