import { withAuth } from "next-auth/middleware";
import { NextResponse } from "next/server";

export default withAuth(
  function middleware(req) {
    const token = req.nextauth.token;
    const path = req.nextUrl.pathname;

    if (token && path === "/" && req.nextUrl.searchParams.get("sair") !== "1") {
      return NextResponse.redirect(new URL("/auth/continuar", req.url));
    }

    return NextResponse.next();
  },
  {
    callbacks: {
      authorized: () => true,
    },
    pages: {
      signIn: "/",
    },
  }
);

export const config = {
  matcher: ["/", "/auth/continuar", "/cadastro"],
};
