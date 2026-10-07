"use client";

import { FormEvent, useEffect, useState } from "react";
import { AppShell } from "@/components/AppShell";
import {
  apiBaseUrl,
  ApiError,
  apiJson,
  type EnderecoCep,
  type TokenRelatorio,
  type UsuarioMe,
} from "@/lib/api";
import { alertaEmail, alertaTelefoneDuplicado, emailsIguais, normalizarTelefone, telefonesIguais } from "@/lib/contato";

type Aba = "perfil" | "api";
type Canal = "principal" | "secundario";
type TipoVerificacao = "email" | "sms";

function soDigitos(valor: string) {
  return valor.replace(/\D/g, "");
}

function formatarCep(valor: string) {
  const d = soDigitos(valor).slice(0, 8);
  if (d.length <= 5) {
    return d;
  }
  return `${d.slice(0, 5)}-${d.slice(5)}`;
}

function Selo({ ok }: { ok: boolean }) {
  return ok ? (
    <span className="selo-ok">Verificado</span>
  ) : (
    <span className="selo-pendente">Não verificado</span>
  );
}

function CampoVerificacao({
  tipo,
  canal,
  disponivel,
  verificado,
  pendente,
  codigo,
  onCodigo,
  onEnviar,
  onConfirmar,
}: {
  tipo: TipoVerificacao;
  canal: Canal;
  disponivel: boolean;
  verificado: boolean;
  pendente: string;
  codigo: string;
  onCodigo: (valor: string) => void;
  onEnviar: () => void;
  onConfirmar: () => void;
}) {
  if (!disponivel) {
    return <p className="muted">Informe e salve o {tipo === "email" ? "e-mail" : "telefone"} antes de verificar.</p>;
  }
  if (verificado) {
    return null;
  }
  const chave = `${tipo}-${canal}`;
  return (
    <div className="verify-row">
      <button className="ghost-button dark" type="button" onClick={onEnviar}>
        Enviar código {tipo === "email" ? "por e-mail" : "por SMS"}
      </button>
      <label className="codigo-campo">
        Código de 6 dígitos
        <input
          id={`codigo-${chave}`}
          className="codigo-6"
          inputMode="numeric"
          maxLength={6}
          value={codigo}
          onChange={(e) => onCodigo(soDigitos(e.target.value).slice(0, 6))}
        />
      </label>
      <button className="primary-button" type="button" onClick={onConfirmar} disabled={codigo.length !== 6}>
        Confirmar
      </button>
      {pendente ? <span className="muted">{pendente}</span> : null}
    </div>
  );
}

export default function ConfiguracoesPage() {
  const [aba, setAba] = useState<Aba>("perfil");
  const [carregando, setCarregando] = useState(true);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState("");
  const [ok, setOk] = useState("");
  const [me, setMe] = useState<UsuarioMe | null>(null);
  const [nome, setNome] = useState("");
  const [emailSecundario, setEmailSecundario] = useState("");
  const [emailSecundarioErro, setEmailSecundarioErro] = useState("");
  const [telefonePrincipal, setTelefonePrincipal] = useState("");
  const [telefoneSecundario, setTelefoneSecundario] = useState("");
  const [cep, setCep] = useState("");
  const [endereco, setEndereco] = useState("");
  const [cidade, setCidade] = useState("");
  const [uf, setUf] = useState("");
  const [pais, setPais] = useState("Brasil");
  const [cepStatus, setCepStatus] = useState("");
  const [cepEditado, setCepEditado] = useState(false);
  const [codigos, setCodigos] = useState<Record<string, string>>({});
  const [pendencias, setPendencias] = useState<Record<string, string>>({});
  const [gerando, setGerando] = useState(false);
  const [tokenApi, setTokenApi] = useState("");
  const [tokenExpira, setTokenExpira] = useState("");
  const [copiado, setCopiado] = useState(false);

  function aplicar(usuario: UsuarioMe) {
    setMe(usuario);
    setNome(usuario.nome ?? "");
    setEmailSecundario(usuario.emailSecundario ?? "");
    setTelefonePrincipal(usuario.telefonePrincipal ?? "");
    setTelefoneSecundario(usuario.telefoneSecundario ?? "");
    setCep(formatarCep(usuario.cep ?? ""));
    setEndereco(usuario.endereco ?? "");
    setCidade(usuario.cidade ?? "");
    setUf((usuario.uf ?? "").toUpperCase());
    setPais(usuario.pais || "Brasil");
    if (usuario.apiTokenExpira) {
      setTokenExpira(usuario.apiTokenExpira);
    }
  }

  async function carregar() {
    const usuario = await apiJson<UsuarioMe>("/v1/auth/me");
    aplicar(usuario);
  }

  useEffect(() => {
    let cancelado = false;
    (async () => {
      try {
        await carregar();
      } catch (falha) {
        if (!cancelado) {
          setErro(falha instanceof ApiError ? falha.message : "Não foi possível carregar o perfil.");
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
  }, []);

  useEffect(() => {
    if (!cepEditado || soDigitos(cep).length !== 8) {
      return;
    }
    const ac = new AbortController();
    const timer = window.setTimeout(async () => {
      setCepStatus("Buscando endereço…");
      try {
        const encontrado = await apiJson<EnderecoCep>(`/v1/enderecos/cep/${soDigitos(cep)}`, {
          signal: ac.signal,
        });
        const partes = [encontrado.logradouro, encontrado.bairro].filter(Boolean);
        if (partes.length > 0) {
          setEndereco(partes.join(", "));
        }
        if (encontrado.cidade) setCidade(encontrado.cidade);
        if (encontrado.uf) setUf(encontrado.uf.toUpperCase());
        if (encontrado.pais) setPais(encontrado.pais === "BR" ? "Brasil" : encontrado.pais);
        if (encontrado.cep) setCep(formatarCep(encontrado.cep));
        setCepStatus("Endereço preenchido pelo CEP.");
      } catch (falha) {
        if (ac.signal.aborted) {
          return;
        }
        setCepStatus(falha instanceof ApiError ? falha.message : "CEP não encontrado.");
      }
    }, 400);
    return () => {
      ac.abort();
      window.clearTimeout(timer);
    };
  }, [cep, cepEditado]);

  async function salvar(e: FormEvent) {
    e.preventDefault();
    setErro("");
    setOk("");
    const alertaSecundario = alertaEmail(emailSecundario, false);
    setEmailSecundarioErro(alertaSecundario);
    if (alertaSecundario) {
      setErro(alertaSecundario);
      return;
    }
    const alertaTelefone = alertaTelefoneDuplicado(telefonePrincipal, telefoneSecundario);
    if (alertaTelefone) {
      setErro(alertaTelefone);
      return;
    }
    setSalvando(true);
    try {
      const atualizado = await apiJson<UsuarioMe>("/v1/auth/me", {
        method: "PUT",
        body: JSON.stringify({
          nome,
          emailSecundario: emailSecundario.trim() || null,
          telefonePrincipal: normalizarTelefone(telefonePrincipal) || null,
          telefoneSecundario: normalizarTelefone(telefoneSecundario) || null,
          cidade,
          endereco,
          cep,
          uf,
          pais,
        }),
      });
      aplicar(atualizado);
      setOk("Dados salvos.");
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível salvar o perfil.");
    } finally {
      setSalvando(false);
    }
  }

  async function enviarCodigo(tipo: TipoVerificacao, canal: Canal) {
    if (!me) {
      return;
    }
    const chave = `${tipo}-${canal}`;
    setErro("");
    setOk("");
    if (tipo === "sms") {
      const alertaTelefone = alertaTelefoneDuplicado(telefonePrincipal, telefoneSecundario);
      if (canal === "secundario" && alertaTelefone) {
        setErro(alertaTelefone);
        return;
      }
      const atual = canal === "principal" ? telefonePrincipal : telefoneSecundario;
      const salvo = canal === "principal" ? me.telefonePrincipal : me.telefoneSecundario;
      if (!telefonesIguais(atual, salvo)) {
        setErro("Salve o novo telefone antes de enviar o código.");
        return;
      }
    }
    if (tipo === "email" && canal === "secundario") {
      const alertaSecundario = alertaEmail(emailSecundario, false);
      if (alertaSecundario) {
        setEmailSecundarioErro(alertaSecundario);
        setErro(alertaSecundario);
        return;
      }
      if (!emailsIguais(emailSecundario, me.emailSecundario)) {
        setErro("Salve o novo e-mail antes de enviar o código.");
        return;
      }
    }
    setPendencias((atual) => ({ ...atual, [chave]: "Enviando…" }));
    try {
      const resposta = await apiJson<{ mensagem: string }>("/v1/auth/me/verificar", {
        method: "POST",
        body: JSON.stringify({ tipo, canal }),
      });
      setPendencias((atual) => ({ ...atual, [chave]: resposta.mensagem }));
    } catch (falha) {
      setPendencias((atual) => ({ ...atual, [chave]: "" }));
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível enviar o código.");
    }
  }

  async function confirmarCodigo(tipo: TipoVerificacao, canal: Canal) {
    const chave = `${tipo}-${canal}`;
    setErro("");
    setOk("");
    try {
      const atualizado = await apiJson<UsuarioMe>("/v1/auth/me/verificar/confirmar", {
        method: "POST",
        body: JSON.stringify({ tipo, canal, codigo: codigos[chave] ?? "" }),
      });
      aplicar(atualizado);
      setCodigos((atual) => ({ ...atual, [chave]: "" }));
      setPendencias((atual) => ({ ...atual, [chave]: "" }));
      setOk("Contato verificado.");
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Código inválido ou expirado.");
    }
  }

  async function gerarToken() {
    setErro("");
    setOk("");
    setCopiado(false);
    setGerando(true);
    try {
      const emitido = await apiJson<TokenRelatorio>("/v1/auth/me/api-token", { method: "POST" });
      setTokenApi(emitido.token);
      setTokenExpira(emitido.expiraEm);
      setOk("Token gerado. Copie agora pois ele não será exibido novamente.");
    } catch (falha) {
      setErro(falha instanceof ApiError ? falha.message : "Não foi possível gerar o token.");
    } finally {
      setGerando(false);
    }
  }

  async function copiarToken() {
    try {
      await navigator.clipboard.writeText(tokenApi);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  }

  const curl = `curl -sS -X GET "${apiBaseUrl}/v1/relatorios/itens?inicio=${encodeURIComponent(new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString())}&fim=${encodeURIComponent(new Date().toISOString())}&limite=10000" -H "Authorization: Bearer ${tokenApi || "$AA_RELATORIO_TOKEN"}" -H "Accept: application/json"`;

  const emailSecundarioVerificado =
    !!me?.emailSecundarioVerificado &&
    emailsIguais(emailSecundario, me.emailSecundario) &&
    !!emailSecundario.trim();
  const telefonePrincipalVerificado =
    !!me?.telefonePrincipalVerificado &&
    telefonesIguais(telefonePrincipal, me.telefonePrincipal) &&
    !!telefonePrincipal.trim();
  const telefoneSecundarioVerificado =
    !!me?.telefoneSecundarioVerificado &&
    telefonesIguais(telefoneSecundario, me.telefoneSecundario) &&
    !!telefoneSecundario.trim();
  const telefoneDuplicadoErro = alertaTelefoneDuplicado(telefonePrincipal, telefoneSecundario);

  return (
    <AppShell titulo="Configurações">
      <p>Atualize seus contatos, confirme e-mail e telefone e gere o token da API de relatório, limitado aos seus recibos.</p>
      <div className="tabs" role="tablist">
        <button
          className={aba === "perfil" ? "tab is-active" : "tab"}
          type="button"
          role="tab"
          aria-selected={aba === "perfil"}
          onClick={() => setAba("perfil")}
        >
          Meus dados
        </button>
        <button
          className={aba === "api" ? "tab is-active" : "tab"}
          type="button"
          role="tab"
          aria-selected={aba === "api"}
          onClick={() => setAba("api")}
        >
          Token da API
        </button>
      </div>
      {erro ? (
        <p className="feedback" role="alert">
          {erro}
        </p>
      ) : null}
      {ok ? (
        <p className="feedback ok" role="status">
          {ok}
        </p>
      ) : null}
      {carregando ? <p className="muted">Carregando…</p> : null}

      {aba === "perfil" && me ? (
        <form className="panel-block config-form" onSubmit={(e) => void salvar(e)}>
          <div className="form-grid">
            <label className="span-2">
              Nome
              <input value={nome} onChange={(e) => setNome(e.target.value)} required />
            </label>
            <label className="span-2">
              E-mail principal
              <input value={me.emailPrincipal} readOnly disabled />
              <Selo ok={!!me.emailPrincipalVerificado} />
            </label>
            <div className="span-2">
              <CampoVerificacao
                tipo="email"
                canal="principal"
                disponivel={!!me.emailPrincipal}
                verificado={!!me.emailPrincipalVerificado}
                pendente={pendencias["email-principal"] ?? ""}
                codigo={codigos["email-principal"] ?? ""}
                onCodigo={(valor) => setCodigos((atual) => ({ ...atual, "email-principal": valor }))}
                onEnviar={() => void enviarCodigo("email", "principal")}
                onConfirmar={() => void confirmarCodigo("email", "principal")}
              />
            </div>
            <label className="span-2">
              E-mail secundário
              <input
                type="email"
                value={emailSecundario}
                onChange={(e) => {
                  setEmailSecundario(e.target.value);
                  setEmailSecundarioErro("");
                }}
                onBlur={() => setEmailSecundarioErro(alertaEmail(emailSecundario, false))}
                aria-invalid={emailSecundarioErro ? true : undefined}
                placeholder="opcional"
              />
              {emailSecundarioErro ? (
                <span className="error" role="alert">
                  {emailSecundarioErro}
                </span>
              ) : null}
              <Selo ok={emailSecundarioVerificado} />
            </label>
            <div className="span-2">
              <CampoVerificacao
                tipo="email"
                canal="secundario"
                disponivel={!!(emailSecundario || me.emailSecundario)}
                verificado={emailSecundarioVerificado}
                pendente={pendencias["email-secundario"] ?? ""}
                codigo={codigos["email-secundario"] ?? ""}
                onCodigo={(valor) => setCodigos((atual) => ({ ...atual, "email-secundario": valor }))}
                onEnviar={() => void enviarCodigo("email", "secundario")}
                onConfirmar={() => void confirmarCodigo("email", "secundario")}
              />
            </div>
            <label>
              Telefone principal
              <input
                value={telefonePrincipal}
                onChange={(e) => setTelefonePrincipal(e.target.value)}
                placeholder="11987654321"
              />
              <Selo ok={telefonePrincipalVerificado} />
            </label>
            <label>
              Telefone secundário
              <input
                value={telefoneSecundario}
                onChange={(e) => setTelefoneSecundario(e.target.value)}
                aria-invalid={telefoneDuplicadoErro ? true : undefined}
                placeholder="opcional"
              />
              {telefoneDuplicadoErro ? (
                <span className="error" role="alert">
                  {telefoneDuplicadoErro}
                </span>
              ) : null}
              <Selo ok={telefoneSecundarioVerificado} />
            </label>
            <div>
              <CampoVerificacao
                tipo="sms"
                canal="principal"
                disponivel={!!(telefonePrincipal || me.telefonePrincipal)}
                verificado={telefonePrincipalVerificado}
                pendente={pendencias["sms-principal"] ?? ""}
                codigo={codigos["sms-principal"] ?? ""}
                onCodigo={(valor) => setCodigos((atual) => ({ ...atual, "sms-principal": valor }))}
                onEnviar={() => void enviarCodigo("sms", "principal")}
                onConfirmar={() => void confirmarCodigo("sms", "principal")}
              />
            </div>
            <div>
              {telefoneDuplicadoErro ? null : (
                <CampoVerificacao
                  tipo="sms"
                  canal="secundario"
                  disponivel={!!(telefoneSecundario || me.telefoneSecundario)}
                  verificado={telefoneSecundarioVerificado}
                  pendente={pendencias["sms-secundario"] ?? ""}
                  codigo={codigos["sms-secundario"] ?? ""}
                  onCodigo={(valor) => setCodigos((atual) => ({ ...atual, "sms-secundario": valor }))}
                  onEnviar={() => void enviarCodigo("sms", "secundario")}
                  onConfirmar={() => void confirmarCodigo("sms", "secundario")}
                />
              )}
            </div>
            <label>
              CEP
              <input
                inputMode="numeric"
                value={cep}
                onChange={(e) => {
                  setCepEditado(true);
                  setCep(formatarCep(e.target.value));
                }}
                placeholder="00000-000"
              />
              {cepStatus ? <span className="muted">{cepStatus}</span> : null}
            </label>
            <label>
              UF
              <input value={uf} onChange={(e) => setUf(e.target.value.toUpperCase().slice(0, 2))} maxLength={2} />
            </label>
            <label className="span-2">
              Endereço
              <input value={endereco} onChange={(e) => setEndereco(e.target.value)} />
            </label>
            <label>
              Cidade
              <input value={cidade} onChange={(e) => setCidade(e.target.value)} />
            </label>
            <label>
              País
              <input value={pais} onChange={(e) => setPais(e.target.value)} />
            </label>
          </div>
          <p className="muted">Salve as alterações antes de pedir um código de verificação para um contato recém-alterado.</p>
          <button className="primary-button" type="submit" disabled={salvando || !!telefoneDuplicadoErro}>
            {salvando ? "Salvando…" : "Salvar dados"}
          </button>
        </form>
      ) : null}

      {aba === "api" ? (
        <section className="panel-block">
          <h2>Token de consumo da API de relatório</h2>
          <p>
            Este token devolve somente os seus recibos, fornecedores/prestadores, produtos e a URL do arquivo
            (`arquivoUrl`). O download em `arquivoUrl` não usa token. Um token novo invalida o anterior. Ele não acessa o restante da API.
          </p>
          {tokenExpira ? (
            <p className="muted">
              Token atual válido até {new Date(tokenExpira).toLocaleString("pt-BR")}.
            </p>
          ) : (
            <p className="muted">Nenhum token ativo. Gere um para integrar via curl.</p>
          )}
          <p className="toolbar">
            <button className="primary-button" type="button" disabled={gerando} onClick={() => void gerarToken()}>
              {gerando ? "Gerando…" : tokenExpira ? "Gerar novo token" : "Gerar token"}
            </button>
          </p>
          {tokenApi ? (
            <>
              <label>
                Token (copie agora)
                <textarea className="token-box" readOnly rows={4} value={tokenApi} />
              </label>
              <p className="toolbar">
                <button className="ghost-button dark" type="button" onClick={() => void copiarToken()}>
                  {copiado ? "Copiado" : "Copiar token"}
                </button>
              </p>
            </>
          ) : null}
          <h3>Exemplo curl</h3>
          <pre className="curl-block">{curl}</pre>
        </section>
      ) : null}
    </AppShell>
  );
}
