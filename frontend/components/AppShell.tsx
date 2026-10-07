"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { useSession } from "next-auth/react";
import { usePathname, useRouter } from "next/navigation";
import { encerrarSessao } from "@/lib/sessao";
import { ApiError, apiJson, lerJwt, limparJwt, type UsuarioMe } from "@/lib/api";

type AppUser = {
  name: string;
  email?: string | null;
  image?: string | null;
};

const NAV = [
  { href: "/dashboard", label: "Indicadores" },
  { href: "/recibos/novo", label: "Adicionar Recibo / NF" },
  { href: "/recibos", label: "Listar Recibo / NF" },
  { href: "/compras", label: "Compras" },
  { href: "/fornecedores", label: "Fornecedores / Prestadores" },
  { href: "/produtos", label: "Produtos/Serviços" },
  { href: "/relatorios", label: "Relatório" },
];

function rotaAtual(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}

function navAtivo(pathname: string, href: string) {
  if (href === "/dashboard") {
    return pathname === "/dashboard";
  }
  if (href === "/recibos/novo") {
    return rotaAtual(pathname, "/recibos/novo");
  }
  if (href === "/recibos") {
    if (rotaAtual(pathname, "/recibos/novo")) {
      return false;
    }
    return rotaAtual(pathname, "/recibos");
  }
  if (href === "/compras") {
    return rotaAtual(pathname, "/compras");
  }
  return pathname === href;
}

export function AppShell({
  titulo,
  kicker = "AUTONOMOUS AUDIT",
  children,
}: {
  titulo: string;
  kicker?: string;
  children: ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const { data: sessao } = useSession();
  const [usuario, setUsuario] = useState<AppUser | null>(null);
  const [erro, setErro] = useState("");
  const [menuAberto, setMenuAberto] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const jwt = lerJwt();
    if (!jwt) {
      router.replace("/");
      return;
    }

    let cancelado = false;
    (async () => {
      try {
        const me = await apiJson<UsuarioMe>("/v1/auth/me");
        if (!cancelado) {
          setUsuario({
            name: me.nome,
            email: me.emailPrincipal,
            image: me.googlePicture ?? sessao?.user?.image,
          });
        }
      } catch (falha) {
        if (falha instanceof ApiError && falha.status === 401) {
          limparJwt();
          router.replace("/");
          return;
        }
        setErro(falha instanceof Error ? falha.message : "Não foi possível carregar o perfil.");
        setUsuario({
          name: sessao?.user?.name ?? "Usuário",
          email: sessao?.user?.email,
          image: sessao?.user?.image,
        });
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [router, sessao?.user?.email, sessao?.user?.image, sessao?.user?.name]);

  useEffect(() => {
    setMenuAberto(false);
  }, [pathname]);

  useEffect(() => {
    if (!menuAberto) {
      return;
    }
    function fechar(ev: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(ev.target as Node)) {
        setMenuAberto(false);
      }
    }
    function tecla(ev: KeyboardEvent) {
      if (ev.key === "Escape") {
        setMenuAberto(false);
      }
    }
    document.addEventListener("mousedown", fechar);
    document.addEventListener("keydown", tecla);
    return () => {
      document.removeEventListener("mousedown", fechar);
      document.removeEventListener("keydown", tecla);
    };
  }, [menuAberto]);

  if (!usuario && !erro) {
    return (
      <main className="dashboard">
        <p className="feedback" role="status" style={{ margin: 24 }}>
          Carregando…
        </p>
      </main>
    );
  }

  if (!usuario) {
    return (
      <main className="dashboard">
        <p className="feedback" role="alert" style={{ margin: 24 }}>
          {erro}
        </p>
      </main>
    );
  }

  const displayName = usuario.name;
  const initial = displayName.charAt(0).toUpperCase();

  return (
    <div className="dashboard app-shell">
      <header className="dashboard-header">
        <a className="brand" href="/dashboard" aria-label="Canada Software início">
          <img className="brand-mark" src="/favicon.svg" alt="" width={40} height={40} />
          <span className="wordmark">
            canada<span>software</span>
          </span>
        </a>
        <div className="dashboard-user user-menu" ref={menuRef}>
          <button
            className="user-menu-trigger"
            type="button"
            aria-haspopup="menu"
            aria-expanded={menuAberto}
            onClick={() => setMenuAberto((aberto) => !aberto)}
          >
            {usuario.image ? (
              <img
                className="avatar"
                src={usuario.image}
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
              {usuario.email ? <span>{usuario.email}</span> : null}
            </div>
            <span className="user-menu-caret" aria-hidden="true">
              ▾
            </span>
          </button>
          {menuAberto ? (
            <div className="user-menu-panel" role="menu">
              <a href="/configuracoes" role="menuitem">
                Configurações
              </a>
              <button type="button" role="menuitem" onClick={() => void encerrarSessao()}>
                Sair
              </button>
            </div>
          ) : null}
        </div>
      </header>

      <div className="app-layout">
        <nav className="app-nav" aria-label="Módulos">
          {NAV.map((item) => {
            const ativo = navAtivo(pathname, item.href);
            return (
              <a key={item.href} href={item.href} className={ativo ? "app-nav-link is-active" : "app-nav-link"}>
                {item.label}
              </a>
            );
          })}
        </nav>
        <main className="dashboard-main">
          <span className="section-kicker">{kicker}</span>
          <h1>{titulo}</h1>
          {children}
        </main>
      </div>
    </div>
  );
}
