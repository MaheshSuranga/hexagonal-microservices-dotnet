# -----------------------------------------------------------------------------
# Stage 1: Runtime Base (Optimized ASP.NET 8 runtime)
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# -----------------------------------------------------------------------------
# Stage 2: Build & Restore (Layer Caching)
# -----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["InventoryApi/InventoryApi.csproj", "InventoryApi/"]
RUN dotnet restore "InventoryApi/InventoryApi.csproj"

COPY InventoryApi/ InventoryApi/
WORKDIR "/src/InventoryApi"
RUN dotnet build "InventoryApi.csproj" -c Release -o /app/build

# -----------------------------------------------------------------------------
# Stage 3: Publish
# -----------------------------------------------------------------------------
FROM build AS publish
RUN dotnet publish "InventoryApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 4: Production Image
# -----------------------------------------------------------------------------
FROM base AS final
WORKDIR /app
USER $APP_UID
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InventoryApi.dll"]
