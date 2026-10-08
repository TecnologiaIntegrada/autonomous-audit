# Autonomous Audit

Sistema web de **gestão de recibos e notas fiscais**: o usuário envia PDF ou imagens, a extração estrutura fornecedor, itens e totais, e o resultado fica consultável em cadastros e relatório exportável (CSV/XLSX).

Este repositório é a **apresentação pública do código** (TCC / avaliação). A operação contínua (Actions, secrets de produção, cluster) permanece nos repositórios **privados** de manutenção.

**Repositório:** https://github.com/TecnologiaIntegrada/autonomous-audit

## Endereços

| Recurso | URL | Descrição |
| --- | --- | --- |
| Interface | https://autonomousaudit.canada-software.com.br/ | Aplicação web publicada para avaliação. |
| API (Swagger) | https://autonomousauditapi.canada-software.com.br/swagger/index.html | Documentação e contrato da API. |
| Protótipo navegável | https://autonomousaudit.canada-software.com.br/prototipo/ | Protótipo HTML da interface, sem conexão com a API. |
| Vídeo de apresentação do projeto | https://youtu.be/VUK6SH74ulw | Visão do projeto e da solução desenvolvida. |
| Vídeo de apresentação do sistema (utilização) | https://youtu.be/ZBbwEPGrZdU | Ciclo de uso da aplicação: login, recibos, cadastros e relatório. |

## Vídeo de apresentação

<p align="center">
  <a href="https://www.youtube.com/watch?v=VUK6SH74ulw">
    <img src="https://img.youtube.com/vi/VUK6SH74ulw/hqdefault.jpg" alt="Vídeo de apresentação do Autonomous Audit" width="560" height="315">
  </a>
</p>

A miniatura acima abre o mesmo vídeo em [YouTube](https://www.youtube.com/watch?v=VUK6SH74ulw).

## O que o sistema faz

Recibos em papel, fotos de celular e arquivos PDF de nota fiscal chegam desestruturados. Para conferir uma despesa, o operador precisa interpretar o documento e transcrever fornecedor ou prestador, itens, totais e campos fiscais. Esse lançamento manual gera nomes inconsistentes, cadastros duplicados e dificuldade para localizar o arquivo que originou o registro. O Autonomous Audit recebe o comprovante, extrai os dados em segundo plano e organiza a compra para consulta, correção nos cadastros e exportação de relatório, conservando a referência ao documento original.

1. Autentica o usuário (e-mail/senha, Google).
2. Recebe um ou mais arquivos (imagens ou PDF até 50 MB) em **Adicionar Recibo / NF**, inclusive por QR no celular.
3. Enfileira o processamento (NATS), consulta o modelo de extração (Perplexity) e persiste a **compra** com itens, fornecedor/prestador e blocos fiscais quando houver NF-e/NFS-e.
4. Mantém CRUDs de fornecedores/prestadores, produtos e serviços, com regras de exclusão por vínculo.
5. Consulta e exporta o **relatório** de compras do período (rascunhos não entram).

### Fluxo de funcionamento

```mermaid
flowchart TD
  A[Usuário envia recibo ou NF] --> B[API aceita o arquivo e grava a compra]
  B --> C[Publica o trabalho na fila NATS]
  C --> D[Worker prepara o documento]
  D --> E[Perplexity extrai fornecedor, itens e totais]
  E --> F[Compra, cadastros e arquivos persistidos]
  F --> G[Usuário confere lista, cadastros e relatório]
```

## Arquitetura (visão)

Front: Next.js 15, React 19, TypeScript, NextAuth.  
Back: ASP.NET Core 8, Clean Architecture, MediatR, EF Core 8 / Npgsql.

O fluxo entre os pods do cluster está em **[k8s/README.md](k8s/README.md)**.

## Estrutura deste repositório

```
backend/     API .NET, Dockerfile e manifests de origem
frontend/    Interface Next.js, Dockerfile e .env.example
k8s/         Pods Kubernetes, Services e modelos de Secret
README.md    Este guia
```

## Dependências de terceiros

Crie contas e preencha apenas arquivos **locais** (`appsettings.Development.json`, `.env.local`, `k8s/*-secret.local.yaml`).

| Serviço | Uso | Onde configurar |
| --- | --- | --- |
| **PostgreSQL 16** | Persistência | `ConnectionStrings:Default` / Secret `postgres-secret` |
| **NATS 2 (JetStream)** | Filas (processar compra, Dropbox, e-mail, Perplexity) | `Nats:Url` / Secret `nats-secret` |
| **Perplexity** | Extração do comprovante | `Perplexity:ApiKey` |
| **Dropbox** | Armazenamento do PDF gerado | `Dropbox:AppKey`, `AppSecret`, tokens |
| **Google Cloud OAuth** | Login Google (Client ID + Secret) | API `Google:ClientId`; front `GOOGLE_CLIENT_*` |
| **SMTP (Titan ou equivalente)** | E-mail de verificação / recuperação | `Smtp:Titan:*` |
| **SMSDev** | Código SMS (opcional) | `SmsDev:ApiKey` |
| **Seq** | Logs estruturados (opcional) | `Seq:ServerUrl`, `Seq:ApiKey` |
| **ViaCEP** | Autocompletar endereço (público, sem chave) | `ViaCep:BaseUrl` |
| **Cloudflare Tunnel** | HTTPS público (produção) | Realizar via painel CF para exposição da api e sistema via tunelling |

Também é necessário gerar **JWT SigningKey** (≥ 32 caracteres) e **NEXTAUTH_SECRET** (≥ 32 caracteres).

## Pré-requisitos locais

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (o Dockerfile do front usa Node 24)
- PostgreSQL 16 (serviço local ou Docker)
- NATS 2 com JetStream (serviço local ou Docker), se for exercitar o processamento assíncrono
- Git

Opcional para o cluster: Docker, kubectl e Minikube (ou Kubernetes compatível).

## Instalação local (passo a passo)

### 1. Clonar

```bash
git clone https://github.com/TecnologiaIntegrada/autonomous-audit.git
cd autonomous-audit
```

### 2. Banco

Crie o banco `appdb` e um usuário com senha **só na sua máquina**.

```sql
CREATE USER app WITH PASSWORD 'sua-senha-local';
CREATE DATABASE appdb OWNER app;
```

A API aplica as migrações EF Core na inicialização (ambiente de desenvolvimento).

### 3. Backend

```bash
cd backend
dotnet restore
```

Crie `src/AutonomousAudit.Api/appsettings.Development.json` **localmente**:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=appdb;Username=app;Password=sua-senha-local"
  },
  "Jwt": {
    "SigningKey": "substitua-por-32-caracteres-ou-mais"
  },
  "Nats": {
    "Url": "nats://localhost:4222"
  }
}
```

Preencha Perplexity, Dropbox, SMTP e demais chaves só se for testar essas integrações. Campos vazios no `appsettings.json` versionado são intencionais.

```bash
dotnet run --project src/AutonomousAudit.Api
```

- Swagger: `http://localhost:5184/swagger`
- Saúde: `http://localhost:5184/health`

### 4. Frontend

```bash
cd frontend
cp .env.example .env.local
```

Edite `.env.local`:

```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184/
NEXTAUTH_URL=http://localhost:3000
NEXTAUTH_SECRET=substitua-por-32-caracteres-ou-mais
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
```

No Google Cloud Console, se usar login Google, autorize `http://localhost:3000` e o callback `/api/auth/callback/google`.

```bash
npm install
npm run dev
```

Abra `http://localhost:3000`. Sem Perplexity configurado, o envio de recibo sobe o arquivo mas a extração não conclui.

### 5. Processamento completo (opcional)

Suba NATS com JetStream, preencha `Perplexity:ApiKey` e `Dropbox:*`, e envie um PDF de teste em **Adicionar Recibo / NF**. O status do recibo deve ir de processamento a processado (ou falha visível).

## Kubernetes

Instruções dos pods, Services, NodePorts e Secrets: **[k8s/README.md](k8s/README.md)**.

Resumo: namespace `dotnet`; pods `postgres`, `nats`, `nats-ui`, `api`, `frontend`; NodePorts 30081 (API), 30085 (front), 30082 (NATS UI). Secrets só no cluster, a partir dos `*.example.yaml`.

## Licença e uso acadêmico

Código apresentado para avaliação do Projeto Integrado / TCC (PUC Minas). O ambiente publicado de demonstração é o indicado nas URLs acima.
