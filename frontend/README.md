# Autonomous Audit — frontend

Next.js 15 (App Router), React 19 e NextAuth v4 (Google). A sessão da API é JWT.

A instalação completa, OAuth e o Kubernetes estão no [README da raiz](../README.md).

```bash
cp .env.example .env.local   # preencha localmente; nao versione
npm install
npm run dev
```

Não commite `.env`, `.env.local` nem o Secret `frontend-secret` com valores reais.
