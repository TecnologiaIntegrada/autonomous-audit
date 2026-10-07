import { apiBaseUrl, apiJson, lerJwt, ApiError } from "@/lib/api";

export const RECIBO_MAX_BYTES = 50 * 1024 * 1024;
export const RECIBO_ACCEPT = "image/jpeg,image/png,image/webp,image/heic,image/gif,application/pdf";
export const RECIBO_FORMATOS =
  "Formatos aceitos: JPEG, PNG, WebP, HEIC, GIF e PDF (até 50 MB cada).";

const TIPOS_OK = [
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/heic",
  "image/gif",
  "application/pdf",
];

export function validarArquivoRecibo(file: File): string | null {
  const nome = file.name.toLowerCase();
  const tipo = (file.type || "").toLowerCase();
  const okTipo =
    TIPOS_OK.includes(tipo) ||
    tipo.startsWith("image/") ||
    nome.endsWith(".pdf") ||
    nome.endsWith(".jpg") ||
    nome.endsWith(".jpeg") ||
    nome.endsWith(".png") ||
    nome.endsWith(".webp") ||
    nome.endsWith(".heic") ||
    nome.endsWith(".gif");
  if (!okTipo) {
    return `${file.name}: envie somente imagens ou PDF.`;
  }
  if (file.size > RECIBO_MAX_BYTES) {
    return `${file.name}: cada arquivo deve ter no máximo 50 MB.`;
  }
  return null;
}

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

export type ReciboLista = {
  id: string;
  codigo: string;
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
  fornecedorId?: string | null;
  fornecedorNome?: string | null;
  ultimoErro?: string | null;
  geminiJson?: unknown;
  qwenJson?: unknown;
  perplexityJson?: unknown;
};

export type ReciboAnexo = {
  id: string;
  ordem: number;
  nomeArquivo: string;
  mime: string;
  origem: string;
  tamanhoBytes: number;
};

export type ReciboItem = {
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
  servicoId?: string | null;
  servicoNome?: string | null;
  tipoItem?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
  statusEstoque: string;
};

export type ReciboDetalhe = ReciboLista & {
  subtotal?: number | null;
  descontos?: number | null;
  acrescimos?: number | null;
  formaPagamento?: string | null;
  fornecedorId?: string | null;
  fornecedorCpfCnpj?: string | null;
  fornecedorEndereco?: string | null;
  fornecedorTelefone?: string | null;
  hashArquivo?: string | null;
  divergencias?: { alertas?: string[] } | null;
  anexos: ReciboAnexo[];
  itens: ReciboItem[];
  duplicatas: ReciboLista[];
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

export type NfeDestinatario = {
  nomeRazaoSocial?: string | null;
  cpfCnpj?: string | null;
  endereco?: string | null;
  bairro?: string | null;
  cep?: string | null;
  municipio?: string | null;
  uf?: string | null;
  telefone?: string | null;
  inscricaoEstadual?: string | null;
  dataEmissao?: string | null;
  dataSaida?: string | null;
  horaSaida?: string | null;
};

export type NfeImposto = {
  baseIcms?: number | null;
  valorIcms?: number | null;
  baseIcmsSt?: number | null;
  valorIcmsSt?: number | null;
  valorTotalProdutos?: number | null;
  valorFrete?: number | null;
  valorSeguro?: number | null;
  desconto?: number | null;
  outrasDespesas?: number | null;
  valorIpi?: number | null;
  valorTotalNota?: number | null;
};

export type NfeTransportador = {
  nomeRazaoSocial?: string | null;
  fretePorConta?: string | null;
  codigoAntt?: string | null;
  placa?: string | null;
  uf?: string | null;
  cpfCnpj?: string | null;
  endereco?: string | null;
  municipio?: string | null;
  ufEndereco?: string | null;
  inscricaoEstadual?: string | null;
  quantidadeVolumes?: number | null;
  especie?: string | null;
  marca?: string | null;
  numeracao?: string | null;
  pesoBruto?: number | null;
  pesoLiquido?: number | null;
};

export type NfeAdicionais = {
  informacoesComplementares?: string | null;
  reservadoAoFisco?: string | null;
  dataHoraImpressao?: string | null;
};

export type RecibosResumo = {
  totalCompras: number;
  rascunhos: number;
  processando: number;
  emRevisao: number;
  validadas: number;
  concluidas: number;
  falhas: number;
  itensEstoquePendentes: number;
  totalGasto: number;
  processados: number;
  armazenamentoUsadoBytes?: number;
  armazenamentoLimiteBytes?: number;
  geminiTokensTotal?: number;
  geminiCustoTotalBrl?: number;
  qwenTokensTotal?: number;
  qwenCustoTotalBrl?: number;
  perplexityTokensTotal?: number;
  perplexityCustoTotalBrl?: number;
  ano?: number;
  mes?: number;
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

export type Fornecedor = {
  id: string;
  nome: string;
  razaoSocial?: string | null;
  cpfCnpj?: string | null;
  telefone?: string | null;
  endereco?: string | null;
  quantidadeRecibos?: number;
};

export type FornecedorCompletar = {
  razaoSocial?: string | null;
  cpfCnpj?: string | null;
  telefone?: string | null;
  endereco?: string | null;
  citacoes: string[];
  camposPreenchidos: string[];
};

export type Produto = {
  id: string;
  nome: string;
  marca?: string | null;
  variante?: string | null;
  unidadeControle: string;
  conteudoEmbalagem?: number | null;
  sinonimos: string[];
  reciboId?: string | null;
  codigoExterno?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorUnitario?: number | null;
  valorDesconto?: number | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
};

export type Servico = {
  id: string;
  nome: string;
  unidadeControle: string;
  reciboId?: string | null;
  codigoExterno?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorUnitario?: number | null;
  valorDesconto?: number | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
};

export function rotuloTipoDocumento(valor?: string | null) {
  return valor === "nota_fiscal" ? "Nota fiscal" : "Recibo";
}

export function rotuloTipoItem(valor?: string | null) {
  if (valor === "servico") return "Serviço";
  if (valor === "misto") return "Produto e serviço";
  return "Produto";
}

export function ehNotaServico(tipoDocumento?: string | null, tipoItem?: string | null) {
  return tipoDocumento === "nota_fiscal" && tipoItem === "servico";
}

export function rotuloContraparte(tipoDocumento?: string | null, tipoItem?: string | null) {
  return ehNotaServico(tipoDocumento, tipoItem) ? "Prestador de serviços" : "Fornecedor";
}

export function rotuloContraparteLista() {
  return "Fornecedor / Prestador";
}

export function rotuloParteNota(tipoDocumento?: string | null, tipoItem?: string | null) {
  return ehNotaServico(tipoDocumento, tipoItem) ? "Tomador de serviços" : "Destinatário / remetente";
}

export function rotuloInscricao(tipoDocumento?: string | null, tipoItem?: string | null) {
  return ehNotaServico(tipoDocumento, tipoItem) ? "Inscrição municipal" : "IE";
}

export function rotuloLayoutNota(tipoDocumento?: string | null, tipoItem?: string | null) {
  return ehNotaServico(tipoDocumento, tipoItem) ? "NFS-e" : "DANFE";
}

export function rotuloDanfe(valor?: number | null) {
  if (valor === 0) return "0 — Entrada";
  if (valor === 1) return "1 — Saída";
  return "—";
}

export function statusReciboLabel(status: string) {
  const mapa: Record<string, string> = {
    rascunho: "Aguardando envio",
    processando: "Processando",
    processado: "Processado",
    revisao: "Processado",
    validada: "Processado",
    concluida: "Processado",
    falha_processamento: "Falha",
  };
  return mapa[status] ?? status;
}

export function reciboComFalha(status: string) {
  return status === "falha_processamento";
}

export function reciboEmProcessamento(status: string) {
  return status === "processando";
}

export function jsonExtracao(recibo: { qwenJson?: unknown; geminiJson?: unknown; perplexityJson?: unknown }) {
  return recibo.perplexityJson ?? recibo.qwenJson ?? recibo.geminiJson ?? null;
}

export function mensagemExtracao(status: string, json: unknown) {
  if (json != null) {
    try {
      return JSON.stringify(json, null, 2);
    } catch {
      return String(json);
    }
  }
  if (status === "falha_processamento") {
    return "A extração não foi concluída. Reprocesse ou remova o recibo.";
  }
  return "Aguardando extração dos dados do documento...";
}

export type RelatorioNfeDestinatario = {
  nomeRazaoSocial?: string | null;
  cpfCnpj?: string | null;
  endereco?: string | null;
  bairro?: string | null;
  cep?: string | null;
  municipio?: string | null;
  uf?: string | null;
  telefone?: string | null;
  inscricaoEstadual?: string | null;
  dataEmissao?: string | null;
  dataSaida?: string | null;
  horaSaida?: string | null;
};

export type RelatorioNfeImposto = {
  baseIcms?: number | null;
  valorIcms?: number | null;
  baseIcmsSt?: number | null;
  valorIcmsSt?: number | null;
  valorTotalProdutos?: number | null;
  valorFrete?: number | null;
  valorSeguro?: number | null;
  desconto?: number | null;
  outrasDespesas?: number | null;
  valorIpi?: number | null;
  valorTotalNota?: number | null;
};

export type RelatorioNfeTransportador = {
  nomeRazaoSocial?: string | null;
  fretePorConta?: string | null;
  codigoAntt?: string | null;
  placa?: string | null;
  uf?: string | null;
  cpfCnpj?: string | null;
  endereco?: string | null;
  municipio?: string | null;
  ufEndereco?: string | null;
  inscricaoEstadual?: string | null;
  quantidadeVolumes?: number | null;
  especie?: string | null;
  marca?: string | null;
  numeracao?: string | null;
  pesoBruto?: number | null;
  pesoLiquido?: number | null;
};

export type RelatorioNfeAdicionais = {
  informacoesComplementares?: string | null;
  reservadoAoFisco?: string | null;
  dataHoraImpressao?: string | null;
};

export type RelatorioItem = {
  reciboId: string;
  compraId: string;
  codigo: string;
  data: string;
  status: string;
  numeroRecibo?: string | null;
  subtotal?: number | null;
  descontos?: number | null;
  acrescimos?: number | null;
  totalCompra?: number | null;
  formaPagamento?: string | null;
  fornecedorId?: string | null;
  fornecedorNome?: string | null;
  fornecedorRazaoSocial?: string | null;
  fornecedorCpfCnpj?: string | null;
  fornecedorTelefone?: string | null;
  fornecedorEndereco?: string | null;
  produtoId?: string | null;
  produtoNome?: string | null;
  produtoMarca?: string | null;
  produtoVariante?: string | null;
  conteudoEmbalagem?: number | null;
  codigoProduto?: string | null;
  descricao: string;
  quantidade: number;
  unidade?: string | null;
  precoUnitario?: number | null;
  desconto?: number | null;
  total?: number | null;
  arquivoUrl?: string | null;
};

export type RelatorioProduto = {
  id?: string | null;
  descricao: string;
  nome?: string | null;
  codigo?: string | null;
  marca?: string | null;
  variante?: string | null;
  conteudoEmbalagem?: number | null;
  quantidade: number;
  unidade?: string | null;
  precoUnitario?: number | null;
  desconto?: number | null;
  total?: number | null;
  tipoItem?: string | null;
  servicoId?: string | null;
  servicoNome?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
  codigoExterno?: string | null;
};

export type RelatorioFornecedor = {
  id?: string | null;
  nome?: string | null;
  razaoSocial?: string | null;
  cpfCnpj?: string | null;
  telefone?: string | null;
  endereco?: string | null;
};

export type RelatorioCompra = {
  compraId: string;
  reciboId: string;
  codigo: string;
  data: string;
  status: string;
  numeroRecibo?: string | null;
  subtotal?: number | null;
  descontos?: number | null;
  acrescimos?: number | null;
  total?: number | null;
  formaPagamento?: string | null;
  arquivoUrl?: string | null;
  tipoDocumento?: string | null;
  tipoItem?: string | null;
  danfeTipo?: number | null;
  serie?: string | null;
  folha?: string | null;
  chaveAcesso?: string | null;
  codigoBarras?: string | null;
  protocoloAutorizacao?: string | null;
  protocoloData?: string | null;
  naturezaOperacao?: string | null;
  inscricaoEstadual?: string | null;
  inscricaoEstadualSt?: string | null;
  dataEmissao?: string | null;
  fornecedor: RelatorioFornecedor;
  produtos: RelatorioProduto[];
  servicos?: RelatorioProduto[];
  nfeDestinatario?: RelatorioNfeDestinatario | null;
  nfeImposto?: RelatorioNfeImposto | null;
  nfeTransportador?: RelatorioNfeTransportador | null;
  nfeAdicionais?: RelatorioNfeAdicionais | null;
};

export type RelatorioItens = {
  inicio: string;
  fim: string;
  fornecedorId?: string | null;
  limite: number;
  total: number;
  compras: RelatorioCompra[];
  itens: RelatorioItem[];
};

export function codigoCurto(codigo?: string | null) {
  if (!codigo) {
    return "—";
  }
  return codigo.slice(0, 8).toUpperCase();
}

export const CONFIRMA_EXCLUSAO_RECIBO =
  "Excluir este recibo/NF e os dados vinculados (compra, itens, arquivos e produtos/serviços deste documento)? Se o fornecedor não tiver outros recibos, ele também será excluído. Esta ação não pode ser desfeita.";

export const UNIDADES_CONTROLE = [
  "un",
  "kg",
  "g",
  "l",
  "ml",
  "m",
  "m2",
  "m3",
  "cx",
  "pc",
  "par",
  "h",
] as const;

export function rotuloReciboOpcao(recibo: ReciboLista) {
  const codigo = codigoCurto(recibo.codigo ?? recibo.id);
  const tipo = rotuloTipoDocumento(recibo.tipoDocumento);
  const nome = recibo.fornecedorNome?.trim() || recibo.numeroRecibo?.trim();
  return nome ? `${codigo} — ${tipo} · ${nome}` : `${codigo} — ${tipo}`;
}

export type CadastroProdutoBody = {
  nome: string;
  marca?: string | null;
  variante?: string | null;
  unidadeControle?: string | null;
  conteudoEmbalagem?: number | null;
  sinonimo?: string | null;
  sinonimos?: string[];
  codigoExterno?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorUnitario?: number | null;
  valorDesconto?: number | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
  reciboId?: string | null;
};

export type CadastroServicoBody = {
  nome: string;
  unidadeControle?: string | null;
  codigoExterno?: string | null;
  ncmSh?: string | null;
  csosn?: string | null;
  cfop?: string | null;
  valorUnitario?: number | null;
  valorDesconto?: number | null;
  valorLiquido?: number | null;
  baseIcms?: number | null;
  valorIcms?: number | null;
  valorIpi?: number | null;
  aliqIcms?: number | null;
  aliqIpi?: number | null;
  reciboId?: string | null;
};

export const recibosApi = {
  criarComArquivos: (files: File[]) => {
    const form = new FormData();
    for (const file of files) {
      form.append("arquivos", file);
    }
    return apiForm<ReciboLista>("/v1/recibos", form);
  },
  criarRascunho: () => apiJson<ReciboLista>("/v1/recibos/rascunho", { method: "POST" }),
  limparRascunhosAnteriores: () =>
    apiJson<{ removidos: number }>("/v1/recibos/rascunhos", { method: "DELETE" }),
  listar: (status?: string) =>
    apiJson<ReciboLista[]>(`/v1/recibos${status ? `?status=${encodeURIComponent(status)}` : ""}`),
  obter: (id: string) => apiJson<ReciboDetalhe>(`/v1/recibos/${id}`),
  resumo: (ano?: number, mes?: number) => {
    const params = new URLSearchParams();
    if (ano != null) params.set("ano", String(ano));
    if (mes != null) params.set("mes", String(mes));
    const qs = params.toString();
    return apiJson<RecibosResumo>(`/v1/recibos/resumo${qs ? `?${qs}` : ""}`);
  },
  anexar: (id: string, arquivo: File) => {
    const form = new FormData();
    form.append("arquivo", arquivo);
    return apiForm<ReciboAnexo>(`/v1/recibos/${id}/anexos`, form);
  },
  patchAnexos: (id: string, body: { ordem?: string[]; remover?: string[] }) =>
    apiJson<ReciboDetalhe>(`/v1/recibos/${id}/anexos`, {
      method: "PATCH",
      body: JSON.stringify(body),
    }),
  captura: (id: string) =>
    apiJson<CapturaCriada>(`/v1/recibos/${id}/captura`, { method: "POST", body: "{}" }),
  capturaStatus: (id: string) => apiJson<CapturaStatus>(`/v1/recibos/${id}/captura`),
  processar: (id: string) =>
    apiJson<ReciboLista>(`/v1/recibos/${id}/processar`, { method: "POST", body: "{}" }),
  reprocessar: (id: string) =>
    apiJson<ReciboLista>(`/v1/recibos/${id}/reprocessar`, { method: "POST", body: "{}" }),
  excluir: (id: string) => apiJson<void>(`/v1/recibos/${id}`, { method: "DELETE" }),
  alterarFornecedor: (id: string, fornecedorId: string) =>
    apiJson<ReciboLista>(`/v1/recibos/${id}/fornecedor`, {
      method: "PATCH",
      body: JSON.stringify({ fornecedorId }),
    }),
  documento: (id: string) => apiJson<{ url: string }>(`/v1/recibos/${id}/documento`),
  arquivoBlob: async (id: string) => {
    const jwt = lerJwt();
    const headers = new Headers();
    if (jwt) {
      headers.set("Authorization", `Bearer ${jwt}`);
    }
    const response = await fetch(`${apiBaseUrl}/v1/recibos/${id}/arquivo`, { headers });
    if (!response.ok) {
      throw new ApiError(response.status, "Prévia indisponível até que o arquivo seja processado...");
    }
    return response.blob();
  },
  relatorioItens: (params: { inicio: string; fim: string; fornecedorId?: string; limite?: number }) => {
    const qs = new URLSearchParams();
    qs.set("inicio", params.inicio);
    qs.set("fim", params.fim);
    if (params.fornecedorId) qs.set("fornecedorId", params.fornecedorId);
    qs.set("limite", String(params.limite ?? 10000));
    return apiJson<RelatorioItens>(`/v1/relatorios/itens?${qs}`);
  },
  relatorioArquivo: async (
    params: { inicio: string; fim: string; fornecedorId?: string; limite?: number },
    formato: "csv" | "xlsx",
  ) => {
    const qs = new URLSearchParams();
    qs.set("inicio", params.inicio);
    qs.set("fim", params.fim);
    if (params.fornecedorId) qs.set("fornecedorId", params.fornecedorId);
    qs.set("limite", String(params.limite ?? 10000));
    qs.set("formato", formato);
    const jwt = lerJwt();
    const headers = new Headers();
    if (jwt) {
      headers.set("Authorization", `Bearer ${jwt}`);
    }
    const response = await fetch(`${apiBaseUrl}/v1/relatorios/itens?${qs}`, { headers });
    if (!response.ok) {
      throw new ApiError(response.status, (await response.text()) || `Falha ${response.status}`);
    }
    return response.blob();
  },
  fornecedores: (busca?: string, reciboId?: string, somenteProprio = false) => {
    const qs = new URLSearchParams();
    if (busca) qs.set("busca", busca);
    if (reciboId) qs.set("reciboId", reciboId);
    if (somenteProprio) qs.set("somenteProprio", "true");
    const suffix = qs.toString() ? `?${qs}` : "";
    return apiJson<Fornecedor[]>(`/v1/fornecedores${suffix}`);
  },
  criarFornecedor: (body: {
    nome: string;
    razaoSocial?: string | null;
    cpfCnpj?: string | null;
    telefone?: string | null;
    endereco?: string | null;
  }) => apiJson<Fornecedor>("/v1/fornecedores", { method: "POST", body: JSON.stringify(body) }),
  atualizarFornecedor: (
    id: string,
    body: {
      nome: string;
      razaoSocial?: string | null;
      cpfCnpj?: string | null;
      telefone?: string | null;
      endereco?: string | null;
    },
  ) =>
    apiJson<Fornecedor>(`/v1/fornecedores/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  excluirFornecedor: (id: string) => apiJson<void>(`/v1/fornecedores/${id}`, { method: "DELETE" }),
  recibosDoFornecedor: (id: string) => apiJson<ReciboLista[]>(`/v1/fornecedores/${id}/recibos`),
  completarFornecedor: (nome: string) =>
    apiJson<FornecedorCompletar>(`/v1/fornecedores/completar`, {
      method: "POST",
      body: JSON.stringify({ nome }),
    }),
  produtos: (busca?: string, reciboId?: string) => {
    const qs = new URLSearchParams();
    if (busca) qs.set("busca", busca);
    if (reciboId) qs.set("reciboId", reciboId);
    const suffix = qs.toString() ? `?${qs}` : "";
    return apiJson<Produto[]>(`/v1/produtos${suffix}`);
  },
  criarProduto: (body: CadastroProdutoBody) =>
    apiJson<Produto>("/v1/produtos", { method: "POST", body: JSON.stringify(body) }),
  atualizarProduto: (id: string, body: CadastroProdutoBody) =>
    apiJson<Produto>(`/v1/produtos/${id}`, {
      method: "PUT",
      body: JSON.stringify({
        ...body,
        sinonimos: body.sinonimos ?? [],
      }),
    }),
  excluirProduto: (id: string) => apiJson<void>(`/v1/produtos/${id}`, { method: "DELETE" }),
  servicos: (busca?: string, reciboId?: string) => {
    const qs = new URLSearchParams();
    if (busca) qs.set("busca", busca);
    if (reciboId) qs.set("reciboId", reciboId);
    const suffix = qs.toString() ? `?${qs}` : "";
    return apiJson<Servico[]>(`/v1/servicos${suffix}`);
  },
  criarServico: (body: CadastroServicoBody) =>
    apiJson<Servico>("/v1/servicos", { method: "POST", body: JSON.stringify(body) }),
  atualizarServico: (id: string, body: CadastroServicoBody) =>
    apiJson<Servico>(`/v1/servicos/${id}`, {
      method: "PUT",
      body: JSON.stringify(body),
    }),
  excluirServico: (id: string) => apiJson<void>(`/v1/servicos/${id}`, { method: "DELETE" }),
};
