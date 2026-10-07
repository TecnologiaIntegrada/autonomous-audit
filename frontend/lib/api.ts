export const apiBaseUrl = (
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "https://autonomousauditapi.canada-software.com.br/"
).replace(/\/$/, "");

const JWT_KEY = "aa_jwt";
const REGISTRO_KEY = "aa_google_registro";

export type GoogleRegistro = {
  registroToken: string;
  email: string;
  nome: string;
  foto?: string;
};

export function guardarJwt(token: string) {
  localStorage.setItem(JWT_KEY, token);
}

export function lerJwt(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  return localStorage.getItem(JWT_KEY);
}

export function limparJwt() {
  localStorage.removeItem(JWT_KEY);
}

export function guardarRegistroGoogle(dados: GoogleRegistro) {
  sessionStorage.setItem(REGISTRO_KEY, JSON.stringify(dados));
}

export function lerRegistroGoogle(): GoogleRegistro | null {
  if (typeof window === "undefined") {
    return null;
  }
  const raw = sessionStorage.getItem(REGISTRO_KEY);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw) as GoogleRegistro;
  } catch {
    return null;
  }
}

export function limparRegistroGoogle() {
  sessionStorage.removeItem(REGISTRO_KEY);
}

type ProblemBody = {
  detail?: string;
  title?: string;
  mensagem?: string;
  errors?: Record<string, string[]>;
};

export function mensagemUsuario(texto: string): string {
  return texto
    .replace(/https?:\/\/[^\s]*dropbox\.com[^\s]*/gi, "armazenamento")
    .replace(/dropbox/gi, "Armazenamento")
    .replace(/Armazenamento Armazenamento/g, "Armazenamento");
}

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(mensagemUsuario(message));
    this.status = status;
  }
}

async function lerErro(response: Response): Promise<string> {
  const text = await response.text();
  if (!text) {
    return `Falha ${response.status}`;
  }
  try {
    const body = JSON.parse(text) as ProblemBody;
    const validacao = body.errors
      ? Object.values(body.errors).flat().filter(Boolean).join(" ")
      : "";
    return body.detail ?? body.mensagem ?? validacao ?? body.title ?? text;
  } catch {
    return text;
  }
}

export async function apiJson<T>(
  path: string,
  init: RequestInit = {},
  opcoes: { skipAuth?: boolean } = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }
  const jwt = opcoes.skipAuth ? null : lerJwt();
  if (jwt && !headers.has("Authorization")) {
    headers.set("Authorization", `Bearer ${jwt}`);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  });
  if (!response.ok) {
    throw new ApiError(response.status, await lerErro(response));
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export type LoginApiResponse = {
  token?: string;
  desafio?: string;
  mensagem?: string;
};

export type UsuarioMe = {
  id: string;
  nome: string;
  emailPrincipal: string;
  emailSecundario?: string | null;
  telefonePrincipal?: string | null;
  telefoneSecundario?: string | null;
  cidade?: string | null;
  endereco?: string | null;
  cep?: string | null;
  uf?: string | null;
  pais?: string | null;
  emailPrincipalVerificado?: boolean;
  emailSecundarioVerificado?: boolean;
  telefonePrincipalVerificado?: boolean;
  telefoneSecundarioVerificado?: boolean;
  apiTokenExpira?: string | null;
  googlePicture?: string | null;
  geminiTokensTotal?: number;
  geminiCustoTotalBrl?: number;
  qwenTokensTotal?: number;
  qwenCustoTotalBrl?: number;
  perplexityTokensTotal?: number;
  perplexityCustoTotalBrl?: number;
};

export type TokenRelatorio = {
  token: string;
  expiraEm: string;
};

export type EnderecoCep = {
  cep: string;
  logradouro: string;
  bairro: string;
  cidade: string;
  uf: string;
  pais: string;
};

export async function apiLogin(
  email: string,
  senha: string,
  codigos?: { sms?: string; email?: string },
): Promise<LoginApiResponse> {
  const headers = new Headers();
  headers.set("email", email);
  headers.set("senha", senha);
  if (codigos?.sms) {
    headers.set("sms_auth_code", codigos.sms);
  }
  if (codigos?.email) {
    headers.set("email_auth_code", codigos.email);
  }
  return apiJson<LoginApiResponse>("/v1/auth/login", { method: "POST", headers }, { skipAuth: true });
}

export type GoogleAuthApiResponse = {
  status: "pronto" | "cadastro_pendente";
  token?: string;
  registroToken?: string;
  email?: string;
  nome?: string;
  foto?: string;
};
