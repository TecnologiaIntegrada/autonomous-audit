"use client";

import { useEffect, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { ApiError } from "@/lib/api";
import { comprasApi, statusCompraLabel, type CompraLista } from "@/lib/compras";
import { codigoCurto, rotuloTipoDocumento, rotuloTipoItem } from "@/lib/recibos";

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

export default function ComprasPage() {
  const [lista, setLista] = useState<CompraLista[]>([]);
  const [erro, setErro] = useState("");

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        const dados = await comprasApi.listar();
        if (!cancelado) {
          setLista(dados);
          setErro("");
        }
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível listar as compras.");
        }
      }
    })();
    return () => {
      cancelado = true;
    };
  }, []);

  return (
    <AppShell titulo="Compras">
      <p>
        Cada imagem reconhecida como compra gera um lançamento, com fornecedor ou prestador, valores e
        produtos ou serviços. Recibos e notas fiscais aparecem na mesma lista, com o tipo
        sinalizado.
      </p>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      {lista.length === 0 && !erro ? (
        <div className="empty-panel">
          <h2>Nenhuma compra lançada</h2>
          <p>
            Envie um recibo ou NF em{" "}
            <a href="/recibos/novo">Adicionar Recibo / NF</a>. Os dados aparecem aqui após a extração.
          </p>
        </div>
      ) : (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Data envio</th>
                <th>Data compra</th>
                <th>Recibo</th>
                <th>Recibo / NF</th>
                <th>Item</th>
                <th>Páginas</th>
                <th>Fornecedor / Prestador</th>
                <th>Documento</th>
                <th>Telefone</th>
                <th>Endereço</th>
                <th>Pagamento</th>
                <th>Subtotal</th>
                <th>Descontos</th>
                <th>Acréscimos</th>
                <th>Total</th>
                <th>PDF</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {lista.map((compra) => (
                <tr key={compra.id}>
                  <td>
                    <a href={`/compras/${compra.id}`}>{formatarData(compra.dataEnvio)}</a>
                  </td>
                  <td>{formatarData(compra.dataCompra)}</td>
                  <td>
                    <a href={`/compras/${compra.id}`}>{compra.numeroRecibo ?? codigoCurto(compra.codigo ?? compra.id)}</a>
                  </td>
                  <td>{rotuloTipoDocumento(compra.tipoDocumento)}</td>
                  <td>{rotuloTipoItem(compra.tipoItem)}</td>
                  <td>{compra.paginas ?? "—"}</td>
                  <td>{compra.fornecedorNome ?? "—"}</td>
                  <td>{compra.fornecedorCpfCnpj ?? "—"}</td>
                  <td>{compra.fornecedorTelefone ?? "—"}</td>
                  <td>{compra.fornecedorEndereco ?? "—"}</td>
                  <td>{compra.formaPagamento ?? "—"}</td>
                  <td>{formatarMoeda(compra.subtotal)}</td>
                  <td>{formatarMoeda(compra.descontos)}</td>
                  <td>{formatarMoeda(compra.acrescimos)}</td>
                  <td>{formatarMoeda(compra.total)}</td>
                  <td>
                    {compra.arquivoUrl ? (
                      <a href={compra.arquivoUrl} target="_blank" rel="noreferrer">
                        PDF
                      </a>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td>{statusCompraLabel(compra.status)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </AppShell>
  );
}
