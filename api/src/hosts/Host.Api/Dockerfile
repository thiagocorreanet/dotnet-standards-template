# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /src
# nuget.config só existe no projeto gerado (pacotes ModularApi.* de feed privado); o glob torna a cópia opcional.
COPY Directory.Build.props Directory.Build.targets Directory.Packages.props global.json nuget.confi[g] ./
COPY src/ ./src/
# Credenciais do feed por secret de build (docker build --secret / compose build.secrets); nunca ficam na imagem.
RUN --mount=type=secret,id=modularapi_feed_user,env=MODULARAPI_FEED_USER,required=false \
    --mount=type=secret,id=modularapi_feed_token,env=MODULARAPI_FEED_TOKEN,required=false \
    dotnet restore src/hosts/Host.Api/Host.Api.csproj
RUN dotnet publish src/hosts/Host.Api/Host.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=5 CMD ["dotnet", "Host.Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Host.Api.dll"]
