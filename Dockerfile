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

# Copy project files first for efficient Docker layer caching on restores
COPY ["OrderApi/OrderApi.csproj", "OrderApi/"]
COPY ["OrderApi.Domain/OrderApi.Domain.csproj", "OrderApi.Domain/"]
RUN dotnet restore "OrderApi/OrderApi.csproj"

# Copy full source and compile
COPY . .
WORKDIR "/src/OrderApi"
RUN dotnet build "OrderApi.csproj" -c Release -o /app/build

# -----------------------------------------------------------------------------
# Stage 3: Publish (Trimmed binaries, ready for production)
# -----------------------------------------------------------------------------
FROM build AS publish
RUN dotnet publish "OrderApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Stage 4: Production Image
# -----------------------------------------------------------------------------
FROM base AS final
WORKDIR /app

# Prepare persistent data directory for SQLite database volume
USER root
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app/data

# Run as non-root user for security (built-in .NET 8 'app' user)
USER $APP_UID

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "OrderApi.dll"]
