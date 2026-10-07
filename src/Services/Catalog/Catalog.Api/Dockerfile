# syntax=docker/dockerfile:1
# Build context: repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached until a project file or the package versions change).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/BuildingBlocks.Domain/*.csproj src/BuildingBlocks/BuildingBlocks.Domain/
COPY src/BuildingBlocks/BuildingBlocks.Application/*.csproj src/BuildingBlocks/BuildingBlocks.Application/
COPY src/BuildingBlocks/BuildingBlocks.Messaging/*.csproj src/BuildingBlocks/BuildingBlocks.Messaging/
COPY src/BuildingBlocks/BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/BuildingBlocks.Web/*.csproj src/BuildingBlocks/BuildingBlocks.Web/
COPY src/Services/Ordering/Ordering.Contracts/*.csproj src/Services/Ordering/Ordering.Contracts/
COPY src/Services/Catalog/Catalog.Domain/*.csproj src/Services/Catalog/Catalog.Domain/
COPY src/Services/Catalog/Catalog.Contracts/*.csproj src/Services/Catalog/Catalog.Contracts/
COPY src/Services/Catalog/Catalog.Application/*.csproj src/Services/Catalog/Catalog.Application/
COPY src/Services/Catalog/Catalog.Infrastructure/*.csproj src/Services/Catalog/Catalog.Infrastructure/
COPY src/Services/Catalog/Catalog.Api/*.csproj src/Services/Catalog/Catalog.Api/
RUN dotnet restore src/Services/Catalog/Catalog.Api/Catalog.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Ordering/Ordering.Contracts/ src/Services/Ordering/Ordering.Contracts/
COPY src/Services/Catalog/ src/Services/Catalog/
RUN dotnet publish src/Services/Catalog/Catalog.Api/Catalog.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# Non-root user shipped with the official .NET 8 images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
