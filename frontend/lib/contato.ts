export function emailValido(valor: string): boolean {
  const texto = valor.trim();
  if (!texto) {
    return false;
  }
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(texto);
}

export function alertaEmail(valor: string, obrigatorio = true): string {
  const texto = valor.trim();
  if (!texto) {
    return obrigatorio ? "Informe um e-mail válido." : "";
  }
  return emailValido(texto) ? "" : "Informe um e-mail válido.";
}

export function normalizarTelefone(valor: string): string {
  const bruto = valor.trim();
  if (!bruto) {
    return "";
  }
  const mais = bruto.startsWith("+");
  const digitos = bruto.replace(/\D/g, "");
  return mais ? `+${digitos}` : digitos;
}

export function telefonesIguais(a?: string | null, b?: string | null): boolean {
  return normalizarTelefone(a ?? "") === normalizarTelefone(b ?? "");
}

export function alertaTelefoneDuplicado(principal: string, secundario: string): string {
  const d1 = (principal ?? "").replace(/\D/g, "");
  const d2 = (secundario ?? "").replace(/\D/g, "");
  if (!d1 || !d2) {
    return "";
  }
  return d1 === d2 ? "O telefone secundário deve ser diferente do telefone principal." : "";
}

export function emailsIguais(a?: string | null, b?: string | null): boolean {
  return (a ?? "").trim().toLowerCase() === (b ?? "").trim().toLowerCase();
}
