FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "ConnectVeiculos.sln"
RUN dotnet publish "ConnectVeiculos.API/ConnectVeiculos.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
# Raiz ICP-Brasil v10: assina o certificado TLS das Sefaz (SP, SVRS) usadas pela
# NF-e. O Linux nao a traz; sem ela a conexao falha (unable to get local issuer).
# Conferida em 01/11/2026 contra a cadeia real de nfe.fazenda.sp.gov.br.
COPY certificados-icp/ICP-Brasilv10.crt /usr/local/share/ca-certificates/ICP-Brasilv10.crt
RUN update-ca-certificates
COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/wwwroot/uploads
ENTRYPOINT ["dotnet", "ConnectVeiculos.API.dll"]
