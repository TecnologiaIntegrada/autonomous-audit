"use client";

import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { apiBaseUrl, ApiError } from "@/lib/api";
import {
  codigoCurto,
  recibosApi,
  rotuloContraparte,
  rotuloContraparteLista,
  rotuloInscricao,
  rotuloParteNota,
  statusReciboLabel,
  type Fornecedor,
  type RelatorioCompra,
} from "@/lib/recibos";

function isoLocal(data: Date) {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${data.getFullYear()}-${pad(data.getMonth() + 1)}-${pad(data.getDate())}T${pad(data.getHours())}:${pad(data.getMinutes())}`;
}

function paraIso(valor: string) {
  const data = new Date(valor);
  return data.toISOString();
}

function formatarMoeda(valor?: number | null) {
  if (valor == null) {
    return "—";
  }
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

function formatarNumero(valor?: number | null) {
  if (valor == null) {
    return "—";
  }
  return valor.toLocaleString("pt-BR", { maximumFractionDigits: 4 });
}

function texto(valor?: string | number | null) {
  if (valor == null || valor === "") {
    return "—";
  }
  return String(valor);
}

function dataBr(valor?: string | null) {
  if (!valor) {
    return "—";
  }
  const data = new Date(valor);
  return Number.isNaN(data.getTime()) ? valor : data.toLocaleString("pt-BR");
}

function Dado({ rotulo, valor }: { rotulo: string; valor?: string | number | null }) {
  return (
    <>
      <dt>{rotulo}</dt>
      <dd>{texto(valor)}</dd>
    </>
  );
}

function baixarBlob(blob: Blob, nome: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nome;
  a.click();
  URL.revokeObjectURL(url);
}

export default function RelatorioPage() {
  const agora = useMemo(() => new Date(), []);
  const inicioPadrao = useMemo(() => {
    const d = new Date(agora.getFullYear(), agora.getMonth(), 1, 0, 0, 0);
    return isoLocal(d);
  }, [agora]);
  const fimPadrao = useMemo(() => isoLocal(agora), [agora]);

  const [inicio, setInicio] = useState(inicioPadrao);
  const [fim, setFim] = useState(fimPadrao);
  const [fornecedorId, setFornecedorId] = useState("");
  const [fornecedores, setFornecedores] = useState<Fornecedor[]>([]);
  const [compras, setCompras] = useState<RelatorioCompra[]>([]);
  const [limite, setLimite] = useState(10000);
  const [total, setTotal] = useState(0);
  const [erro, setErro] = useState("");
  const [carregando, setCarregando] = useState(false);
  const dialogRef = useRef<HTMLDialogElement>(null);
  const detalheRef = useRef<HTMLDialogElement>(null);
  const [curl, setCurl] = useState("");
  const [copiado, setCopiado] = useState(false);
  const [detalhe, setDetalhe] = useState<RelatorioCompra | null>(null);

  useEffect(() => {
    void recibosApi
      .fornecedores(undefined, undefined, true)
      .then(setFornecedores)
      .catch(() => undefined);
  }, []);

  function paramsConsulta() {
    return {
      inicio: paraIso(inicio),
      fim: paraIso(fim),
      fornecedorId: fornecedorId || undefined,
      limite: 10000,
    };
  }

  async function consultar(e?: FormEvent) {
    e?.preventDefault();
    setErro("");
    const inicioIso = paraIso(inicio);
    const fimIso = paraIso(fim);
    const dias = (new Date(fimIso).getTime() - new Date(inicioIso).getTime()) / (24 * 60 * 60 * 1000);
    if (dias > 93) {
      setErro("O período não pode ser maior que 3 meses.");
      return;
    }
    setCarregando(true);
    try {
      const resposta = await recibosApi.relatorioItens(paramsConsulta());
      setCompras(resposta.compras ?? []);
      setLimite(resposta.limite);
      setTotal(resposta.total);
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível montar o relatório.");
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    void consultar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function exportar(formato: "csv" | "xlsx") {
    setErro("");
    try {
      const blob = await recibosApi.relatorioArquivo(paramsConsulta(), formato);
      baixarBlob(blob, formato === "xlsx" ? "relatorio-compras.xlsx" : "relatorio-compras.csv");
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível exportar o relatório.");
    }
  }

  function abrirCurl() {
    const qs = new URLSearchParams();
    qs.set("inicio", paraIso(inicio));
    qs.set("fim", paraIso(fim));
    if (fornecedorId) qs.set("fornecedorId", fornecedorId);
    qs.set("limite", "10000");
    const comando = `curl -sS -X GET "${apiBaseUrl}/v1/relatorios/itens?${qs.toString()}" -H "Authorization: Bearer $AA_RELATORIO_TOKEN" -H "Accept: application/json"`;
    setCurl(comando);
    setCopiado(false);
    dialogRef.current?.showModal();
  }

  function abrirDetalhe(compra: RelatorioCompra) {
    setDetalhe(compra);
    detalheRef.current?.showModal();
  }

  function fecharDetalhe() {
    detalheRef.current?.close();
  }

  async function copiarCurl() {
    try {
      await navigator.clipboard.writeText(curl);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  }

  return (
    <AppShell titulo="Relatório">
      <p>
        Hierarquia compra → fornecedor/prestador → produtos e serviços, com link para o PDF. A consulta, o CSV, o Excel e
        a API aceitam até 3 meses e 10.000 registros deste usuário. Acrescente{" "}
        <code>formato=xlsx</code> ou <code>formato=csv</code> na API para baixar a planilha.
      </p>
      <form className="inline-form relatorio-filtros" onSubmit={(e) => void consultar(e)}>
        <label>
          Início
          <input type="datetime-local" value={inicio} onChange={(e) => setInicio(e.target.value)} required />
        </label>
        <label>
          Fim
          <input type="datetime-local" value={fim} onChange={(e) => setFim(e.target.value)} required />
        </label>
        <label>
          {rotuloContraparteLista()}
          <select value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
            <option value="">Todos</option>
            {fornecedores.map((fornecedor) => (
              <option key={fornecedor.id} value={fornecedor.id}>
                {fornecedor.nome}
              </option>
            ))}
          </select>
        </label>
        <button className="primary-button" type="submit" disabled={carregando}>
          {carregando ? "Consultando…" : "Consultar"}
        </button>
        <button className="ghost-button dark" type="button" disabled={compras.length === 0} onClick={() => void exportar("csv")}>
          Exportar CSV
        </button>
        <button className="ghost-button dark" type="button" disabled={compras.length === 0} onClick={() => void exportar("xlsx")}>
          Exportar Excel
        </button>
        <button className="ghost-button dark" type="button" onClick={abrirCurl}>
          Gerar curl
        </button>
      </form>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      <p className="muted">
        {total} compra(s) nesta chamada (limite {limite}).
      </p>
      {compras.map((compra) => (
        <section className="panel-block relatorio-compra" key={compra.compraId}>
          <h2>
            Compra {compra.numeroRecibo ?? codigoCurto(compra.codigo)}{" "}
            <span className="muted">
              {new Date(compra.data).toLocaleDateString("pt-BR")} · {statusReciboLabel(compra.status)}
            </span>
          </h2>
          <p>
            Subtotal {formatarMoeda(compra.subtotal)} · Descontos {formatarMoeda(compra.descontos)} ·
            Acréscimos {formatarMoeda(compra.acrescimos)} · Total {formatarMoeda(compra.total)} ·{" "}
            {compra.formaPagamento ?? "Pagamento não informado"}
            {" · "}
            {compra.arquivoUrl ? (
              <a href={compra.arquivoUrl} target="_blank" rel="noreferrer">
                Ver PDF
              </a>
            ) : (
              <a href={`/compras/${compra.compraId}`}>Abrir compra</a>
            )}
            {" · "}
            <button className="ghost-button dark" type="button" onClick={() => abrirDetalhe(compra)}>
              Visualizar
            </button>
          </p>
          <h3>{rotuloContraparte(compra.tipoDocumento, compra.tipoItem)}</h3>
          <p>
            {compra.fornecedor.nome ?? "—"}
            {compra.fornecedor.razaoSocial ? ` · ${compra.fornecedor.razaoSocial}` : ""}
            {compra.fornecedor.cpfCnpj ? ` · ${compra.fornecedor.cpfCnpj}` : ""}
            {compra.fornecedor.telefone ? ` · ${compra.fornecedor.telefone}` : ""}
            {compra.fornecedor.endereco ? ` · ${compra.fornecedor.endereco}` : ""}
          </p>
          <h3>Produtos</h3>
          {compra.produtos.length === 0 ? (
            <p className="muted">Nenhum produto extraído nesta compra.</p>
          ) : (
            <table className="data-table">
              <thead>
                <tr>
                  <th>Descrição</th>
                  <th>Código</th>
                  <th>Marca</th>
                  <th>Variante</th>
                  <th>Embalagem</th>
                  <th>Qtd</th>
                  <th>Unitário</th>
                  <th>Desconto</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {compra.produtos.map((produto, indice) => (
                  <tr key={`${compra.compraId}-${produto.id ?? produto.descricao}-${indice}`}>
                    <td>{produto.nome ?? produto.descricao}</td>
                    <td>{produto.codigo ?? "—"}</td>
                    <td>{produto.marca ?? "—"}</td>
                    <td>{produto.variante ?? "—"}</td>
                    <td>{produto.conteudoEmbalagem ?? "—"}</td>
                    <td>
                      {produto.quantidade}
                      {produto.unidade ? ` ${produto.unidade}` : ""}
                    </td>
                    <td>{formatarMoeda(produto.precoUnitario)}</td>
                    <td>{formatarMoeda(produto.desconto)}</td>
                    <td>{formatarMoeda(produto.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      ))}
      {compras.length === 0 && !carregando ? (
        <p className="muted">Nenhum registro no período selecionado.</p>
      ) : null}

      <dialog
        ref={detalheRef}
        className="modal modal-wide modal-relatorio"
        onClose={() => setDetalhe(null)}
      >
        <button className="close" type="button" aria-label="Fechar" onClick={fecharDetalhe}>
          ×
        </button>
        <h2>Detalhe da compra</h2>
        {detalhe ? (
          <>
            <p>
              {detalhe.numeroRecibo ?? codigoCurto(detalhe.codigo)} · {dataBr(detalhe.data)} ·{" "}
              {statusReciboLabel(detalhe.status)}
            </p>
            <div className="detalhe-bloco">
              <h3>Compra</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Código" valor={codigoCurto(detalhe.codigo)} />
                <Dado rotulo="Tipo documento" valor={detalhe.tipoDocumento} />
                <Dado rotulo="Tipo item" valor={detalhe.tipoItem} />
                <Dado rotulo="DANFE tipo" valor={detalhe.danfeTipo} />
                <Dado rotulo="Série" valor={detalhe.serie} />
                <Dado rotulo="Folha" valor={detalhe.folha} />
                <Dado rotulo="Chave de acesso" valor={detalhe.chaveAcesso} />
                <Dado rotulo="Código de barras" valor={detalhe.codigoBarras} />
                <Dado rotulo="Protocolo" valor={detalhe.protocoloAutorizacao} />
                <Dado rotulo="Data protocolo" valor={dataBr(detalhe.protocoloData)} />
                <Dado rotulo="Natureza" valor={detalhe.naturezaOperacao} />
                <Dado rotulo="IE" valor={detalhe.inscricaoEstadual} />
                <Dado rotulo="IE ST" valor={detalhe.inscricaoEstadualSt} />
                <Dado rotulo="Emissão" valor={dataBr(detalhe.dataEmissao)} />
                <Dado rotulo="Subtotal" valor={formatarMoeda(detalhe.subtotal)} />
                <Dado rotulo="Descontos" valor={formatarMoeda(detalhe.descontos)} />
                <Dado rotulo="Acréscimos" valor={formatarMoeda(detalhe.acrescimos)} />
                <Dado rotulo="Total" valor={formatarMoeda(detalhe.total)} />
                <Dado rotulo="Pagamento" valor={detalhe.formaPagamento} />
              </dl>
            </div>
            <div className="detalhe-bloco">
              <h3>{rotuloContraparte(detalhe.tipoDocumento, detalhe.tipoItem)}</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Nome" valor={detalhe.fornecedor.nome} />
                <Dado rotulo="Razão social" valor={detalhe.fornecedor.razaoSocial} />
                <Dado rotulo="CPF/CNPJ" valor={detalhe.fornecedor.cpfCnpj} />
                <Dado rotulo="Telefone" valor={detalhe.fornecedor.telefone} />
                <Dado rotulo="Endereço" valor={detalhe.fornecedor.endereco} />
              </dl>
            </div>
            <div className="detalhe-bloco">
              <h3>Produtos e serviços</h3>
              {detalhe.produtos.length === 0 ? (
                <p className="muted">Nenhum item nesta compra.</p>
              ) : (
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Tipo</th>
                      <th>Descrição</th>
                      <th>Código</th>
                      <th>NCM/SH</th>
                      <th>CSOSN</th>
                      <th>CFOP</th>
                      <th>Qtd</th>
                      <th>Unitário</th>
                      <th>Líquido</th>
                      <th>ICMS</th>
                      <th>IPI</th>
                      <th>Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detalhe.produtos.map((item, indice) => (
                      <tr key={`${detalhe.compraId}-det-${indice}`}>
                        <td>{item.tipoItem ?? "produto"}</td>
                        <td>{item.nome ?? item.descricao}</td>
                        <td>{item.codigo ?? item.codigoExterno ?? "—"}</td>
                        <td>{item.ncmSh ?? "—"}</td>
                        <td>{item.csosn ?? "—"}</td>
                        <td>{item.cfop ?? "—"}</td>
                        <td>
                          {item.quantidade}
                          {item.unidade ? ` ${item.unidade}` : ""}
                        </td>
                        <td>{formatarMoeda(item.precoUnitario)}</td>
                        <td>{formatarMoeda(item.valorLiquido)}</td>
                        <td>{formatarMoeda(item.valorIcms)}</td>
                        <td>{formatarMoeda(item.valorIpi)}</td>
                        <td>{formatarMoeda(item.total)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
            <div className="detalhe-bloco">
              <h3>{rotuloParteNota(detalhe.tipoDocumento, detalhe.tipoItem)}</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Nome" valor={detalhe.nfeDestinatario?.nomeRazaoSocial} />
                <Dado rotulo="CPF/CNPJ" valor={detalhe.nfeDestinatario?.cpfCnpj} />
                <Dado rotulo="Endereço" valor={detalhe.nfeDestinatario?.endereco} />
                <Dado rotulo="Bairro" valor={detalhe.nfeDestinatario?.bairro} />
                <Dado rotulo="CEP" valor={detalhe.nfeDestinatario?.cep} />
                <Dado rotulo="Município" valor={detalhe.nfeDestinatario?.municipio} />
                <Dado rotulo="UF" valor={detalhe.nfeDestinatario?.uf} />
                <Dado rotulo="Telefone" valor={detalhe.nfeDestinatario?.telefone} />
                <Dado rotulo={rotuloInscricao(detalhe.tipoDocumento, detalhe.tipoItem)} valor={detalhe.nfeDestinatario?.inscricaoEstadual} />
                <Dado rotulo="Emissão" valor={dataBr(detalhe.nfeDestinatario?.dataEmissao)} />
                <Dado rotulo="Saída" valor={dataBr(detalhe.nfeDestinatario?.dataSaida)} />
                <Dado rotulo="Hora saída" valor={detalhe.nfeDestinatario?.horaSaida} />
              </dl>
            </div>
            <div className="detalhe-bloco">
              <h3>Cálculo do imposto</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Base ICMS" valor={formatarMoeda(detalhe.nfeImposto?.baseIcms)} />
                <Dado rotulo="Valor ICMS" valor={formatarMoeda(detalhe.nfeImposto?.valorIcms)} />
                <Dado rotulo="Base ICMS ST" valor={formatarMoeda(detalhe.nfeImposto?.baseIcmsSt)} />
                <Dado rotulo="Valor ICMS ST" valor={formatarMoeda(detalhe.nfeImposto?.valorIcmsSt)} />
                <Dado rotulo="Total produtos" valor={formatarMoeda(detalhe.nfeImposto?.valorTotalProdutos)} />
                <Dado rotulo="Frete" valor={formatarMoeda(detalhe.nfeImposto?.valorFrete)} />
                <Dado rotulo="Seguro" valor={formatarMoeda(detalhe.nfeImposto?.valorSeguro)} />
                <Dado rotulo="Desconto" valor={formatarMoeda(detalhe.nfeImposto?.desconto)} />
                <Dado rotulo="Outras despesas" valor={formatarMoeda(detalhe.nfeImposto?.outrasDespesas)} />
                <Dado rotulo="Valor IPI" valor={formatarMoeda(detalhe.nfeImposto?.valorIpi)} />
                <Dado rotulo="Total nota" valor={formatarMoeda(detalhe.nfeImposto?.valorTotalNota)} />
              </dl>
            </div>
            <div className="detalhe-bloco">
              <h3>Transportador / volumes</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Nome" valor={detalhe.nfeTransportador?.nomeRazaoSocial} />
                <Dado rotulo="Frete por conta" valor={detalhe.nfeTransportador?.fretePorConta} />
                <Dado rotulo="ANTT" valor={detalhe.nfeTransportador?.codigoAntt} />
                <Dado rotulo="Placa" valor={detalhe.nfeTransportador?.placa} />
                <Dado rotulo="UF placa" valor={detalhe.nfeTransportador?.uf} />
                <Dado rotulo="CPF/CNPJ" valor={detalhe.nfeTransportador?.cpfCnpj} />
                <Dado rotulo="Endereço" valor={detalhe.nfeTransportador?.endereco} />
                <Dado rotulo="Município" valor={detalhe.nfeTransportador?.municipio} />
                <Dado rotulo="UF" valor={detalhe.nfeTransportador?.ufEndereco} />
                <Dado rotulo="IE" valor={detalhe.nfeTransportador?.inscricaoEstadual} />
                <Dado rotulo="Volumes" valor={formatarNumero(detalhe.nfeTransportador?.quantidadeVolumes)} />
                <Dado rotulo="Espécie" valor={detalhe.nfeTransportador?.especie} />
                <Dado rotulo="Marca" valor={detalhe.nfeTransportador?.marca} />
                <Dado rotulo="Numeração" valor={detalhe.nfeTransportador?.numeracao} />
                <Dado rotulo="Peso bruto" valor={formatarNumero(detalhe.nfeTransportador?.pesoBruto)} />
                <Dado rotulo="Peso líquido" valor={formatarNumero(detalhe.nfeTransportador?.pesoLiquido)} />
              </dl>
            </div>
            <div className="detalhe-bloco">
              <h3>Dados adicionais</h3>
              <dl className="detalhe-grid">
                <Dado rotulo="Informações" valor={detalhe.nfeAdicionais?.informacoesComplementares} />
                <Dado rotulo="Reservado ao fisco" valor={detalhe.nfeAdicionais?.reservadoAoFisco} />
                <Dado rotulo="Impressão" valor={dataBr(detalhe.nfeAdicionais?.dataHoraImpressao)} />
              </dl>
            </div>
            <p className="toolbar">
              <button className="ghost-button dark" type="button" onClick={fecharDetalhe}>
                Fechar
              </button>
            </p>
          </>
        ) : null}
      </dialog>

      <dialog ref={dialogRef} className="modal modal-wide" onClose={() => setCopiado(false)}>
        <h2>Integração via curl</h2>
        <p>
          Endpoint limitado a 3 meses e 10.000 registros do seu usuário. A resposta JSON usa a
          hierarquia <code>compras[]</code> com fornecedor e produtos, além de{" "}
          <code>arquivoUrl</code> para o PDF. Use o token gerado em{" "}
          <a href="/configuracoes">Configurações</a>. Para planilha: acrescente{" "}
          <code>&formato=xlsx</code> ou <code>&formato=csv</code>.
        </p>
        <pre className="curl-block">{curl}</pre>
        <p className="toolbar">
          <button className="primary-button" type="button" onClick={() => void copiarCurl()}>
            {copiado ? "Copiado" : "Copiar"}
          </button>
          <button className="ghost-button dark" type="button" onClick={() => dialogRef.current?.close()}>
            Fechar
          </button>
        </p>
      </dialog>
    </AppShell>
  );
}
