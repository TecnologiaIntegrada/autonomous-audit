"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AuthShell } from "@/components/AuthShell";
import { VoltarAoLoginButton } from "@/components/VoltarAoLoginButton";
import {
  apiJson,
  guardarJwt,
  lerRegistroGoogle,
  limparRegistroGoogle,
  type EnderecoCep,
  type LoginApiResponse,
} from "@/lib/api";
import { alertaEmail, normalizarTelefone } from "@/lib/contato";

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

export default function CadastroPage() {
  const router = useRouter();
  const [modoGoogle, setModoGoogle] = useState(false);
  const [pronto, setPronto] = useState(false);
  const [nome, setNome] = useState("");
  const [email, setEmail] = useState("");
  const [emailConfirmacao, setEmailConfirmacao] = useState("");
  const [telefone, setTelefone] = useState("");
  const [senha, setSenha] = useState("");
  const [senhaConfirmacao, setSenhaConfirmacao] = useState("");
  const [foto, setFoto] = useState("");
  const [cep, setCep] = useState("");
  const [logradouro, setLogradouro] = useState("");
  const [numero, setNumero] = useState("");
  const [bairro, setBairro] = useState("");
  const [cidade, setCidade] = useState("");
  const [uf, setUf] = useState("");
  const [pais, setPais] = useState("Brasil");
  const [cepStatus, setCepStatus] = useState("");
  const [erro, setErro] = useState("");
  const [emailErro, setEmailErro] = useState("");
  const [emailConfirmErro, setEmailConfirmErro] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [registroToken, setRegistroToken] = useState<string | null>(null);

  useEffect(() => {
    const registro = lerRegistroGoogle();
    if (registro) {
      setModoGoogle(true);
      setRegistroToken(registro.registroToken);
      setNome(registro.nome);
      setEmail(registro.email);
      setEmailConfirmacao(registro.email);
      setFoto(registro.foto ?? "");
    }
    setPronto(true);
  }, []);

  useEffect(() => {
    if (!pronto || soDigitos(cep).length !== 8) {
      return;
    }
    if (modoGoogle && !registroToken) {
      return;
    }
    const ac = new AbortController();
    const timer = window.setTimeout(async () => {
      setCepStatus("Buscando endereço…");
      try {
        const path = modoGoogle
          ? `/v1/auth/google/cep/${soDigitos(cep)}`
          : `/v1/auth/cadastro/cep/${soDigitos(cep)}`;
        const endereco = await apiJson<EnderecoCep>(
          path,
          {
            method: "GET",
            headers: modoGoogle && registroToken ? { "X-Registro-Token": registroToken } : undefined,
            signal: ac.signal,
          },
          { skipAuth: true },
        );
        setLogradouro(endereco.logradouro ?? "");
        setBairro(endereco.bairro ?? "");
        setCidade(endereco.cidade ?? "");
        setUf(endereco.uf ?? "");
        setPais(endereco.pais || "Brasil");
        setCepStatus("");
      } catch (falha) {
        if (ac.signal.aborted) {
          return;
        }
        setCepStatus(falha instanceof Error ? falha.message : "CEP não encontrado.");
      }
    }, 350);
    return () => {
      window.clearTimeout(timer);
      ac.abort();
    };
  }, [cep, modoGoogle, pronto, registroToken]);

  async function enviar(event: FormEvent) {
    event.preventDefault();
    setErro("");
    setEmailErro("");
    setEmailConfirmErro("");
    const alertaPrincipal = alertaEmail(email);
    const alertaConfirmacao = alertaEmail(emailConfirmacao);
    setEmailErro(alertaPrincipal);
    setEmailConfirmErro(alertaConfirmacao);
    if (alertaPrincipal || alertaConfirmacao) {
      setErro("Informe um e-mail válido.");
      return;
    }
    if (senha !== senhaConfirmacao) {
      setErro("A confirmação da senha não confere.");
      return;
    }
    if (emailConfirmacao.trim().toLowerCase() !== email.trim().toLowerCase()) {
      setErro(modoGoogle ? "Confirme o e-mail principal da conta Google." : "A confirmação do e-mail não confere.");
      return;
    }
    if (soDigitos(cep).length !== 8) {
      setErro("Informe um CEP válido.");
      return;
    }
    if (modoGoogle && !registroToken) {
      return;
    }
    setEnviando(true);
    try {
      const resposta = await apiJson<LoginApiResponse>(
        modoGoogle ? "/v1/auth/google/cadastro" : "/v1/auth/cadastro",
        {
          method: "POST",
          body: JSON.stringify(
            modoGoogle
              ? {
                  registroToken,
                  nome,
                  senha,
                  senhaConfirmacao,
                  emailConfirmacao,
                  telefonePrincipal: normalizarTelefone(telefone),
                  cep,
                  endereco: logradouro,
                  numero,
                  bairro,
                  cidade,
                  uf,
                  pais,
                }
              : {
                  nome,
                  email,
                  emailConfirmacao,
                  telefonePrincipal: normalizarTelefone(telefone),
                  senha,
                  senhaConfirmacao,
                  cep,
                  endereco: logradouro,
                  numero,
                  bairro,
                  cidade,
                  uf,
                  pais,
                },
          ),
        },
        { skipAuth: true },
      );
      if (!resposta.token) {
        setErro("Cadastro concluído, mas a API não devolveu o token.");
        setEnviando(false);
        return;
      }
      guardarJwt(resposta.token);
      limparRegistroGoogle();
      router.replace("/dashboard");
    } catch (falha) {
      setErro(falha instanceof Error ? falha.message : "Não foi possível concluir o cadastro.");
      setEnviando(false);
    }
  }

  if (!pronto) {
    return (
      <AuthShell reiniciarAoInicio>
        <span className="section-kicker">NOVO USUÁRIO</span>
        <h2>Complete seu cadastro</h2>
        <p className="feedback" role="status">
          Abrindo o cadastro…
        </p>
      </AuthShell>
    );
  }

  if (enviando) {
    return (
      <main className="shell">
        <section className="login-panel" style={{ gridColumn: "1 / -1" }}>
          <div className="form-wrap">
            <span className="section-kicker">AUTONOMOUS AUDIT</span>
            <h2>Preparando acesso</h2>
            <p className="feedback" role="status">
              Gravando seu cadastro e liberando o ambiente…
            </p>
          </div>
        </section>
      </main>
    );
  }

  return (
    <AuthShell reiniciarAoInicio>
      <span className="section-kicker">NOVO USUÁRIO</span>
      <h2>Complete seu cadastro</h2>
      <p style={{ color: "var(--muted)", marginTop: 0 }}>
        {modoGoogle
          ? "Sua conta Google ainda não está na base. Confirme os dados, defina a senha e informe o endereço."
          : "Informe nome, e-mail, telefone, senha e endereço. Em seguida o sistema libera o acesso e abre o dashboard."}
      </p>
      {foto ? (
        <div className="cadastro-foto">
          <img src={foto} alt="" width={72} height={72} referrerPolicy="no-referrer" />
          <span>Foto da conta Google</span>
        </div>
      ) : null}
      <form noValidate onSubmit={enviar}>
        <div className="field">
          <label htmlFor="nome">Nome</label>
          <div className="input-wrap">
            <input id="nome" value={nome} onChange={(e) => setNome(e.target.value)} required />
          </div>
        </div>
        <div className="field">
          <label htmlFor="email">E-mail principal</label>
          <div className="input-wrap">
            <input
              id="email"
              type="email"
              value={email}
              readOnly={modoGoogle}
              disabled={modoGoogle}
              onChange={(e) => {
                setEmail(e.target.value);
                setEmailErro("");
              }}
              onBlur={() => setEmailErro(alertaEmail(email))}
              aria-invalid={emailErro ? true : undefined}
              required
            />
          </div>
          {emailErro ? (
            <span className="error" role="alert">
              {emailErro}
            </span>
          ) : null}
        </div>
        <div className="field">
          <label htmlFor="emailConfirmacao">Confirme o e-mail</label>
          <div className="input-wrap">
            <input
              id="emailConfirmacao"
              type="email"
              value={emailConfirmacao}
              onChange={(e) => {
                setEmailConfirmacao(e.target.value);
                setEmailConfirmErro("");
              }}
              onBlur={() => setEmailConfirmErro(alertaEmail(emailConfirmacao))}
              aria-invalid={emailConfirmErro ? true : undefined}
              required
            />
          </div>
          {emailConfirmErro ? (
            <span className="error" role="alert">
              {emailConfirmErro}
            </span>
          ) : null}
        </div>
        <div className="field">
          <label htmlFor="telefone">Telefone celular (principal)</label>
          <div className="input-wrap">
            <input
              id="telefone"
              value={telefone}
              onChange={(e) => setTelefone(e.target.value)}
              placeholder="11987654321"
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="cep">CEP</label>
          <div className="input-wrap">
            <input
              id="cep"
              inputMode="numeric"
              value={cep}
              onChange={(e) => setCep(formatarCep(e.target.value))}
              placeholder="00000-000"
              required
            />
          </div>
          {cepStatus ? (
            <span className="caps" role="status">
              {cepStatus}
            </span>
          ) : null}
        </div>
        <div className="field">
          <label htmlFor="logradouro">Endereço</label>
          <div className="input-wrap">
            <input
              id="logradouro"
              value={logradouro}
              onChange={(e) => setLogradouro(e.target.value)}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="numero">Número</label>
          <div className="input-wrap">
            <input id="numero" value={numero} onChange={(e) => setNumero(e.target.value)} required />
          </div>
        </div>
        <div className="field">
          <label htmlFor="bairro">Bairro</label>
          <div className="input-wrap">
            <input id="bairro" value={bairro} onChange={(e) => setBairro(e.target.value)} />
          </div>
        </div>
        <div className="field">
          <label htmlFor="cidade">Cidade</label>
          <div className="input-wrap">
            <input id="cidade" value={cidade} onChange={(e) => setCidade(e.target.value)} required />
          </div>
        </div>
        <div className="field">
          <label htmlFor="uf">UF</label>
          <div className="input-wrap">
            <input
              id="uf"
              value={uf}
              maxLength={2}
              onChange={(e) => setUf(e.target.value.toUpperCase())}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="pais">País</label>
          <div className="input-wrap">
            <input id="pais" value={pais} onChange={(e) => setPais(e.target.value)} required />
          </div>
        </div>
        <div className="field">
          <label htmlFor="senha">Senha</label>
          <div className="input-wrap">
            <input
              id="senha"
              type="password"
              autoComplete="new-password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              minLength={8}
              required
            />
          </div>
        </div>
        <div className="field">
          <label htmlFor="senhaConfirmacao">Repita a senha</label>
          <div className="input-wrap">
            <input
              id="senhaConfirmacao"
              type="password"
              autoComplete="new-password"
              value={senhaConfirmacao}
              onChange={(e) => setSenhaConfirmacao(e.target.value)}
              minLength={8}
              required
            />
          </div>
        </div>
        {erro ? (
          <p className="feedback" role="alert">
            {erro}
          </p>
        ) : null}
        <button className="submit" type="submit">
          <span>Avançar</span>
          <span aria-hidden="true">↗</span>
        </button>
      </form>
      <p>
        <VoltarAoLoginButton />
      </p>
    </AuthShell>
  );
}
