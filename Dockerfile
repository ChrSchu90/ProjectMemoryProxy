# ------------------------------------------------------------
# Build
# ------------------------------------------------------------

FROM --platform=${BUILDPLATFORM} mcr.microsoft.com/dotnet/sdk:10.0-alpine3.24@sha256:5562fd626eec39f6f53218f9b7d7856ab7195d51d846568304fbe85071a3b6e6 AS build

ARG TARGETARCH
ARG TARGETVARIANT
ARG APP_VERSION

WORKDIR /src
COPY . .

RUN dotnet publish \
    source/ProjectMemoryProxy.Server/ProjectMemoryProxy.Server.csproj \
    -p:DebugType=embedded \
    -p:GenerateDocumentationFile=false \
    -p:Version="${APP_VERSION}" \
    -c Release \
    -a "${TARGETARCH}" \
    --verbosity n \
    --self-contained true \
    -o /app/publish




# ------------------------------------------------------------
# Runtime
# ------------------------------------------------------------

FROM alpine:3.24@sha256:be2656061b536c1883c4034790e21d0a09c5aaaf51968b89368a3c0371ea2c9a AS final

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

HEALTHCHECK --interval=30s --timeout=35s --start-period=30s --retries=3 \
    CMD curl --fail --silent --show-error --max-time 30 http://127.0.0.1:8000/health/ready > /dev/null || exit 1

EXPOSE 8000
VOLUME ["/data"]
ENTRYPOINT ["./ProjectMemoryProxy.Server"]
