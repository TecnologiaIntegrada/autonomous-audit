# Autonomous Audit — backend

API ASP.NET Core 8 (Minimal APIs, MediatR, EF Core 8, PostgreSQL, NATS).

A instalação completa, as dependências de terceiros e o Kubernetes estão no [README da raiz](../README.md). Os manifests deste diretório (`k8s/`) são os mesmos documentados em [`../k8s`](../k8s).

```bash
dotnet restore
dotnet run --project src/AutonomousAudit.Api
```

Swagger local: `http://localhost:5184/swagger`
