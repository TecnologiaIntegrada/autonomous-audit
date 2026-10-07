"use client";

import { useEffect, useMemo, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { ApiError } from "@/lib/api";
import { recibosApi, type RecibosResumo } from "@/lib/recibos";

const LIMITE_PADRAO = 1024 * 1024 * 1024;

const MESES = [
  { valor: 1, rotulo: "Janeiro" },
  { valor: 2, rotulo: "Fevereiro" },
  { valor: 3, rotulo: "Março" },
  { valor: 4, rotulo: "Abril" },
  { valor: 5, rotulo: "Maio" },
  { valor: 6, rotulo: "Junho" },
  { valor: 7, rotulo: "Julho" },
  { valor: 8, rotulo: "Agosto" },
  { valor: 9, rotulo: "Setembro" },
  { valor: 10, rotulo: "Outubro" },
  { valor: 11, rotulo: "Novembro" },
  { valor: 12, rotulo: "Dezembro" },
];

function mesAnoAtualBrasil() {
  const partes = new Intl.DateTimeFormat("pt-BR", {
    timeZone: "America/Sao_Paulo",
    year: "numeric",
    month: "numeric",
  }).formatToParts(new Date());
  const ano = Number(partes.find((p) => p.type === "year")?.value);
  const mes = Number(partes.find((p) => p.type === "month")?.value);
  return { ano, mes };
}

function formatarBytes(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }
  if (bytes < 1024 * 1024 * 1024) {
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`;
}

export default function DashboardPage() {
  const vigente = useMemo(() => mesAnoAtualBrasil(), []);
  const [ano, setAno] = useState(vigente.ano);
  const [mes, setMes] = useState(vigente.mes);
  const [resumo, setResumo] = useState<RecibosResumo | null>(null);
  const [erro, setErro] = useState("");
  const [carregando, setCarregando] = useState(true);

  const anos = useMemo(() => {
    const lista: number[] = [];
    for (let a = vigente.ano; a >= vigente.ano - 5; a -= 1) {
      lista.push(a);
    }
    return lista;
  }, [vigente.ano]);

  useEffect(() => {
    let cancelado = false;
    setCarregando(true);
    (async () => {
      try {
        const dados = await recibosApi.resumo(ano, mes);
        if (!cancelado) {
          setResumo(dados);
          setErro("");
        }
      } catch (falha) {
        if (!cancelado) {
          setResumo(null);
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível carregar o resumo.");
        }
      } finally {
        if (!cancelado) {
          setCarregando(false);
        }
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [ano, mes]);

  const usado = resumo?.armazenamentoUsadoBytes ?? 0;
  const limite = resumo?.armazenamentoLimiteBytes || LIMITE_PADRAO;
  const percentual = Math.min(100, (usado / limite) * 100);
  const esgotado = usado >= limite;

  return (
    <AppShell titulo="Indicadores de uso">
      <p>Dados extraídos dos documentos de operações realizadas : compras, notas fiscais e recibos.</p>
      <div className="inline-form dashboard-filtro">
        <label>
          Tipo Data
          <select
            aria-label="Tipo Data — mês"
            value={mes}
            onChange={(e) => setMes(Number(e.target.value))}
          >
            {MESES.map((m) => (
              <option key={m.valor} value={m.valor}>
                {m.rotulo}
              </option>
            ))}
          </select>
        </label>
        <label>
          Ano
          <select
            aria-label="Tipo Data — ano"
            value={ano}
            onChange={(e) => setAno(Number(e.target.value))}
          >
            {anos.map((a) => (
              <option key={a} value={a}>
                {a}
              </option>
            ))}
          </select>
        </label>
      </div>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      {resumo ? (
        <>
          <div className="kpi-grid">
            <Kpi titulo="Recibos" valor={resumo.totalCompras} href="/recibos" />
            <Kpi titulo="Processados" valor={resumo.processados} href="/recibos" />
            <Kpi titulo="Processando" valor={resumo.processando} />
            <Kpi
              titulo="Total extraído"
              valor={resumo.totalGasto.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
            />
          </div>
          <div className="kpi-grid">
            <Kpi titulo="Falhas" valor={resumo.falhas} href="/recibos" />
            <Kpi
              titulo="Tokens utilizados"
              valor={(resumo.perplexityTokensTotal ?? resumo.qwenTokensTotal ?? resumo.geminiTokensTotal ?? 0).toLocaleString("pt-BR")}
            />
            <Kpi
              titulo="Custo IA"
              valor={(resumo.perplexityCustoTotalBrl ?? resumo.qwenCustoTotalBrl ?? resumo.geminiCustoTotalBrl ?? 0).toLocaleString("pt-BR", {
                style: "currency",
                currency: "BRL",
                minimumFractionDigits: 2,
                maximumFractionDigits: 4,
              })}
            />
          </div>
          <section className="panel-block armazenamento-panel">
            <h2>Armazenamento</h2>
            <div className="armazenamento-layout">
              <Rosca percentual={percentual} esgotado={esgotado} />
              <div>
                <p className="armazenamento-valor">
                  {formatarBytes(usado)} de {formatarBytes(limite)}
                </p>
                <p className="muted">
                  {esgotado
                    ? "Cota de 1 GB atingida. Exclua recibos para anexar novos arquivos."
                    : `Restam ${formatarBytes(Math.max(0, limite - usado))} para novos documentos.`}
                </p>
              </div>
            </div>
          </section>
          {resumo.porFornecedor.length > 0 ? (
            <section className="panel-block">
              <h2>Por fornecedor / prestador</h2>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Fornecedor / Prestador</th>
                    <th>Recibos</th>
                    <th>Total</th>
                  </tr>
                </thead>
                <tbody>
                  {resumo.porFornecedor.map((linha) => (
                    <tr key={linha.fornecedorId ?? linha.nome}>
                      <td>{linha.nome}</td>
                      <td>{linha.quantidade}</td>
                      <td>
                        {linha.total.toLocaleString("pt-BR", { style: "currency", currency: "BRL" })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>
          ) : (
            <div className="empty-panel">
              <h2>Nenhum recibo no período</h2>
              <p>Comece por Adicionar Recibo / NF para enviar um PDF ou fotos.</p>
              <a className="primary-button" href="/recibos/novo">
                Adicionar Recibo / NF
              </a>
            </div>
          )}
        </>
      ) : carregando && !erro ? (
        <p className="feedback">Carregando indicadores…</p>
      ) : null}
    </AppShell>
  );
}

function Rosca({ percentual, esgotado }: { percentual: number; esgotado: boolean }) {
  const raio = 54;
  const circunferencia = 2 * Math.PI * raio;
  const preenchido = (Math.max(0, Math.min(100, percentual)) / 100) * circunferencia;
  return (
    <svg className="rosca" viewBox="0 0 140 140" role="img" aria-label={`Uso de armazenamento ${percentual.toFixed(0)}%`}>
      <circle className="rosca-fundo" cx="70" cy="70" r={raio} />
      <circle
        className={esgotado ? "rosca-uso is-full" : "rosca-uso"}
        cx="70"
        cy="70"
        r={raio}
        strokeDasharray={`${preenchido} ${circunferencia}`}
      />
      <text x="70" y="76" textAnchor="middle">
        {percentual.toFixed(0)}%
      </text>
    </svg>
  );
}

function Kpi({ titulo, valor, href }: { titulo: string; valor: number | string; href?: string }) {
  const inner = (
    <>
      <span>{titulo}</span>
      <strong>{valor}</strong>
    </>
  );
  return href ? (
    <a className="kpi-card" href={href}>
      {inner}
    </a>
  ) : (
    <div className="kpi-card">{inner}</div>
  );
}
