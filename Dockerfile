# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Claims.Domain/Claims.Domain.csproj                 src/Claims.Domain/
COPY src/Claims.Application/Claims.Application.csproj       src/Claims.Application/
COPY src/Claims.Infrastructure/Claims.Infrastructure.csproj src/Claims.Infrastructure/
COPY src/Claims.Api/Claims.Api.csproj                       src/Claims.Api/
RUN dotnet restore src/Claims.Api/Claims.Api.csproj

COPY src/ src/
RUN dotnet publish src/Claims.Api/Claims.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app .
ENTRYPOINT ["dotnet", "Claims.Api.dll"]
