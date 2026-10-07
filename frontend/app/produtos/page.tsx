"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { ApiError } from "@/lib/api";
import {
  codigoCurto,
  recibosApi,
  rotuloReciboOpcao,
  UNIDADES_CONTROLE,
  type Produto,
  type ReciboLista,
  type Servico,
} from "@/lib/recibos";

type TipoLista = "produto" | "servico";
type AbaModal = "identificacao" | "classificacao" | "valores";

type Formulario = {
  nome: string;
  marca: string;
  variante: string;
  unidadeControle: string;
  conteudoEmbalagem: string;
  sinonimos: string;
  codigoExterno: string;
  ncmSh: string;
  csosn: string;
  cfop: string;
  valorUnitario: string;
  valorDesconto: string;
  valorLiquido: string;
  baseIcms: string;
  valorIcms: string;
  valorIpi: string;
  aliqIcms: string;
  aliqIpi: string;
  reciboId: string;
};

const formularioVazio: Formulario = {
  nome: "",
  marca: "",
  variante: "",
  unidadeControle: "un",
  conteudoEmbalagem: "",
  sinonimos: "",
  codigoExterno: "",
  ncmSh: "",
  csosn: "",
  cfop: "",
  valorUnitario: "",
  valorDesconto: "",
  valorLiquido: "",
  baseIcms: "",
  valorIcms: "",
  valorIpi: "",
  aliqIcms: "",
  aliqIpi: "",
  reciboId: "",
};

function texto(valor?: string | number | null) {
  return valor == null ? "" : String(valor);
}

function formularioDeProduto(produto: Produto): Formulario {
  return {
    ...formularioVazio,
    nome: produto.nome ?? "",
    marca: produto.marca ?? "",
    variante: produto.variante ?? "",
    unidadeControle: produto.unidadeControle || "un",
    conteudoEmbalagem: texto(produto.conteudoEmbalagem),
    sinonimos: (produto.sinonimos ?? []).join(", "),
    codigoExterno: produto.codigoExterno ?? "",
    ncmSh: produto.ncmSh ?? "",
    csosn: produto.csosn ?? "",
    cfop: produto.cfop ?? "",
    valorUnitario: texto(produto.valorUnitario),
    valorDesconto: texto(produto.valorDesconto),
    valorLiquido: texto(produto.valorLiquido),
    baseIcms: texto(produto.baseIcms),
    valorIcms: texto(produto.valorIcms),
    valorIpi: texto(produto.valorIpi),
    aliqIcms: texto(produto.aliqIcms),
    aliqIpi: texto(produto.aliqIpi),
    reciboId: produto.reciboId ?? "",
  };
}

function formularioDeServico(servico: Servico): Formulario {
  return {
    ...formularioVazio,
    nome: servico.nome ?? "",
    unidadeControle: servico.unidadeControle || "un",
    codigoExterno: servico.codigoExterno ?? "",
    ncmSh: servico.ncmSh ?? "",
    csosn: servico.csosn ?? "",
    cfop: servico.cfop ?? "",
    valorUnitario: texto(servico.valorUnitario),
    valorDesconto: texto(servico.valorDesconto),
    valorLiquido: texto(servico.valorLiquido),
    baseIcms: texto(servico.baseIcms),
    valorIcms: texto(servico.valorIcms),
    valorIpi: texto(servico.valorIpi),
    aliqIcms: texto(servico.aliqIcms),
    aliqIpi: texto(servico.aliqIpi),
    reciboId: servico.reciboId ?? "",
  };
}

function parseNumero(valor: string): number | null {
  const textoValor = valor.trim().replace(",", ".");
  if (!textoValor) {
    return null;
  }
  const numero = Number(textoValor);
  return Number.isFinite(numero) ? numero : null;
}

function parseSinonimos(valor: string): string[] {
  return valor
    .split(/[,;\n]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function fiscalDoForm(form: Formulario) {
  return {
    codigoExterno: form.codigoExterno.trim() || null,
    ncmSh: form.ncmSh.trim() || null,
    csosn: form.csosn.trim() || null,
    cfop: form.cfop.trim() || null,
    valorUnitario: parseNumero(form.valorUnitario),
    valorDesconto: parseNumero(form.valorDesconto),
    valorLiquido: parseNumero(form.valorLiquido),
    baseIcms: parseNumero(form.baseIcms),
    valorIcms: parseNumero(form.valorIcms),
    valorIpi: parseNumero(form.valorIpi),
    aliqIcms: parseNumero(form.aliqIcms),
    aliqIpi: parseNumero(form.aliqIpi),
  };
}

function formatarMoeda(valor?: number | null) {
  if (valor == null) {
    return "—";
  }
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

type LinhaFiscal = {
  id: string;
  nome: string;
  codigoExterno?: string | null;
  unidadeControle: string;
  valorUnitario?: number | null;
  valorLiquido?: number | null;
  reciboId?: string | null;
};

const COLUNAS_RESUMO = 5;

function CabecalhoFiscal() {
  return (
    <tr>
      <th>Código</th>
      <th>Unid.</th>
      <th>Valor unitário</th>
      <th>Valor líquido</th>
      <th>Recibo</th>
    </tr>
  );
}

function LinhaCadastro({
  linha,
  onEditar,
  onExcluir,
  excluindo,
}: {
  linha: LinhaFiscal;
  onEditar: () => void;
  onExcluir: () => void;
  excluindo: boolean;
}) {
  return (
    <tbody className="produto-grupo">
      <tr className="produto-grupo-nome">
        <td colSpan={COLUNAS_RESUMO}>
          <div className="produto-nome-linha">
            <span className="produto-nome">{linha.nome}</span>
            <div className="acoes-cadastro">
              <button className="ghost-button dark" type="button" onClick={onEditar}>
                Editar
              </button>
              <button className="danger-button" type="button" disabled={excluindo} onClick={onExcluir}>
                {excluindo ? "Excluindo…" : "Excluir"}
              </button>
            </div>
          </div>
        </td>
      </tr>
      <tr className="produto-grupo-dados">
        <td>{linha.codigoExterno ?? "—"}</td>
        <td>{linha.unidadeControle}</td>
        <td>{formatarMoeda(linha.valorUnitario)}</td>
        <td>{formatarMoeda(linha.valorLiquido)}</td>
        <td>{linha.reciboId ? <a href={`/recibos/${linha.reciboId}`}>{codigoCurto(linha.reciboId)}</a> : "—"}</td>
      </tr>
    </tbody>
  );
}

export default function ProdutosPage() {
  const [tipoLista, setTipoLista] = useState<TipoLista>("produto");
  const [produtos, setProdutos] = useState<Produto[]>([]);
  const [servicos, setServicos] = useState<Servico[]>([]);
  const [recibos, setRecibos] = useState<ReciboLista[]>([]);
  const [reciboId, setReciboId] = useState("");
  const [busca, setBusca] = useState("");
  const [erro, setErro] = useState("");
  const [erroModal, setErroModal] = useState("");
  const [salvando, setSalvando] = useState(false);
  const [excluindo, setExcluindo] = useState<string | null>(null);
  const [criando, setCriando] = useState(false);
  const [tipoModal, setTipoModal] = useState<TipoLista>("produto");
  const [editandoProduto, setEditandoProduto] = useState<Produto | null>(null);
  const [editandoServico, setEditandoServico] = useState<Servico | null>(null);
  const [abaModal, setAbaModal] = useState<AbaModal>("identificacao");
  const [form, setForm] = useState<Formulario>(formularioVazio);
  const dialogRef = useRef<HTMLDialogElement>(null);

  async function carregar(filtro?: string) {
    const [listaProdutos, listaServicos, listaRecibos] = await Promise.all([
      recibosApi.produtos(undefined, filtro || undefined),
      recibosApi.servicos(undefined, filtro || undefined),
      recibosApi.listar(),
    ]);
    setProdutos(listaProdutos);
    setServicos(listaServicos);
    setRecibos(listaRecibos);
  }

  useEffect(() => {
    const filtro = new URLSearchParams(window.location.search).get("reciboId") ?? "";
    setReciboId(filtro);
    void carregar(filtro).catch((falha) =>
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível listar produtos e serviços."),
    );
  }, []);

  function abrirNovo() {
    setCriando(true);
    setTipoModal(tipoLista);
    setEditandoProduto(null);
    setEditandoServico(null);
    setErroModal("");
    setAbaModal("identificacao");
    setForm({ ...formularioVazio, reciboId });
    dialogRef.current?.showModal();
  }

  function abrirProduto(produto: Produto) {
    setCriando(false);
    setTipoModal("produto");
    setEditandoProduto(produto);
    setEditandoServico(null);
    setErroModal("");
    setAbaModal("identificacao");
    setForm(formularioDeProduto(produto));
    dialogRef.current?.showModal();
  }

  function abrirServico(servico: Servico) {
    setCriando(false);
    setTipoModal("servico");
    setEditandoServico(servico);
    setEditandoProduto(null);
    setErroModal("");
    setAbaModal("identificacao");
    setForm(formularioDeServico(servico));
    dialogRef.current?.showModal();
  }

  function fecharEdicao() {
    dialogRef.current?.close();
  }

  function aoFecharModal() {
    setEditandoProduto(null);
    setEditandoServico(null);
    setCriando(false);
    setErroModal("");
    setForm(formularioVazio);
  }

  function validarNumeros() {
    const campos: [string, string][] = [
      ["embalagem", form.conteudoEmbalagem],
      ["valor unitário", form.valorUnitario],
      ["valor desconto", form.valorDesconto],
      ["valor líquido", form.valorLiquido],
      ["base ICMS", form.baseIcms],
      ["valor ICMS", form.valorIcms],
      ["valor IPI", form.valorIpi],
      ["alíquota ICMS", form.aliqIcms],
      ["alíquota IPI", form.aliqIpi],
    ];
    for (const [nome, valor] of campos) {
      if (valor.trim() && parseNumero(valor) == null) {
        return `Informe um número válido em ${nome}.`;
      }
    }
    return "";
  }

  async function salvar(evento: FormEvent) {
    evento.preventDefault();
    const invalido = validarNumeros();
    if (invalido) {
      setErroModal(invalido);
      return;
    }
    setSalvando(true);
    setErroModal("");
    const fiscal = fiscalDoForm(form);
    const reciboVinculo = form.reciboId || null;
    try {
      if (criando && tipoModal === "servico") {
        const criado = await recibosApi.criarServico({
          nome: form.nome.trim(),
          unidadeControle: form.unidadeControle.trim() || "un",
          reciboId: reciboVinculo,
          ...fiscal,
        });
        setServicos((atual) => [criado, ...atual]);
      } else if (criando) {
        const criado = await recibosApi.criarProduto({
          nome: form.nome.trim(),
          marca: form.marca.trim() || null,
          variante: form.variante.trim() || null,
          unidadeControle: form.unidadeControle.trim() || "un",
          conteudoEmbalagem: parseNumero(form.conteudoEmbalagem),
          sinonimos: parseSinonimos(form.sinonimos),
          reciboId: reciboVinculo,
          ...fiscal,
        });
        setProdutos((atual) => [criado, ...atual]);
      } else if (editandoProduto) {
        const atualizado = await recibosApi.atualizarProduto(editandoProduto.id, {
          nome: form.nome.trim(),
          marca: form.marca.trim() || null,
          variante: form.variante.trim() || null,
          unidadeControle: form.unidadeControle.trim() || "un",
          conteudoEmbalagem: parseNumero(form.conteudoEmbalagem),
          sinonimos: parseSinonimos(form.sinonimos),
          reciboId: reciboVinculo,
          ...fiscal,
        });
        setProdutos((atual) => atual.map((item) => (item.id === atualizado.id ? { ...item, ...atualizado } : item)));
      } else if (editandoServico) {
        const atualizado = await recibosApi.atualizarServico(editandoServico.id, {
          nome: form.nome.trim(),
          unidadeControle: form.unidadeControle.trim() || "un",
          reciboId: reciboVinculo,
          ...fiscal,
        });
        setServicos((atual) => atual.map((item) => (item.id === atualizado.id ? { ...item, ...atualizado } : item)));
      }
      fecharEdicao();
    } catch (falha) {
      setErroModal(falha instanceof ApiError ? falha.message : "Não foi possível salvar o cadastro.");
    } finally {
      setSalvando(false);
    }
  }

  async function excluirProduto(produto: Produto) {
    if (!window.confirm(`Excluir o produto ${produto.nome}? Esta ação não pode ser desfeita.`)) {
      return;
    }
    setExcluindo(produto.id);
    setErro("");
    try {
      await recibosApi.excluirProduto(produto.id);
      setProdutos((atual) => atual.filter((item) => item.id !== produto.id));
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível excluir o produto.");
    } finally {
      setExcluindo(null);
    }
  }

  async function excluirServico(servico: Servico) {
    if (!window.confirm(`Excluir o serviço ${servico.nome}? Esta ação não pode ser desfeita.`)) {
      return;
    }
    setExcluindo(servico.id);
    setErro("");
    try {
      await recibosApi.excluirServico(servico.id);
      setServicos((atual) => atual.filter((item) => item.id !== servico.id));
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível excluir o serviço.");
    } finally {
      setExcluindo(null);
    }
  }

  const termo = busca.trim().toLowerCase();
  const produtosVisiveis = produtos.filter((item) =>
    !termo
      ? true
      : [item.nome, item.codigoExterno, item.marca, codigoCurto(item.id)]
          .filter(Boolean)
          .some((campo) => String(campo).toLowerCase().includes(termo)),
  );
  const servicosVisiveis = servicos.filter((item) =>
    !termo
      ? true
      : [item.nome, item.codigoExterno, codigoCurto(item.id)]
          .filter(Boolean)
          .some((campo) => String(campo).toLowerCase().includes(termo)),
  );

  const ehServico = criando ? tipoModal === "servico" : editandoServico != null;
  const editando = editandoProduto ?? editandoServico;
  const tituloModal = criando
    ? ehServico
      ? "Novo serviço"
      : "Novo produto"
    : ehServico
      ? "Editar serviço"
      : "Editar produto";

  return (
    <AppShell titulo="Produtos / Serviços">
      <p>
        Os campos da linha da DANFE (NCM/SH, CSOSN, CFOP, unidade, valores e ICMS/IPI) valem para
        produto e para serviço quando a nota fiscal traz essa grade. A quantidade fica no item da
        compra, porque muda a cada documento.
      </p>
      {reciboId ? <p className="muted">Filtro do recibo {codigoCurto(reciboId)}.</p> : null}
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}

      <div className="toolbar cadastro-toolbar">
        <label className="cadastro-busca">
          Buscar
          <input
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
            placeholder="Nome, código ou marca"
          />
        </label>
        <button className="primary-button" type="button" onClick={abrirNovo}>
          Novo {tipoLista === "servico" ? "serviço" : "produto"}
        </button>
      </div>

      <div className="tabs" role="tablist" aria-label="Cadastros">
        <button
          className={tipoLista === "produto" ? "tab is-active" : "tab"}
          type="button"
          onClick={() => setTipoLista("produto")}
        >
          Produtos ({produtos.length})
        </button>
        <button
          className={tipoLista === "servico" ? "tab is-active" : "tab"}
          type="button"
          onClick={() => setTipoLista("servico")}
        >
          Serviços ({servicos.length})
        </button>
      </div>

      {tipoLista === "produto" ? (
        produtosVisiveis.length === 0 ? (
          <p className="muted">Nenhum produto encontrado. Use Novo produto para incluir um cadastro.</p>
        ) : (
          <div className="table-scroll">
            <table className="data-table produtos-tabela">
              <thead>
                <CabecalhoFiscal />
              </thead>
              {produtosVisiveis.map((p) => (
                <LinhaCadastro
                  key={p.id}
                  linha={p}
                  onEditar={() => abrirProduto(p)}
                  onExcluir={() => void excluirProduto(p)}
                  excluindo={excluindo === p.id}
                />
              ))}
            </table>
          </div>
        )
      ) : servicosVisiveis.length === 0 ? (
        <p className="muted">Nenhum serviço encontrado. Use Novo serviço para incluir um cadastro.</p>
      ) : (
        <div className="table-scroll">
          <table className="data-table produtos-tabela">
            <thead>
              <CabecalhoFiscal />
            </thead>
            {servicosVisiveis.map((s) => (
              <LinhaCadastro
                key={s.id}
                linha={s}
                onEditar={() => abrirServico(s)}
                onExcluir={() => void excluirServico(s)}
                excluindo={excluindo === s.id}
              />
            ))}
          </table>
        </div>
      )}

      <dialog ref={dialogRef} className="modal modal-wide modal-cadastro" onClose={aoFecharModal}>
        <button className="close" type="button" aria-label="Fechar" onClick={fecharEdicao}>
          ×
        </button>
        <h2>{tituloModal}</h2>
        <p>
          {criando
            ? "Inclua o cadastro completo. Unidade e Recibo / NF usam lookup das opções do sistema."
            : "Atualize os dados deste cadastro. O código interno não pode ser alterado."}
        </p>
        {editando ? <p className="muted">Código: {codigoCurto(editando.id)}</p> : null}
        {erroModal ? (
          <p className="feedback" role="alert">
            {erroModal}
          </p>
        ) : null}

        <div className="tabs" role="tablist" aria-label="Campos do cadastro">
          <button
            className={abaModal === "identificacao" ? "tab is-active" : "tab"}
            type="button"
            onClick={() => setAbaModal("identificacao")}
          >
            Identificação
          </button>
          <button
            className={abaModal === "classificacao" ? "tab is-active" : "tab"}
            type="button"
            onClick={() => setAbaModal("classificacao")}
          >
            Classificação fiscal
          </button>
          <button
            className={abaModal === "valores" ? "tab is-active" : "tab"}
            type="button"
            onClick={() => setAbaModal("valores")}
          >
            Valores e impostos
          </button>
        </div>

        <form className="form-grid modal-form" onSubmit={(e) => void salvar(e)}>
          {abaModal === "identificacao" ? (
            <>
              <label>
                Nome
                <input
                  value={form.nome}
                  onChange={(e) => setForm((atual) => ({ ...atual, nome: e.target.value }))}
                  required
                  maxLength={200}
                />
              </label>
              {!ehServico ? (
                <label>
                  Marca
                  <input
                    value={form.marca}
                    onChange={(e) => setForm((atual) => ({ ...atual, marca: e.target.value }))}
                  />
                </label>
              ) : null}
              {!ehServico ? (
                <label>
                  Variante
                  <input
                    value={form.variante}
                    onChange={(e) => setForm((atual) => ({ ...atual, variante: e.target.value }))}
                  />
                </label>
              ) : null}
              <label>
                Unidade
                <select
                  value={form.unidadeControle}
                  onChange={(e) => setForm((atual) => ({ ...atual, unidadeControle: e.target.value }))}
                >
                  {UNIDADES_CONTROLE.map((unidade) => (
                    <option key={unidade} value={unidade}>
                      {unidade.toUpperCase()}
                    </option>
                  ))}
                  {form.unidadeControle &&
                  !UNIDADES_CONTROLE.includes(form.unidadeControle as (typeof UNIDADES_CONTROLE)[number]) ? (
                    <option value={form.unidadeControle}>{form.unidadeControle}</option>
                  ) : null}
                </select>
              </label>
              <label>
                Recibo / NF
                <select
                  value={form.reciboId}
                  onChange={(e) => setForm((atual) => ({ ...atual, reciboId: e.target.value }))}
                >
                  <option value="">Sem vínculo</option>
                  {recibos.map((recibo) => (
                    <option key={recibo.id} value={recibo.id}>
                      {rotuloReciboOpcao(recibo)}
                    </option>
                  ))}
                </select>
              </label>
              {!ehServico ? (
                <label>
                  Embalagem
                  <input
                    value={form.conteudoEmbalagem}
                    onChange={(e) => setForm((atual) => ({ ...atual, conteudoEmbalagem: e.target.value }))}
                    inputMode="decimal"
                  />
                </label>
              ) : null}
              <label>
                Código do produto/serviço
                <input
                  value={form.codigoExterno}
                  onChange={(e) => setForm((atual) => ({ ...atual, codigoExterno: e.target.value }))}
                />
              </label>
              {!ehServico ? (
                <label className="span-2">
                  Sinônimos
                  <textarea
                    rows={3}
                    value={form.sinonimos}
                    onChange={(e) => setForm((atual) => ({ ...atual, sinonimos: e.target.value }))}
                    placeholder="Separe por vírgula"
                  />
                </label>
              ) : null}
            </>
          ) : null}

          {abaModal === "classificacao" ? (
            <>
              <p className="span-2 muted">
                Classificação da linha da DANFE, usada tanto em mercadoria quanto em serviço prestado
                na NF-e.
              </p>
              <label>
                NCM/SH
                <input value={form.ncmSh} onChange={(e) => setForm((atual) => ({ ...atual, ncmSh: e.target.value }))} />
              </label>
              <label>
                CSOSN
                <input value={form.csosn} onChange={(e) => setForm((atual) => ({ ...atual, csosn: e.target.value }))} />
              </label>
              <label>
                CFOP
                <input value={form.cfop} onChange={(e) => setForm((atual) => ({ ...atual, cfop: e.target.value }))} />
              </label>
            </>
          ) : null}

          {abaModal === "valores" ? (
            <>
              <p className="span-2 muted">
                Valores e tributos da linha da NF-e. ICMS e IPI aparecem na DANFE de produto e também
                quando o serviço é lançado nesse layout. Quantidade não entra no cadastro: ela fica no
                item da compra.
              </p>
              <label>
                Valor unitário
                <input
                  value={form.valorUnitario}
                  onChange={(e) => setForm((atual) => ({ ...atual, valorUnitario: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Valor desconto
                <input
                  value={form.valorDesconto}
                  onChange={(e) => setForm((atual) => ({ ...atual, valorDesconto: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Valor líquido
                <input
                  value={form.valorLiquido}
                  onChange={(e) => setForm((atual) => ({ ...atual, valorLiquido: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Base de cálculo ICMS
                <input
                  value={form.baseIcms}
                  onChange={(e) => setForm((atual) => ({ ...atual, baseIcms: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Valor ICMS
                <input
                  value={form.valorIcms}
                  onChange={(e) => setForm((atual) => ({ ...atual, valorIcms: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Valor IPI
                <input
                  value={form.valorIpi}
                  onChange={(e) => setForm((atual) => ({ ...atual, valorIpi: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Alíquota ICMS %
                <input
                  value={form.aliqIcms}
                  onChange={(e) => setForm((atual) => ({ ...atual, aliqIcms: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
              <label>
                Alíquota IPI %
                <input
                  value={form.aliqIpi}
                  onChange={(e) => setForm((atual) => ({ ...atual, aliqIpi: e.target.value }))}
                  inputMode="decimal"
                />
              </label>
            </>
          ) : null}

          <div className="toolbar span-2">
            <button className="primary-button" type="submit" disabled={salvando}>
              {salvando ? "Salvando…" : criando ? "Incluir" : "Salvar"}
            </button>
            <button className="ghost-button dark" type="button" onClick={fecharEdicao}>
              Cancelar
            </button>
          </div>
        </form>
      </dialog>
    </AppShell>
  );
}
