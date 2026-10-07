"use client";

import { useEffect, useRef, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ApiError, mensagemUsuario } from "@/lib/api";
import {
  codigoCurto,
  CONFIRMA_EXCLUSAO_RECIBO,
  jsonExtracao,
  mensagemExtracao,
  reciboComFalha,
  reciboEmProcessamento,
  recibosApi,
  rotuloDanfe,
  rotuloContraparte,
  rotuloLayoutNota,
  rotuloParteNota,
  rotuloTipoDocumento,
  rotuloTipoItem,
  statusReciboLabel,
  type ReciboDetalhe,
} from "@/lib/recibos";

export default function ReciboDetalhePage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = params.id;
  const [recibo, setRecibo] = useState<ReciboDetalhe | null>(null);
  const [preview, setPreview] = useState("");
  const [erro, setErro] = useState("");
  const [segundos, setSegundos] = useState(0);
  const [aba, setAba] = useState<"dados" | "itens" | "nfe" | "documento">("dados");
  const blobRef = useRef<string>("");

  async function carregarDetalhe() {
    const detalhe = await recibosApi.obter(id);
    setRecibo(detalhe);
    return detalhe;
  }

  async function carregarCapa() {
    if (blobRef.current) {
      return;
    }
    const blob = await recibosApi.arquivoBlob(id);
    const url = URL.createObjectURL(blob);
    blobRef.current = url;
    setPreview(url);
  }

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        await carregarDetalhe();
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível abrir o recibo.");
        }
      }
    })();
    return () => {
      cancelado = true;
      if (blobRef.current) {
        URL.revokeObjectURL(blobRef.current);
        blobRef.current = "";
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    if (!recibo) {
      return;
    }
    void carregarCapa().catch(() => undefined);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recibo?.id]);

  useEffect(() => {
    if (!recibo || !reciboEmProcessamento(recibo.status)) {
      return;
    }
    const timer = window.setInterval(() => {
      void carregarDetalhe()
        .then((detalhe) => {
          if (!reciboEmProcessamento(detalhe.status)) {
            void carregarCapa().catch(() => undefined);
          }
        })
        .catch(() => undefined);
    }, 2000);
    return () => window.clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recibo?.status, id]);

  useEffect(() => {
    if (!recibo || !reciboEmProcessamento(recibo.status)) {
      return;
    }
    const inicio = Date.parse(recibo.dataEnvio);
    const tick = () => setSegundos(Math.max(0, Math.floor((Date.now() - inicio) / 1000)));
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, [recibo]);

  useEffect(() => {
    if (preview || !recibo) {
      return;
    }
    const timer = window.setInterval(() => {
      void carregarCapa().catch(() => undefined);
    }, 8000);
    return () => window.clearInterval(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [preview, recibo?.id]);

  async function remover() {
    if (!window.confirm(CONFIRMA_EXCLUSAO_RECIBO)) {
      return;
    }
    setErro("");
    try {
      await recibosApi.excluir(id);
      router.push("/recibos");
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível excluir o recibo.");
    }
  }

  if (!recibo && !erro) {
    return (
      <AppShell titulo="Recibo">
        <p>Carregando recibo…</p>
      </AppShell>
    );
  }

  if (!recibo) {
    return (
      <AppShell titulo="Recibo">
        <p className="feedback" role="alert">
          {erro}
        </p>
      </AppShell>
    );
  }

  const processando = reciboEmProcessamento(recibo.status);
  const extraido = jsonExtracao(recibo) != null;

  return (
    <AppShell titulo={`${rotuloTipoDocumento(recibo.tipoDocumento)} ${codigoCurto(recibo.codigo ?? recibo.id)}`}>
      <p>
        Status: <strong>{statusReciboLabel(recibo.status)}</strong>
        {" · "}
        {rotuloTipoDocumento(recibo.tipoDocumento)}
        {" · "}
        {rotuloTipoItem(recibo.tipoItem)}
        {recibo.ultimoErro ? ` — ${mensagemUsuario(recibo.ultimoErro)}` : ""}
      </p>
      {processando ? (
        <p className="muted">
          Extração dos dados em andamento ({formatarDuracao(segundos)}). Cadastros e itens
          aparecem assim que a leitura do documento terminar.
        </p>
      ) : null}
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      <p className="toolbar">
        {reciboComFalha(recibo.status) ? (
          <button
            className="primary-button"
            type="button"
            onClick={() => void recibosApi.reprocessar(id).then(carregarDetalhe)}
          >
            Reprocessar
          </button>
        ) : null}
        <button className="danger-button" type="button" onClick={() => void remover()}>
          Excluir
        </button>
        {!processando && extraido && !recibo.fornecedorNome && recibo.itens.length === 0 ? (
          <button
            className="primary-button"
            type="button"
            onClick={() => void recibosApi.reprocessar(id).then(carregarDetalhe)}
          >
            Relançar cadastros e itens
          </button>
        ) : null}
      </p>

      <div className="tabs" role="tablist" aria-label="Detalhe do recibo">
        <button
          className={aba === "dados" ? "tab is-active" : "tab"}
          type="button"
          role="tab"
          id="recibo-tab-dados"
          aria-controls="recibo-painel-dados"
          aria-selected={aba === "dados"}
          onClick={() => setAba("dados")}
        >
          Dados extraídos
        </button>
        <button
          className={aba === "itens" ? "tab is-active" : "tab"}
          type="button"
          role="tab"
          id="recibo-tab-itens"
          aria-controls="recibo-painel-itens"
          aria-selected={aba === "itens"}
          onClick={() => setAba("itens")}
        >
          Itens lançados
        </button>
        {recibo.tipoDocumento === "nota_fiscal" ? (
          <button
            className={aba === "nfe" ? "tab is-active" : "tab"}
            type="button"
            role="tab"
            id="recibo-tab-nfe"
            aria-controls="recibo-painel-nfe"
            aria-selected={aba === "nfe"}
            onClick={() => setAba("nfe")}
          >
            Dados da nota
          </button>
        ) : null}
        <button
          className={aba === "documento" ? "tab is-active" : "tab"}
          type="button"
          role="tab"
          id="recibo-tab-documento"
          aria-controls="recibo-painel-documento"
          aria-selected={aba === "documento"}
          onClick={() => setAba("documento")}
        >
          Documento
        </button>
      </div>

      <section
        className="panel-block"
        role="tabpanel"
        id="recibo-painel-dados"
        aria-labelledby="recibo-tab-dados"
        hidden={aba !== "dados"}
      >
        <p>Documento: {rotuloTipoDocumento(recibo.tipoDocumento)}</p>
        <p>Produto ou serviço: {rotuloTipoItem(recibo.tipoItem)}</p>
        <p>
          {rotuloContraparte(recibo.tipoDocumento, recibo.tipoItem)}: {recibo.fornecedorNome ?? "—"}
        </p>
        <p>Número: {recibo.numeroRecibo ?? "—"}</p>
        {recibo.tipoDocumento === "nota_fiscal" ? (
          <>
            {recibo.tipoItem === "servico" ? null : <p>DANFE: {rotuloDanfe(recibo.danfeTipo)}</p>}
            <p>Série: {recibo.serie ?? "—"}</p>
            <p>Chave de acesso: {recibo.chaveAcesso ?? "—"}</p>
            <p>Natureza: {recibo.naturezaOperacao ?? "—"}</p>
          </>
        ) : null}
        <p>
          Total:{" "}
          {recibo.total != null
            ? recibo.total.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
            : "—"}
        </p>
        <p>
          <a href={`/compras/${recibo.id}`}>Ver compra lançada</a>
          {" · "}
          <a href={`/fornecedores?reciboId=${recibo.id}`}>
            {rotuloContraparte(recibo.tipoDocumento, recibo.tipoItem)} deste documento
          </a>
          {" · "}
          <a href={`/produtos?reciboId=${recibo.id}`}>Produtos/serviços deste documento</a>
        </p>
        <pre className="json-block recibo-json">{mensagemExtracao(recibo.status, jsonExtracao(recibo))}</pre>
      </section>

      <section
        className="panel-block"
        role="tabpanel"
        id="recibo-painel-itens"
        aria-labelledby="recibo-tab-itens"
        hidden={aba !== "itens"}
      >
        {recibo.itens.length > 0 ? (
          <table className="data-table">
            <thead>
              <tr>
                <th>Tipo</th>
                <th>Descrição</th>
                <th>Qtd</th>
                <th>Total</th>
                <th>Cadastro</th>
              </tr>
            </thead>
            <tbody>
              {recibo.itens.map((item) => (
                <tr key={item.id}>
                  <td>{rotuloTipoItem(item.tipoItem)}</td>
                  <td>{item.descricaoOriginal}</td>
                  <td>{item.quantidade}</td>
                  <td>
                    {item.total != null
                      ? item.total.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
                      : "—"}
                  </td>
                  <td>{item.servicoNome ?? item.produtoNome ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : (
          <p className="muted">Nenhum item lançado ainda.</p>
        )}
      </section>

      <section
        className="panel-block"
        role="tabpanel"
        id="recibo-painel-nfe"
        aria-labelledby="recibo-tab-nfe"
        hidden={aba !== "nfe"}
      >
        <h2>{rotuloLayoutNota(recibo.tipoDocumento, recibo.tipoItem)}</h2>
        <p>Tipo: {rotuloDanfe(recibo.danfeTipo)}</p>
        <p>Número: {recibo.numeroRecibo ?? "—"}</p>
        <p>
          Série: {recibo.serie ?? "—"} · Folha: {recibo.folha ?? "—"}
        </p>
        <p>Chave de acesso: {recibo.chaveAcesso ?? "—"}</p>
        <p>Código de barras: {recibo.codigoBarras ?? "—"}</p>
        <p>Protocolo: {recibo.protocoloAutorizacao ?? "—"}</p>
        <p>Natureza: {recibo.naturezaOperacao ?? "—"}</p>
        <p>IE emitente: {recibo.inscricaoEstadual ?? "—"}</p>
        <h2>{rotuloParteNota(recibo.tipoDocumento, recibo.tipoItem)}</h2>
        <p>Nome: {recibo.nfeDestinatario?.nomeRazaoSocial ?? "—"}</p>
        <p>CPF/CNPJ: {recibo.nfeDestinatario?.cpfCnpj ?? "—"}</p>
        <p>Endereço: {recibo.nfeDestinatario?.endereco ?? "—"}</p>
        <p>
          {recibo.nfeDestinatario?.bairro ?? "—"} · {recibo.nfeDestinatario?.municipio ?? "—"} /{" "}
          {recibo.nfeDestinatario?.uf ?? "—"}
        </p>
        <h2>Cálculo do imposto</h2>
        <p>
          Total produtos:{" "}
          {recibo.nfeImposto?.valorTotalProdutos != null
            ? recibo.nfeImposto.valorTotalProdutos.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
            : "—"}
        </p>
        <p>
          Total da nota:{" "}
          {recibo.nfeImposto?.valorTotalNota != null
            ? recibo.nfeImposto.valorTotalNota.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
            : "—"}
        </p>
        <h2>Transportador</h2>
        <p>Nome: {recibo.nfeTransportador?.nomeRazaoSocial ?? "—"}</p>
        <p>Frete por conta: {recibo.nfeTransportador?.fretePorConta ?? "—"}</p>
        <h2>Dados adicionais</h2>
        <p>{recibo.nfeAdicionais?.informacoesComplementares ?? "—"}</p>
      </section>

      <section
        className="panel-block tab-panel-documento"
        role="tabpanel"
        id="recibo-painel-documento"
        aria-labelledby="recibo-tab-documento"
        hidden={aba !== "documento"}
      >
        {preview ? (
          <iframe className="doc-frame doc-frame-full" title="Documento do recibo" src={preview} />
        ) : (
          <p className="muted">Prévia indisponível até que o arquivo seja processado...</p>
        )}
      </section>
    </AppShell>
  );
}

function formatarDuracao(segundos: number) {
  const min = Math.floor(segundos / 60);
  const seg = segundos % 60;
  return `${min}:${String(seg).padStart(2, "0")}`;
}
