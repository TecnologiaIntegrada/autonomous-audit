import { apiBaseUrl, apiJson, lerJwt, ApiError } from "@/lib/api";
import type { NfeAdicionais, NfeDestinatario, NfeImposto, NfeTransportador } from "@/lib/recibos";

export async function apiForm<T>(
  path: string,
  form: FormData,
  headers?: Record<string, string>,
): Promise<T> {
  const h = new Headers(headers);
  const jwt = lerJwt();
  if (jwt && !h.has("Authorization")) {
    h.set("Authorization", `Bearer ${jwt}`);
  }
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: "POST",
    headers: h,
    body: form,
  });
  if (!response.ok) {
    const text = await response.text();
    throw new ApiError(response.status, text || `Falha ${response.status}`);
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export type CompraLista = {
  id: string;
  codigo?: string;
  status: string;
  dataEnvio: string;
  dataCompra?: string | null;
  numeroRecibo?: string | null;
  tipoDocumento?: string | null;
  tipoItem?: string | null;
  danfeTipo?: number | null;
  serie?: string | null;
  chaveAcesso?: string | null;
  subtotal?: number | null;
  descontos?: number | null;
  acrescimos?: number | null;
  total?: number | null;
  formaPagamento?: string | null;
  fornecedorNome?: string | null;
  fornecedorRazaoSocial?: string | null;
  fornecedorCpfCnpj?: string | null;
  fornecedorTelefone?: string | null;
  fornecedorEndereco?: string | null;
  paginas?: string | null;
  reciboOrigemId?: string | null;
  arquivoUrl?: string | null;
  ultimoErro?: string | null;
};

export type CompraAnexo = {
  id: string;
  ordem: number;
  nomeArquivo: string;
  mime: string;
  origem: string;
  tamanhoBytes: number;
};

export type CompraItem = {
  id: string;
  ordem: number;
  descricaoOriginal: string;
  codigoImpresso?: string | null;
  quantidade: number;
  unidade?: string | null;
  precoUnitario?: number | null;
  desconto?: number | null;
  total?: number | null;
  produtoId?: string | null;
  produtoNome?: string | null;
  produtoMarca?: string | null;
  produtoVariante?: string | null;
  servicoId?: string | null;
  servicoNome?: string | null;
  tipoItem?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorLiquido?: number | null;
  sugestaoConteudoEmbalagem?: number | null;
  sugestaoQuantidadeConvertida?: number | null;
  sugestaoUnidadeControle?: string | null;
  statusEstoque: string;
};

export type CompraDetalhe = CompraLista & {
  subtotal?: number | null;
  descontos?: number | null;
  acrescimos?: number | null;
  formaPagamento?: string | null;
  fornecedorId?: string | null;
  fornecedorCpfCnpj?: string | null;
  fornecedorRazaoSocial?: string | null;
  fornecedorEndereco?: string | null;
  fornecedorTelefone?: string | null;
  paginas?: string | null;
  reciboOrigemId?: string | null;
  arquivoUrl?: string | null;
  hashArquivo?: string | null;
  geminiJson?: unknown;
  qwenJson?: unknown;
  perplexityJson?: unknown;
  divergencias?: { alertas?: string[] } | null;
  anexos: CompraAnexo[];
  itens: CompraItem[];
  duplicatas: CompraLista[];
  folha?: string | null;
  codigoBarras?: string | null;
  protocoloAutorizacao?: string | null;
  protocoloData?: string | null;
  naturezaOperacao?: string | null;
  inscricaoEstadual?: string | null;
  inscricaoEstadualSt?: string | null;
  dataEmissao?: string | null;
  nfeDestinatario?: NfeDestinatario | null;
  nfeImposto?: NfeImposto | null;
  nfeTransportador?: NfeTransportador | null;
  nfeAdicionais?: NfeAdicionais | null;
};

export type ComprasResumo = {
  totalCompras: number;
  rascunhos: number;
  processando: number;
  emRevisao: number;
  validadas: number;
  concluidas: number;
  falhas: number;
  itensEstoquePendentes: number;
  totalGasto: number;
  porFornecedor: { fornecedorId?: string | null; nome: string; quantidade: number; total: number }[];
};

export type CapturaCriada = {
  url: string;
  expiraEm: string;
  qrPngBase64: string;
};

export type CapturaStatus = {
  ativa: boolean;
  consumida: boolean;
  concluida?: boolean;
  expiraEm?: string | null;
  anexos: number;
};

export type EstoqueFila = {
  itemId: string;
  compraId: string;
  descricao: string;
  quantidade: number;
  unidade?: string | null;
  statusEstoque: string;
  produtoNome?: string | null;
  fornecedorNome?: string | null;
  dataCompra?: string | null;
  sugestaoQuantidadeConvertida?: number | null;
  sugestaoUnidadeControle?: string | null;
};

export type Fornecedor = {
  id: string;
  nome: string;
  razaoSocial?: string | null;
  cpfCnpj?: string | null;
  telefone?: string | null;
  endereco?: string | null;
};

export type Produto = {
  id: string;
  nome: string;
  marca?: string | null;
  variante?: string | null;
  unidadeControle: string;
  conteudoEmbalagem?: number | null;
  sinonimos: string[];
};

export function statusCompraLabel(status: string) {
  const mapa: Record<string, string> = {
    rascunho: "Rascunho",
    processando: "Processando",
    processado: "Lançada",
    revisao: "Em revisão",
    validada: "Validada",
    concluida: "Concluída",
    falha_processamento: "Falha",
  };
  return mapa[status] ?? status;
}

export const comprasApi = {
  criar: () => apiJson<CompraLista>("/v1/compras", { method: "POST" }),
  listar: (status?: string) =>
    apiJson<CompraLista[]>(`/v1/compras${status ? `?status=${encodeURIComponent(status)}` : ""}`),
  obter: (id: string) => apiJson<CompraDetalhe>(`/v1/compras/${id}`),
  resumo: () => apiJson<ComprasResumo>("/v1/compras/resumo"),
  anexar: (id: string, arquivo: File) => {
    const form = new FormData();
    form.append("arquivo", arquivo);
    return apiForm<CompraAnexo>(`/v1/compras/${id}/anexos`, form);
  },
  patchAnexos: (id: string, body: { ordem?: string[]; remover?: string[] }) =>
    apiJson<CompraDetalhe>(`/v1/compras/${id}/anexos`, {
      method: "PATCH",
      body: JSON.stringify(body),
    }),
  captura: (id: string) =>
    apiJson<CapturaCriada>(`/v1/compras/${id}/captura`, { method: "POST", body: "{}" }),
  capturaStatus: (id: string) => apiJson<CapturaStatus>(`/v1/compras/${id}/captura`),
  processar: (id: string) =>
    apiJson<CompraLista>(`/v1/compras/${id}/processar`, { method: "POST", body: "{}" }),
  reprocessar: (id: string) =>
    apiJson<CompraLista>(`/v1/compras/${id}/reprocessar`, { method: "POST", body: "{}" }),
  validar: (id: string, body: unknown) =>
    apiJson<CompraDetalhe>(`/v1/compras/${id}/validacao`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  documento: (id: string) => apiJson<{ url: string }>(`/v1/compras/${id}/documento`),
  estoqueFila: (status?: string) =>
    apiJson<EstoqueFila[]>(`/v1/estoque/fila${status ? `?status=${encodeURIComponent(status)}` : ""}`),
  confirmarItem: (itemId: string) =>
    apiJson<{ id: string; statusEstoque: string }>(`/v1/estoque/itens/${itemId}/confirmar`, {
      method: "POST",
      body: "{}",
    }),
  dispensarItem: (itemId: string) =>
    apiJson<{ id: string; statusEstoque: string }>(`/v1/estoque/itens/${itemId}/dispensar`, {
      method: "POST",
      body: "{}",
    }),
  fornecedores: (busca?: string) =>
    apiJson<Fornecedor[]>(`/v1/fornecedores${busca ? `?busca=${encodeURIComponent(busca)}` : ""}`),
  criarFornecedor: (body: Omit<Fornecedor, "id">) =>
    apiJson<Fornecedor>("/v1/fornecedores", { method: "POST", body: JSON.stringify(body) }),
  produtos: (busca?: string) =>
    apiJson<Produto[]>(`/v1/produtos${busca ? `?busca=${encodeURIComponent(busca)}` : ""}`),
  criarProduto: (body: { nome: string; marca?: string; unidadeControle?: string; sinonimo?: string }) =>
    apiJson<Produto>("/v1/produtos", { method: "POST", body: JSON.stringify(body) }),
};
