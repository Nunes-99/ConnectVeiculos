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
COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/wwwroot/uploads
ENTRYPOINT ["dotnet", "ConnectVeiculos.API.dll"]
