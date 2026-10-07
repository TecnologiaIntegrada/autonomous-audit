"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ApiError } from "@/lib/api";
import { comprasApi, statusCompraLabel, type CompraDetalhe } from "@/lib/compras";
import { codigoCurto, rotuloDanfe, rotuloContraparte, rotuloInscricao, rotuloParteNota, rotuloTipoDocumento, rotuloTipoItem } from "@/lib/recibos";

function formatarData(valor?: string | null) {
  if (!valor) {
    return "—";
  }
  const data = new Date(valor);
  if (Number.isNaN(data.getTime())) {
    return "—";
  }
  return data.toLocaleDateString("pt-BR");
}

function formatarMoeda(valor?: number | null) {
  if (valor == null) {
    return "—";
  }
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function CompraDetalhePage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const [compra, setCompra] = useState<CompraDetalhe | null>(null);
  const [erro, setErro] = useState("");

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        const detalhe = await comprasApi.obter(id);
        if (!cancelado) {
          setCompra(detalhe);
          setErro("");
        }
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível abrir a compra.");
        }
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [id]);

  if (!compra && !erro) {
    return (
      <AppShell titulo="Compra">
        <p>Carregando compra…</p>
      </AppShell>
    );
  }

  if (!compra) {
    return (
      <AppShell titulo="Compra">
        <p className="feedback" role="alert">
          {erro}
        </p>
      </AppShell>
    );
  }

  return (
    <AppShell titulo={`Compra ${compra.numeroRecibo ?? codigoCurto(compra.codigo ?? compra.id)}`}>
      <p>
        Status: <strong>{statusCompraLabel(compra.status)}</strong>
        {" · "}
        <a href={`/recibos/${compra.reciboOrigemId ?? compra.id}`}>Ver recibo</a>
        {compra.arquivoUrl ? (
          <>
            {" · "}
            <a href={compra.arquivoUrl} target="_blank" rel="noreferrer">
              Ver PDF
            </a>
          </>
        ) : null}
      </p>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}

      <div className="split-grid">
        <section className="panel-block">
          <h2>Compra</h2>
          <p>Data: {formatarData(compra.dataCompra)}</p>
          <p>Número do recibo/NF: {compra.numeroRecibo ?? "—"}</p>
          <p>Documento: {rotuloTipoDocumento(compra.tipoDocumento)}</p>
          <p>Produto ou serviço: {rotuloTipoItem(compra.tipoItem)}</p>
          {compra.tipoDocumento === "nota_fiscal" ? (
            <>
              <p>DANFE: {rotuloDanfe(compra.danfeTipo)}</p>
              <p>Série: {compra.serie ?? "—"}</p>
              <p>Folha: {compra.folha ?? "—"}</p>
              <p>Chave de acesso: {compra.chaveAcesso ?? "—"}</p>
              <p>Código de barras: {compra.codigoBarras ?? "—"}</p>
              <p>Protocolo: {compra.protocoloAutorizacao ?? "—"}</p>
              <p>Natureza: {compra.naturezaOperacao ?? "—"}</p>
              <p>Inscrição estadual: {compra.inscricaoEstadual ?? "—"}</p>
            </>
          ) : null}
          <p>Subtotal: {formatarMoeda(compra.subtotal)}</p>
          <p>Descontos: {formatarMoeda(compra.descontos)}</p>
          <p>Acréscimos: {formatarMoeda(compra.acrescimos)}</p>
          <p>Total: {formatarMoeda(compra.total)}</p>
          <p>Páginas no PDF: {compra.paginas ?? "—"}</p>
          <p>Forma de pagamento: {compra.formaPagamento ?? "—"}</p>
        </section>
        <section className="panel-block">
          <h2>{rotuloContraparte(compra.tipoDocumento, compra.tipoItem)}</h2>
          <p>Nome: {compra.fornecedorNome ?? "—"}</p>
          <p>Razão social: {compra.fornecedorRazaoSocial ?? "—"}</p>
          <p>CPF/CNPJ: {compra.fornecedorCpfCnpj ?? "—"}</p>
          <p>Telefone: {compra.fornecedorTelefone ?? "—"}</p>
          <p>Endereço: {compra.fornecedorEndereco ?? "—"}</p>
          {compra.fornecedorId ? (
            <p>
              <a href={`/fornecedores?reciboId=${compra.id}`}>
                Abrir cadastro do {rotuloContraparte(compra.tipoDocumento, compra.tipoItem).toLowerCase()}
              </a>
            </p>
          ) : null}
        </section>
      </div>

      <section className="panel-block">
        <h2>Produtos e serviços</h2>
        {compra.itens.length === 0 ? (
          <p className="muted">Nenhum item foi extraído deste documento.</p>
        ) : (
          <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Tipo</th>
                <th>Descrição</th>
                <th>Código</th>
                <th>NCM</th>
                <th>CFOP</th>
                <th>Qtd</th>
                <th>Unidade</th>
                <th>Unitário</th>
                <th>Desconto</th>
                <th>Total</th>
                <th>Cadastro</th>
              </tr>
            </thead>
            <tbody>
              {compra.itens.map((item) => (
                <tr key={item.id}>
                  <td>{rotuloTipoItem(item.tipoItem)}</td>
                  <td>{item.descricaoOriginal}</td>
                  <td>{item.codigoImpresso ?? "—"}</td>
                  <td>{item.ncmSh ?? "—"}</td>
                  <td>{item.cfop ?? "—"}</td>
                  <td>{item.quantidade}</td>
                  <td>{item.unidade ?? "—"}</td>
                  <td>{formatarMoeda(item.precoUnitario)}</td>
                  <td>{formatarMoeda(item.desconto)}</td>
                  <td>{formatarMoeda(item.total)}</td>
                  <td>{item.servicoNome ?? item.produtoNome ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        )}
        <p>
          <a href={`/produtos?reciboId=${compra.id}`}>Produtos/serviços deste documento</a>
        </p>
      </section>

      {compra.tipoDocumento === "nota_fiscal" ? (
        <>
          <div className="split-grid">
            <section className="panel-block">
              <h2>{rotuloParteNota(compra.tipoDocumento, compra.tipoItem)}</h2>
              <p>Nome: {compra.nfeDestinatario?.nomeRazaoSocial ?? "—"}</p>
              <p>CPF/CNPJ: {compra.nfeDestinatario?.cpfCnpj ?? "—"}</p>
              <p>Endereço: {compra.nfeDestinatario?.endereco ?? "—"}</p>
              <p>Bairro: {compra.nfeDestinatario?.bairro ?? "—"}</p>
              <p>CEP: {compra.nfeDestinatario?.cep ?? "—"}</p>
              <p>
                Município: {compra.nfeDestinatario?.municipio ?? "—"} / {compra.nfeDestinatario?.uf ?? "—"}
              </p>
              <p>
                {rotuloInscricao(compra.tipoDocumento, compra.tipoItem)}: {compra.nfeDestinatario?.inscricaoEstadual ?? "—"}
              </p>
            </section>
            <section className="panel-block">
              <h2>Cálculo do imposto</h2>
              <p>Base ICMS: {formatarMoeda(compra.nfeImposto?.baseIcms)}</p>
              <p>Valor ICMS: {formatarMoeda(compra.nfeImposto?.valorIcms)}</p>
              <p>Valor total produtos: {formatarMoeda(compra.nfeImposto?.valorTotalProdutos)}</p>
              <p>Frete: {formatarMoeda(compra.nfeImposto?.valorFrete)}</p>
              <p>IPI: {formatarMoeda(compra.nfeImposto?.valorIpi)}</p>
              <p>Total da nota: {formatarMoeda(compra.nfeImposto?.valorTotalNota)}</p>
            </section>
          </div>
          <div className="split-grid">
            <section className="panel-block">
              <h2>Transportador / volumes</h2>
              <p>Nome: {compra.nfeTransportador?.nomeRazaoSocial ?? "—"}</p>
              <p>Frete por conta: {compra.nfeTransportador?.fretePorConta ?? "—"}</p>
              <p>Placa: {compra.nfeTransportador?.placa ?? "—"}</p>
              <p>Quantidade: {compra.nfeTransportador?.quantidadeVolumes ?? "—"}</p>
              <p>Peso bruto: {compra.nfeTransportador?.pesoBruto ?? "—"}</p>
              <p>Peso líquido: {compra.nfeTransportador?.pesoLiquido ?? "—"}</p>
            </section>
            <section className="panel-block">
              <h2>Dados adicionais</h2>
              <p>{compra.nfeAdicionais?.informacoesComplementares ?? "—"}</p>
              {compra.nfeAdicionais?.reservadoAoFisco ? <p>Fisco: {compra.nfeAdicionais.reservadoAoFisco}</p> : null}
            </section>
          </div>
        </>
      ) : null}
    </AppShell>
  );
}
