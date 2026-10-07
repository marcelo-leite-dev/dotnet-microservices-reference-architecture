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
COPY src/Services/Catalog/Catalog.Contracts/*.csproj src/Services/Catalog/Catalog.Contracts/
COPY src/Services/Ordering/Ordering.Domain/*.csproj src/Services/Ordering/Ordering.Domain/
COPY src/Services/Ordering/Ordering.Contracts/*.csproj src/Services/Ordering/Ordering.Contracts/
COPY src/Services/Ordering/Ordering.Application/*.csproj src/Services/Ordering/Ordering.Application/
COPY src/Services/Ordering/Ordering.Infrastructure/*.csproj src/Services/Ordering/Ordering.Infrastructure/
COPY src/Services/Ordering/Ordering.Api/*.csproj src/Services/Ordering/Ordering.Api/
RUN dotnet restore src/Services/Ordering/Ordering.Api/Ordering.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Catalog/Catalog.Contracts/ src/Services/Catalog/Catalog.Contracts/
COPY src/Services/Ordering/ src/Services/Ordering/
RUN dotnet publish src/Services/Ordering/Ordering.Api/Ordering.Api.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# Non-root user shipped with the official .NET 8 images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Ordering.Api.dll"]
