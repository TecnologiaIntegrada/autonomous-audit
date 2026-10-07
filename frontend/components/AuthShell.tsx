"use client";

import { encerrarSessao } from "@/lib/sessao";

export function AuthShell({
  children,
  reiniciarAoInicio = false,
}: {
  children: React.ReactNode;
  reiniciarAoInicio?: boolean;
}) {
  return (
    <main className="shell">
      <section className="brand-panel" aria-label="Canada Software">
        <a
          className="brand"
          href="/"
          aria-label="Canada Software início"
          onClick={
            reiniciarAoInicio
              ? (event) => {
                  event.preventDefault();
                  void encerrarSessao();
                }
              : undefined
          }
        >
          <img className="brand-mark" src="/favicon.svg" alt="" width={49} height={49} />
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
            Tecnologia que simplifica.
            <br />
            Conexões que fazem a diferença.
          </p>
        </div>
        <div className="panel-footer">
          <span>FEITO PARA IR ALÉM.</span>
          <span className="footer-line" />
          <span>01 / ∞</span>
        </div>
      </section>
      <section className="login-panel">
        <div className="form-wrap">{children}</div>
      </section>
    </main>
  );
}
