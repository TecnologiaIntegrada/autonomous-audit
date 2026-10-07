"use client";

import { encerrarSessao } from "@/lib/sessao";

type DashboardUser = {
  name?: string | null;
  email?: string | null;
  image?: string | null;
};

type DashboardShellProps = {
  user: DashboardUser;
};

export function DashboardShell({ user }: DashboardShellProps) {
  const displayName = user.name ?? user.email ?? "Usuário";
  const initial = displayName.charAt(0).toUpperCase();

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <a className="brand" href="/dashboard" aria-label="Canada Software início">
          <img
            className="brand-mark"
            src="/favicon.svg"
            alt=""
            width={40}
            height={40}
          />
          <span className="wordmark">
            canada<span>software</span>
          </span>
        </a>
        <div className="dashboard-user">
          {user.image ? (
            <img
              className="avatar"
              src={user.image}
              alt=""
              width={40}
              height={40}
              referrerPolicy="no-referrer"
            />
          ) : (
            <span className="avatar avatar-fallback" aria-hidden="true">
              {initial}
            </span>
          )}
          <div className="dashboard-user-copy">
            <strong>{displayName}</strong>
            {user.email ? <span>{user.email}</span> : null}
          </div>
          <button className="ghost-button" type="button" onClick={() => void encerrarSessao()}>
            Sair
          </button>
        </div>
      </header>

      <main className="dashboard-main">
        <span className="section-kicker">AUTONOMOUS AUDIT</span>
        <h1>Dashboard</h1>
        <p>Você está autenticado. O conteúdo desta área será adicionado nas próximas etapas.</p>
        <div className="empty-panel">
          <span className="empty-mark" aria-hidden="true">
            ✳
          </span>
          <h2>Nada por aqui ainda</h2>
          <p>Esta tela está pronta para receber os módulos do Autonomous Audit.</p>
        </div>
      </main>
    </div>
  );
}
