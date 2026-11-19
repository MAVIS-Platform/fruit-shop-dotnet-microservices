FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ["src/Services/FruitCatalog/FruitCatalog.API/FruitCatalog.API.csproj", "src/Services/FruitCatalog/FruitCatalog.API/"]
RUN dotnet restore "src/Services/FruitCatalog/FruitCatalog.API/FruitCatalog.API.csproj"

COPY . .
WORKDIR "/src/src/Services/FruitCatalog/FruitCatalog.API"
RUN dotnet publish "FruitCatalog.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
  CMD wget -qO- http://localhost:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "FruitCatalog.API.dll"]
