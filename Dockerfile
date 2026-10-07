# ------------------------------------------------------------
# Build
# ------------------------------------------------------------

FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0-alpine3.24@sha256:3cc3bbbbf93d82104892f42aa9106b6be4d120346dea0649643a97c801525256 AS build

ARG TARGETARCH
ARG TARGETVARIANT
ARG APP_VERSION=0.0.1

WORKDIR /src
COPY . .

RUN dotnet publish \
    source/ProjectMemoryProxy.Server/ProjectMemoryProxy.Server.csproj \
    -p:DebugType=embedded \
    -p:Version="${APP_VERSION}" \
    -c Release \
    -a "${TARGETARCH}" \
    --verbosity n \
    --self-contained true \
    -o /app/publish




# ------------------------------------------------------------
# Runtime
# ------------------------------------------------------------

FROM alpine:3.24@sha256:28bd5fe8b56d1bd048e5babf5b10710ebe0bae67db86916198a6eec434943f8b AS final

RUN apk add --upgrade --no-cache \
        ca-certificates-bundle \
        curl \
        libgcc \
        libssl3 \
        libstdc++ \
        zlib \
    && mkdir -p /app /data \
    && chmod 0777 /data

WORKDIR /app

COPY --from=build /app/publish/ ./

ENV \
    ASPNETCORE_HTTP_PORTS=8000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true \
    ProjectMemoryProxy__DataDirectory=/data

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl --fail --silent http://127.0.0.1:8000/health/ready > /dev/null || exit 1

EXPOSE 8000
VOLUME ["/data"]
ENTRYPOINT ["./ProjectMemoryProxy.Server"]
