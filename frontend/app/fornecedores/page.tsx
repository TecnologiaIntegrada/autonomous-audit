"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { AppShell } from "@/components/AppShell";
import { ApiError, apiJson, type EnderecoCep } from "@/lib/api";
import {
  codigoCurto,
  recibosApi,
  rotuloTipoDocumento,
  rotuloTipoItem,
  statusReciboLabel,
  type Fornecedor,
  type ReciboLista,
} from "@/lib/recibos";

type Formulario = {
  nome: string;
  razaoSocial: string;
  cpfCnpj: string;
  telefone: string;
  cep: string;
  endereco: string;
};

type AbaCadastro = "dados" | "recibos";

const formularioVazio: Formulario = {
  nome: "",
  razaoSocial: "",
  cpfCnpj: "",
  telefone: "",
  cep: "",
  endereco: "",
};

function soDigitos(valor: string) {
  return valor.replace(/\D/g, "");
}

function formatarMoeda(valor?: number | null) {
  if (valor == null) {
    return "—";
  }
  return valor.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export default function FornecedoresPage() {
  const [lista, setLista] = useState<Fornecedor[]>([]);
  const [reciboIdFiltro, setReciboIdFiltro] = useState("");
  const [busca, setBusca] = useState("");
  const [erro, setErro] = useState("");
  const [erroModal, setErroModal] = useState("");
  const [salvando, setSalvando] = useState(false);
  const [excluindo, setExcluindo] = useState<string | null>(null);
  const [pesquisando, setPesquisando] = useState(false);
  const [buscandoCep, setBuscandoCep] = useState(false);
  const [avisoPesquisa, setAvisoPesquisa] = useState("");
  const [editando, setEditando] = useState<Fornecedor | null>(null);
  const [criando, setCriando] = useState(false);
  const [form, setForm] = useState<Formulario>(formularioVazio);
  const [aba, setAba] = useState<AbaCadastro>("dados");
  const [recibosFornecedor, setRecibosFornecedor] = useState<ReciboLista[]>([]);
  const [carregandoRecibos, setCarregandoRecibos] = useState(false);
  const dialogRef = useRef<HTMLDialogElement>(null);

  async function carregar(filtro?: string) {
    const dados = await recibosApi.fornecedores(undefined, filtro || undefined);
    setLista([...dados].sort((a, b) => a.nome.localeCompare(b.nome, "pt-BR")));
  }

  useEffect(() => {
    const filtro = new URLSearchParams(window.location.search).get("reciboId") ?? "";
    setReciboIdFiltro(filtro);
    void carregar(filtro).catch((falha) =>
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível listar fornecedores e prestadores."),
    );
  }, []);

  const visiveis = lista.filter((item) => {
    const termo = busca.trim().toLowerCase();
    if (!termo) {
      return true;
    }
    return [item.nome, item.razaoSocial, item.cpfCnpj, codigoCurto(item.id)]
      .filter(Boolean)
      .some((campo) => String(campo).toLowerCase().includes(termo));
  });

  function abrirNovo() {
    setCriando(true);
    setEditando(null);
    setErroModal("");
    setAvisoPesquisa("");
    setForm({ ...formularioVazio });
    setAba("dados");
    setRecibosFornecedor([]);
    dialogRef.current?.showModal();
  }

  function abrirEdicao(fornecedor: Fornecedor, abaInicial: AbaCadastro = "dados") {
    setCriando(false);
    setEditando(fornecedor);
    setErroModal("");
    setAvisoPesquisa("");
    setForm({
      nome: fornecedor.nome ?? "",
      razaoSocial: fornecedor.razaoSocial ?? "",
      cpfCnpj: fornecedor.cpfCnpj ?? "",
      telefone: fornecedor.telefone ?? "",
      cep: "",
      endereco: fornecedor.endereco ?? "",
    });
    setAba(abaInicial);
    setRecibosFornecedor([]);
    dialogRef.current?.showModal();
    void carregarRecibos(fornecedor.id);
  }

  async function carregarRecibos(id: string) {
    setCarregandoRecibos(true);
    try {
      const recibos = await recibosApi.recibosDoFornecedor(id);
      setRecibosFornecedor(recibos);
    } catch (falha) {
      setErroModal(falha instanceof ApiError ? falha.message : "Não foi possível listar os recibos deste cadastro.");
    } finally {
      setCarregandoRecibos(false);
    }
  }

  function fecharModal() {
    dialogRef.current?.close();
  }

  function aoFecharModal() {
    setEditando(null);
    setCriando(false);
    setErroModal("");
    setAvisoPesquisa("");
    setForm(formularioVazio);
    setAba("dados");
    setRecibosFornecedor([]);
  }

  function corpoCadastro() {
    return {
      nome: form.nome.trim(),
      razaoSocial: form.razaoSocial.trim() || null,
      cpfCnpj: form.cpfCnpj.trim() || null,
      telefone: form.telefone.trim() || null,
      endereco: form.endereco.trim() || null,
    };
  }

  async function salvar(evento: FormEvent) {
    evento.preventDefault();
    if (!criando && !editando) {
      return;
    }
    setSalvando(true);
    setErroModal("");
    try {
      const corpo = corpoCadastro();
      if (criando) {
        const criado = await recibosApi.criarFornecedor(corpo);
        setLista((atual) => [...atual, criado].sort((a, b) => a.nome.localeCompare(b.nome, "pt-BR")));
      } else if (editando) {
        const atualizado = await recibosApi.atualizarFornecedor(editando.id, corpo);
        setLista((atual) =>
          [...atual.map((item) => (item.id === atualizado.id ? { ...item, ...atualizado } : item))].sort((a, b) =>
            a.nome.localeCompare(b.nome, "pt-BR"),
          ),
        );
      }
      fecharModal();
    } catch (falha) {
      setErroModal(falha instanceof ApiError ? falha.message : "Não foi possível salvar o cadastro.");
    } finally {
      setSalvando(false);
    }
  }

  async function excluir(fornecedor: Fornecedor) {
    if (!window.confirm(`Excluir ${fornecedor.nome}? Esta ação não pode ser desfeita.`)) {
      return;
    }
    setExcluindo(fornecedor.id);
    setErro("");
    try {
      await recibosApi.excluirFornecedor(fornecedor.id);
      setLista((atual) => atual.filter((item) => item.id !== fornecedor.id));
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível excluir o cadastro.");
    } finally {
      setExcluindo(null);
    }
  }

  async function buscarCep() {
    const cep = soDigitos(form.cep);
    if (cep.length !== 8) {
      setErroModal("Informe um CEP com 8 dígitos.");
      return;
    }
    setBuscandoCep(true);
    setErroModal("");
    try {
      const encontrado = await apiJson<EnderecoCep>(`/v1/enderecos/cep/${cep}`);
      const partes = [encontrado.logradouro, encontrado.bairro, encontrado.cidade, encontrado.uf]
        .filter(Boolean)
        .join(", ");
      setForm((atual) => ({
        ...atual,
        cep: encontrado.cep || atual.cep,
        endereco: partes || atual.endereco,
      }));
      setAvisoPesquisa("Endereço preenchido pelo CEP. Confira antes de salvar.");
    } catch (falha) {
      setErroModal(falha instanceof ApiError ? falha.message : "CEP não encontrado.");
    } finally {
      setBuscandoCep(false);
    }
  }

  async function pesquisar() {
    if (!form.nome.trim()) {
      setErroModal("Informe o nome para pesquisar.");
      return;
    }
    setPesquisando(true);
    setErroModal("");
    setAvisoPesquisa("");
    try {
      const sugestao = await recibosApi.completarFornecedor(form.nome.trim());
      setForm((atual) => ({
        ...atual,
        razaoSocial: atual.razaoSocial.trim() ? atual.razaoSocial : sugestao.razaoSocial?.trim() || "",
        cpfCnpj: atual.cpfCnpj.trim() ? atual.cpfCnpj : sugestao.cpfCnpj?.trim() || "",
        telefone: atual.telefone.trim() ? atual.telefone : sugestao.telefone?.trim() || "",
        endereco: atual.endereco.trim() ? atual.endereco : sugestao.endereco?.trim() || "",
      }));
      const campos = sugestao.camposPreenchidos?.length
        ? sugestao.camposPreenchidos.join(", ")
        : "campos vazios";
      setAvisoPesquisa(`Pesquisa concluída. Preenchidos: ${campos}. Confira antes de salvar.`);
    } catch (falha) {
      setErroModal(falha instanceof ApiError ? falha.message : "Não foi possível pesquisar o fornecedor.");
    } finally {
      setPesquisando(false);
    }
  }

  const ocupado = salvando || pesquisando || buscandoCep;
  const tituloModal = criando ? "Novo cadastro" : "Editar cadastro";

  return (
    <AppShell titulo="Fornecedores / Prestadores">
      {reciboIdFiltro ? <p className="muted">Filtro do recibo {codigoCurto(reciboIdFiltro)}.</p> : null}
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
            placeholder="Nome, razão social, CPF/CNPJ ou código"
          />
        </label>
        <button className="primary-button" type="button" onClick={abrirNovo}>
          Novo
        </button>
      </div>

      {visiveis.length === 0 ? (
        <p className="muted">Nenhum cadastro encontrado. Use Novo para incluir um fornecedor ou prestador.</p>
      ) : (
        <table className="data-table">
          <thead>
            <tr>
              <th>Código</th>
              <th>Nome</th>
              <th>Razão social</th>
              <th>Recibos / NF</th>
              <th>Ações</th>
            </tr>
          </thead>
          <tbody>
            {visiveis.map((f) => (
              <tr key={f.id}>
                <td>{codigoCurto(f.id)}</td>
                <td>{f.nome}</td>
                <td>{f.razaoSocial ?? "—"}</td>
                <td>
                  <button
                    className="ghost-button dark"
                    type="button"
                    onClick={() => abrirEdicao(f, "recibos")}
                  >
                    {f.quantidadeRecibos ?? 0}
                  </button>
                </td>
                <td>
                  <div className="acoes-cadastro">
                    <button className="ghost-button dark" type="button" onClick={() => abrirEdicao(f)}>
                      Editar
                    </button>
                    <button
                      className="danger-button"
                      type="button"
                      disabled={excluindo === f.id}
                      onClick={() => void excluir(f)}
                    >
                      {excluindo === f.id ? "Excluindo…" : "Excluir"}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <dialog ref={dialogRef} className="modal modal-wide modal-cadastro" onClose={aoFecharModal}>
        <button className="close" type="button" aria-label="Fechar" onClick={fecharModal}>
          ×
        </button>
        <h2>{tituloModal}</h2>
        <p>
          {criando
            ? "Inclua um fornecedor ou prestador de serviços. Recibos e notas fiscais são vinculados automaticamente na captura."
            : "Atualize os dados deste fornecedor ou prestador e consulte os recibos vinculados."}
        </p>
        {editando ? <p className="muted">Código: {codigoCurto(editando.id)}</p> : null}
        {erroModal ? (
          <p className="feedback" role="alert">
            {erroModal}
          </p>
        ) : null}
        {avisoPesquisa ? (
          <p className="feedback ok" role="status">
            {avisoPesquisa}
          </p>
        ) : null}

        <div className="tabs" role="tablist" aria-label="Cadastro do fornecedor">
          <button
            className={`tab${aba === "dados" ? " is-active" : ""}`}
            type="button"
            role="tab"
            aria-selected={aba === "dados"}
            onClick={() => setAba("dados")}
          >
            Dados
          </button>
          <button
            className={`tab${aba === "recibos" ? " is-active" : ""}`}
            type="button"
            role="tab"
            aria-selected={aba === "recibos"}
            onClick={() => setAba("recibos")}
          >
            Recibos
          </button>
        </div>

        {aba === "dados" ? (
          <form className="form-grid modal-form" onSubmit={(e) => void salvar(e)}>
            <label>
              Nome
              <input
                value={form.nome}
                onChange={(e) => setForm((atual) => ({ ...atual, nome: e.target.value }))}
                required
                maxLength={200}
              />
            </label>
            <label>
              Razão social
              <input
                value={form.razaoSocial}
                onChange={(e) => setForm((atual) => ({ ...atual, razaoSocial: e.target.value }))}
              />
            </label>
            <label>
              CPF/CNPJ
              <input
                value={form.cpfCnpj}
                onChange={(e) => setForm((atual) => ({ ...atual, cpfCnpj: e.target.value }))}
              />
            </label>
            <label>
              Telefone
              <input
                value={form.telefone}
                onChange={(e) => setForm((atual) => ({ ...atual, telefone: e.target.value }))}
              />
            </label>
            <label>
              CEP
              <span className="campo-lookup">
                <input
                  value={form.cep}
                  onChange={(e) => setForm((atual) => ({ ...atual, cep: e.target.value }))}
                  inputMode="numeric"
                  placeholder="00000-000"
                />
                <button className="ghost-button dark" type="button" disabled={ocupado} onClick={() => void buscarCep()}>
                  {buscandoCep ? "Buscando…" : "Buscar"}
                </button>
              </span>
            </label>
            <label className="span-2">
              Endereço
              <textarea
                rows={3}
                value={form.endereco}
                onChange={(e) => setForm((atual) => ({ ...atual, endereco: e.target.value }))}
              />
            </label>
            <div className="toolbar span-2">
              <button className="primary-button" type="submit" disabled={ocupado}>
                {salvando ? "Salvando…" : criando ? "Incluir" : "Salvar"}
              </button>
              <button
                className="ghost-button dark"
                type="button"
                disabled={ocupado || !form.nome.trim()}
                title="Pesquisar dados da empresa na internet"
                aria-label="Pesquisar dados da empresa na internet"
                onClick={() => void pesquisar()}
              >
                {pesquisando ? "Pesquisando…" : "Pesquisar na internet"}
              </button>
              <button className="ghost-button dark" type="button" disabled={ocupado} onClick={fecharModal}>
                Cancelar
              </button>
            </div>
          </form>
        ) : (
          <div>
            {criando ? (
              <p className="muted">Inclua o cadastro para vincular recibos e notas fiscais a este fornecedor.</p>
            ) : carregandoRecibos ? (
              <p className="muted">Carregando recibos…</p>
            ) : recibosFornecedor.length === 0 ? (
              <p className="muted">Nenhum recibo ou nota fiscal vinculado a este cadastro.</p>
            ) : (
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Entrada</th>
                      <th>Código</th>
                      <th>Documento</th>
                      <th>Número</th>
                      <th>Total</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {recibosFornecedor.map((recibo) => (
                      <tr key={recibo.id}>
                        <td>{new Date(recibo.dataEnvio).toLocaleString("pt-BR")}</td>
                        <td>
                          <a href={`/recibos/${recibo.id}`}>{codigoCurto(recibo.codigo ?? recibo.id)}</a>
                        </td>
                        <td>
                          {rotuloTipoDocumento(recibo.tipoDocumento)} · {rotuloTipoItem(recibo.tipoItem)}
                        </td>
                        <td>{recibo.numeroRecibo ?? "—"}</td>
                        <td>{formatarMoeda(recibo.total)}</td>
                        <td>{statusReciboLabel(recibo.status)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <div className="toolbar" style={{ marginTop: 16 }}>
              <button className="ghost-button dark" type="button" onClick={fecharModal}>
                Fechar
              </button>
            </div>
          </div>
        )}
      </dialog>
    </AppShell>
  );
}
