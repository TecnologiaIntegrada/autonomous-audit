"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { signIn } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { apiLogin, guardarJwt, lerJwt, limparJwt, limparRegistroGoogle } from "@/lib/api";
import { alertaEmail } from "@/lib/contato";
import { limparCookiesAuth } from "@/lib/sessao";

const oauthErrorMessages: Record<string, string> = {
  OAuthSignin: "Não foi possível iniciar o login com Google. Tente novamente.",
  OAuthCallback: "O retorno do Google falhou. Confira a URI de redirecionamento no console OAuth.",
  OAuthCreateAccount: "Não foi possível criar a sessão com a conta Google.",
  Callback: "Houve um erro ao concluir a autenticação.",
  AccessDenied: "O acesso com Google foi recusado.",
  Configuration: "A autenticação OAuth ainda não está configurada corretamente.",
  Default: "Não foi possível entrar com Google. Tente novamente.",
};

export function LoginScreen() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [emailError, setEmailError] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const [capsLock, setCapsLock] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [feedback, setFeedback] = useState("");
  const [desafio, setDesafio] = useState<string | null>(null);
  const [smsCode, setSmsCode] = useState("");
  const [emailCode, setEmailCode] = useState("");

  const year = useMemo(() => new Date().getFullYear(), []);

  useEffect(() => {
    if (searchParams.get("sair") === "1") {
      limparJwt();
      limparRegistroGoogle();
      limparCookiesAuth();
      window.history.replaceState({}, "", "/");
      return;
    }
    if (lerJwt()) {
      router.replace("/dashboard");
    }
  }, [router, searchParams]);

  useEffect(() => {
    const oauthError = searchParams.get("error");
    if (!oauthError) {
      return;
    }
    setFeedback(oauthErrorMessages[oauthError] ?? oauthErrorMessages.Default);
  }, [searchParams]);

  function clearFieldErrors() {
    setEmailError("");
    setPasswordError("");
    setFeedback("");
  }

  async function handlePasswordLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextEmailError = alertaEmail(email);
    const nextPasswordError = password ? "" : "Digite sua senha.";
    setEmailError(nextEmailError);
    setPasswordError(nextPasswordError);
    if (nextEmailError || nextPasswordError) {
      return;
    }

    setSubmitting(true);
    try {
      const precisaSms = Boolean(desafio?.includes("sms"));
      const precisaEmail = Boolean(desafio?.includes("email"));
      const resposta = await apiLogin(email.trim(), password, {
        sms: precisaSms ? smsCode : undefined,
        email: precisaEmail ? emailCode : undefined,
      });
      if (resposta.token) {
        guardarJwt(resposta.token);
        router.replace("/dashboard");
        return;
      }
      if (resposta.desafio) {
        setDesafio(resposta.desafio);
        setFeedback(resposta.mensagem ?? "Informe o código de autenticação enviado.");
        return;
      }
      setFeedback("Não foi possível entrar. Tente novamente.");
    } catch (falha) {
      setFeedback(falha instanceof Error ? falha.message : "E-mail ou senha inválidos.");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleGoogleLogin() {
    setFeedback("");
    setGoogleLoading(true);
    await signIn("google", { callbackUrl: "/auth/continuar" });
  }

  return (
    <main className="shell shell--login">
      <section className="brand-panel" aria-label="Canada Software">
        <a className="brand" href="/" aria-label="Canada Software início">
          <img
            className="brand-mark"
            src="/favicon.svg"
            alt=""
            width={49}
            height={49}
          />
          <span className="wordmark">
            canada<span>software</span>
          </span>
        </a>
        <div className="brand-content">
          <span className="eyebrow">
            <span className="tiny-cross">✳</span> Gestão de Recibos e Notas Fiscais
          </span>
          <h1>
            Autonomous
            <br />
            <em>Audit</em>
          </h1>
          <p>
            Captura estruturada de dados em documentos financeiros
          </p>
          <div className="orbit" aria-hidden="true">
            <div className="orbit-ring ring-one" />
            <div className="orbit-ring ring-two" />
            <div className="orbit-ring ring-three" />
            <span className="orbit-core">
              <img src="/favicon.svg" alt="" width={68} height={68} />
            </span>
            <span className="orbit-node node-one">+</span>
            <span className="orbit-node node-two">⌘</span>
            <span className="orbit-node node-three">↗</span>
          </div>
        </div>
        <div className="panel-footer">
          <span>FEITO PARA IR ALÉM.</span>
          <span className="footer-line" />
          <span>01 / ∞</span>
        </div>
      </section>

      <section className="login-panel">
        <div className="form-wrap">
          <div className="welcome-icon" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
              <rect x="5" y="10" width="14" height="11" rx="3" />
              <path d="M8 10V7a4 4 0 018 0v3M12 14v3" />
            </svg>
          </div>
          <h2 className="sr-only">Acessar Autonomous Audit</h2>
          <form id="login-form" noValidate onSubmit={handlePasswordLogin}>
            <div className="field">
              <label htmlFor="email">E-mail</label>
              <div className="input-wrap">
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <rect x="3" y="5" width="18" height="14" rx="3" />
                  <path d="m4 7 8 6 8-6" />
                </svg>
                <input
                  id="email"
                  name="email"
                  type="email"
                  placeholder="voce@empresa.com.br"
                  autoComplete="username"
                  required
                  aria-describedby="email-error"
                  aria-invalid={emailError ? true : undefined}
                  value={email}
                  onChange={(event) => {
                    setEmail(event.target.value);
                    clearFieldErrors();
                  }}
                  onBlur={() => setEmailError(alertaEmail(email))}
                />
              </div>
              <span id="email-error" className="error">
                {emailError}
              </span>
            </div>
            <div className="field">
              <label htmlFor="password">Senha</label>
              <div className="input-wrap">
                <svg viewBox="0 0 24 24" aria-hidden="true">
                  <rect x="5" y="10" width="14" height="11" rx="3" />
                  <path d="M8 10V7a4 4 0 018 0v3" />
                </svg>
                <input
                  id="password"
                  name="password"
                  type={showPassword ? "text" : "password"}
                  placeholder="Digite sua senha"
                  autoComplete="current-password"
                  required
                  aria-describedby="password-error"
                  aria-invalid={passwordError ? true : undefined}
                  value={password}
                  onChange={(event) => {
                    setPassword(event.target.value);
                    clearFieldErrors();
                  }}
                  onKeyUp={(event) => setCapsLock(event.getModifierState("CapsLock"))}
                  onBlur={() => setCapsLock(false)}
                />
                <button
                  className="eye"
                  type="button"
                  aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}
                  aria-pressed={showPassword}
                  onClick={() => setShowPassword((value) => !value)}
                >
                  <svg viewBox="0 0 24 24">
                    <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7S2 12 2 12Z" />
                    <circle cx="12" cy="12" r="3" />
                  </svg>
                </button>
              </div>
              <span id="password-error" className="error">
                {passwordError}
              </span>
              <span id="caps-warning" className="caps" hidden={!capsLock}>
                Caps Lock está ativado.
              </span>
            </div>
            {desafio?.includes("sms") ? (
              <div className="field">
                <label htmlFor="smsCode">Código SMS</label>
                <div className="input-wrap">
                  <input
                    id="smsCode"
                    inputMode="numeric"
                    maxLength={6}
                    autoComplete="one-time-code"
                    value={smsCode}
                    onChange={(event) => setSmsCode(event.target.value)}
                    required
                  />
                </div>
              </div>
            ) : null}
            {desafio?.includes("email") ? (
              <div className="field">
                <label htmlFor="emailCode">Código do e-mail</label>
                <div className="input-wrap">
                  <input
                    id="emailCode"
                    inputMode="numeric"
                    maxLength={6}
                    autoComplete="one-time-code"
                    value={emailCode}
                    onChange={(event) => setEmailCode(event.target.value)}
                    required
                  />
                </div>
              </div>
            ) : null}
            <div className="form-options">
              <a
                className="text-button"
                href="/cadastro"
                onClick={(event) => {
                  event.preventDefault();
                  window.location.assign("/cadastro");
                }}
              >
                Criar uma conta
              </a>
              <a className="text-button" href="/recuperar-senha">
                Esqueci minha senha
              </a>
            </div>
            <button type="submit" className="submit" disabled={submitting}>
              <span>{submitting ? "Verificando…" : "Acessar a plataforma"}</span>
              <span aria-hidden="true">↗</span>
            </button>
            {feedback ? (
              <p className="feedback" role="status" tabIndex={-1}>
                {feedback}
              </p>
            ) : null}
          </form>
          <div className="divider">
            <span>ou</span>
          </div>
          <button
            className="google-button"
            type="button"
            disabled={googleLoading}
            onClick={handleGoogleLogin}
          >
            <svg width="20" height="20" viewBox="0 0 48 48" aria-hidden="true">
              <path
                fill="#EA4335"
                d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5Z"
              />
              <path
                fill="#4285F4"
                d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6C44.4 38.03 46.98 31.87 46.98 24.55Z"
              />
              <path
                fill="#FBBC05"
                d="M10.53 28.59A14.41 14.41 0 0 1 9.75 24c0-1.59.27-3.13.78-4.59l-7.98-6.19A23.87 23.87 0 0 0 0 24c0 3.87.93 7.53 2.56 10.78l7.97-6.19Z"
              />
              <path
                fill="#34A853"
                d="M24 48c6.48 0 11.93-2.13 15.91-5.8l-7.73-6c-2.15 1.45-4.92 2.3-8.18 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48Z"
              />
            </svg>
            <span>{googleLoading ? "Redirecionando…" : "Entrar com Google"}</span>
          </button>
        </div>
        <footer className="login-footer">
          <span>© {year} Canada Software</span>
          <span>
            canada-software.com.br <span className="domain-cross">✳</span>
          </span>
        </footer>
      </section>
    </main>
  );
}
