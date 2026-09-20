# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ ./src/
RUN dotnet restore src/hosts/Host.Api/Host.Api.csproj
RUN dotnet publish src/hosts/Host.Api/Host.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=5 CMD ["dotnet", "Host.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Host.Api.dll"]
