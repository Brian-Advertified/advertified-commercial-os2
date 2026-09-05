FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build

WORKDIR /src

COPY ["Directory.Build.props", "global.json", "./"]
COPY ["api/Advertified.Commercial.Api.csproj", "api/packages.lock.json", "api/"]
COPY ["api/src/Advertified.Commercial.Application/Advertified.Commercial.Application.csproj", "api/src/Advertified.Commercial.Application/packages.lock.json", "api/src/Advertified.Commercial.Application/"]
COPY ["api/src/Advertified.Commercial.DatabaseMigrator/Advertified.Commercial.DatabaseMigrator.csproj", "api/src/Advertified.Commercial.DatabaseMigrator/packages.lock.json", "api/src/Advertified.Commercial.DatabaseMigrator/"]
COPY ["api/src/Advertified.Commercial.Domain/Advertified.Commercial.Domain.csproj", "api/src/Advertified.Commercial.Domain/packages.lock.json", "api/src/Advertified.Commercial.Domain/"]
COPY ["api/src/Advertified.Commercial.Infrastructure/Advertified.Commercial.Infrastructure.csproj", "api/src/Advertified.Commercial.Infrastructure/packages.lock.json", "api/src/Advertified.Commercial.Infrastructure/"]

RUN dotnet restore api/Advertified.Commercial.Api.csproj --locked-mode
RUN dotnet restore api/src/Advertified.Commercial.DatabaseMigrator/Advertified.Commercial.DatabaseMigrator.csproj --locked-mode

COPY api/ api/
COPY shared/contracts/master-data.json shared/contracts/master-data.json

RUN dotnet publish api/Advertified.Commercial.Api.csproj \
    --configuration Release \
    --no-restore \
    --no-self-contained \
    --output /out/api \
    -p:UseAppHost=false
RUN dotnet publish api/src/Advertified.Commercial.DatabaseMigrator/Advertified.Commercial.DatabaseMigrator.csproj \
    --configuration Release \
    --no-restore \
    --no-self-contained \
    --output /out/migrator \
    -p:UseAppHost=false

# Explicit offline/unit validation target. No Docker socket or live service credentials
# are mounted; database/container integration fixtures require separate authorization.
FROM build AS tests
COPY shared/ shared/
COPY ["AGENTS.md", "ADVERTIFIED.md", "./"]
ARG TEST_FILTER
RUN test -n "$TEST_FILTER"
RUN dotnet restore api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj --locked-mode
RUN dotnet test api/tests/Advertified.Commercial.Api.Tests/Advertified.Commercial.Api.Tests.csproj \
    --configuration Release --no-restore --filter "$TEST_FILTER" --verbosity normal

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble@sha256:a4556ed033fa96f984bb7a8d348851cb2d36b1281dd2420070045f664fbb5f94 AS api

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build --chown=app:app /out/api/ ./

USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health/live HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -q ' 200 ' <&3"]
ENTRYPOINT ["dotnet", "Advertified.Commercial.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0.11-noble@sha256:a365ce6a50b09176855d085c69da3fc1204a48432e36087e9a208f6e5860e235 AS migrator

WORKDIR /app
COPY --from=build --chown=app:app /out/migrator/ ./

USER app
ENTRYPOINT ["dotnet", "Advertified.Commercial.DatabaseMigrator.dll"]
