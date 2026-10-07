"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { ApiError, mensagemUsuario } from "@/lib/api";
import {
  codigoCurto,
  CONFIRMA_EXCLUSAO_RECIBO,
  jsonExtracao,
  reciboComFalha,
  reciboEmProcessamento,
  recibosApi,
  rotuloTipoDocumento,
  rotuloTipoItem,
  statusReciboLabel,
  type Fornecedor,
  type ReciboLista,
} from "@/lib/recibos";

export default function RecibosPage() {
  const [lista, setLista] = useState<ReciboLista[]>([]);
  const [fornecedores, setFornecedores] = useState<Fornecedor[]>([]);
  const [edicoes, setEdicoes] = useState<Record<string, string>>({});
  const [salvandoId, setSalvandoId] = useState<string | null>(null);
  const [erro, setErro] = useState("");
  const [excluindo, setExcluindo] = useState<string | null>(null);

  async function carregar() {
    const [dados, cadastros] = await Promise.all([recibosApi.listar(), recibosApi.fornecedores()]);
    setLista(dados);
    setFornecedores([...cadastros].sort((a, b) => a.nome.localeCompare(b.nome, "pt-BR")));
  }

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        await carregar();
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível listar os recibos.");
        }
      }
    })();
    return () => {
      cancelado = true;
    };
  }, []);

  useEffect(() => {
    if (!lista.some((recibo) => reciboEmProcessamento(recibo.status))) {
      return;
    }
    const timer = window.setInterval(() => {
      void carregar().catch(() => undefined);
    }, 3000);
    return () => window.clearInterval(timer);
  }, [lista]);

  function fornecedorSelecionado(recibo: ReciboLista) {
    return edicoes[recibo.id] ?? recibo.fornecedorId ?? "";
  }

  function alterado(recibo: ReciboLista) {
    const atual = fornecedorSelecionado(recibo);
    return atual !== "" && atual !== (recibo.fornecedorId ?? "");
  }

  async function salvarFornecedor(recibo: ReciboLista) {
    const fornecedorId = fornecedorSelecionado(recibo);
    if (!fornecedorId || !alterado(recibo)) {
      return;
    }
    setErro("");
    setSalvandoId(recibo.id);
    try {
      const atualizado = await recibosApi.alterarFornecedor(recibo.id, fornecedorId);
      setLista((atual) => atual.map((item) => (item.id === recibo.id ? { ...item, ...atualizado } : item)));
      setEdicoes((atual) => {
        const proximo = { ...atual };
        delete proximo[recibo.id];
        return proximo;
      });
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível alterar o fornecedor.");
    } finally {
      setSalvandoId(null);
    }
  }

  async function remover(id: string) {
    if (!window.confirm(CONFIRMA_EXCLUSAO_RECIBO)) {
      return;
    }
    setErro("");
    setExcluindo(id);
    try {
      await recibosApi.excluir(id);
      setLista((atual) => atual.filter((recibo) => recibo.id !== id));
      const cadastros = await recibosApi.fornecedores();
      setFornecedores([...cadastros].sort((a, b) => a.nome.localeCompare(b.nome, "pt-BR")));
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível excluir o recibo.");
    } finally {
      setExcluindo(null);
    }
  }

  return (
    <AppShell titulo="Recibos e notas fiscais">
      <p>Arquivos enviados e extração dos dados. O lançamento da compra aparece no menu Compras.</p>
      <p>
        <a className="primary-button" href="/recibos/novo">
          Adicionar Recibo / NF
        </a>
      </p>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      {lista.length === 0 && !erro ? (
        <div className="empty-panel">
          <h2>Nenhum recibo</h2>
          <p>Envie um PDF ou fotos (até 50 MB cada) para criar o primeiro registro.</p>
        </div>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th className="col-abrir">
                <span className="sr-only">Abrir</span>
              </th>
              <th>Data</th>
              <th>Código</th>
              <th>Fornecedor / Prestador</th>
              <th>Documento</th>
              <th>Total</th>
              <th>Status</th>
              <th>Extração</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {lista.map((recibo) => {
              const selecionado = fornecedorSelecionado(recibo);
              const podeSalvar = alterado(recibo) && salvandoId !== recibo.id;
              return (
                <tr key={recibo.id}>
                  <td className="col-abrir">
                    <a
                      href={`/recibos/${recibo.id}`}
                      className="icone-abrir"
                      title="Visualizar recibo / NF"
                      aria-label={`Visualizar ${codigoCurto(recibo.codigo ?? recibo.id)}`}
                    >
                      <IconeRecibo />
                    </a>
                  </td>
                  <td>
                    <a href={`/recibos/${recibo.id}`}>
                      {new Date(recibo.dataCompra ?? recibo.dataEnvio).toLocaleDateString("pt-BR")}
                    </a>
                  </td>
                  <td>
                    <a href={`/recibos/${recibo.id}`}>{codigoCurto(recibo.codigo ?? recibo.id)}</a>
                  </td>
                  <td className="fornecedor-celula">
                    <select
                      value={selecionado}
                      aria-label={`Fornecedor do recibo ${codigoCurto(recibo.codigo ?? recibo.id)}`}
                      onChange={(e) =>
                        setEdicoes((atual) => ({
                          ...atual,
                          [recibo.id]: e.target.value,
                        }))
                      }
                    >
                      <option value="">Selecionar…</option>
                      {recibo.fornecedorId && !fornecedores.some((f) => f.id === recibo.fornecedorId) ? (
                        <option value={recibo.fornecedorId}>{recibo.fornecedorNome ?? "Cadastro atual"}</option>
                      ) : null}
                      {fornecedores.map((fornecedor) => (
                        <option key={fornecedor.id} value={fornecedor.id}>
                          {fornecedor.nome}
                        </option>
                      ))}
                    </select>
                  </td>
                  <td>
                    {rotuloTipoDocumento(recibo.tipoDocumento)} · {rotuloTipoItem(recibo.tipoItem)}
                  </td>
                  <td>
                    {recibo.total != null
                      ? recibo.total.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })
                      : "—"}
                  </td>
                  <td>{statusReciboLabel(recibo.status)}</td>
                  <td>{mensagemLista(recibo)}</td>
                  <td>
                    <div className="acoes-linha">
                      <button
                        className="icone-salvar"
                        type="button"
                        disabled={!podeSalvar}
                        title="Salvar fornecedor"
                        aria-label={`Salvar fornecedor do recibo ${codigoCurto(recibo.codigo ?? recibo.id)}`}
                        onClick={() => void salvarFornecedor(recibo)}
                      >
                        {salvandoId === recibo.id ? <IconeSalvando /> : <IconeSalvar />}
                      </button>
                      <button
                        className="danger-button"
                        type="button"
                        disabled={excluindo === recibo.id}
                        onClick={() => void remover(recibo.id)}
                      >
                        {excluindo === recibo.id ? "Excluindo…" : "Excluir"}
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </AppShell>
  );
}

function IconeRecibo() {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M7 3.5h10a1.2 1.2 0 0 1 1.2 1.2V20l-2.1-1.2-2.1 1.2-2-1.2-2 1.2-2.1-1.2-2.1 1.2V4.7A1.2 1.2 0 0 1 7 3.5Z"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinejoin="round"
      />
      <path d="M9 8h6M9 11.5h6M9 15h4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
    </svg>
  );
}

function IconeSalvar() {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M5 5.5A1.5 1.5 0 0 1 6.5 4h9.2L19 7.3V18.5A1.5 1.5 0 0 1 17.5 20h-11A1.5 1.5 0 0 1 5 18.5v-13Z"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinejoin="round"
      />
      <path d="M8 4.5V9h8V4.5M8 20v-6h8v6" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
    </svg>
  );
}

function IconeSalvando() {
  return (
    <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="7" stroke="currentColor" strokeWidth="1.7" strokeDasharray="8 6" />
    </svg>
  );
}

function mensagemLista(recibo: ReciboLista) {
  if (reciboComFalha(recibo.status)) {
    return mensagemUsuario(recibo.ultimoErro ?? "Falha no processamento.");
  }
  if (reciboEmProcessamento(recibo.status) || jsonExtracao(recibo) == null) {
    return "Aguardando extração dos dados do documento...";
  }
  return "Dados extraídos.";
}
