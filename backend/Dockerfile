FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AutonomousAudit.sln ./
COPY src/AutonomousAudit.Domain/AutonomousAudit.Domain.csproj src/AutonomousAudit.Domain/
COPY src/AutonomousAudit.Application/AutonomousAudit.Application.csproj src/AutonomousAudit.Application/
COPY src/AutonomousAudit.Infrastructure/AutonomousAudit.Infrastructure.csproj src/AutonomousAudit.Infrastructure/
COPY src/AutonomousAudit.Api/AutonomousAudit.Api.csproj src/AutonomousAudit.Api/
RUN dotnet restore src/AutonomousAudit.Api/AutonomousAudit.Api.csproj
COPY src/ src/
RUN dotnet publish src/AutonomousAudit.Api/AutonomousAudit.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AutonomousAudit.Api.dll"]
