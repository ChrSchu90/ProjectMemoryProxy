#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")" || exit

DOCKER_FILE=Dockerfile
IMAGE_NAME=project-memory-proxy:dev
APP_VERSION=0.0.1-debug
DOCKER_PLATFORM=linux/amd64 # linux/amd64 linux/arm64/v8 linux/arm/v7

docker buildx build --load --progress=plain --platform "${DOCKER_PLATFORM}" --build-arg APP_VERSION="${APP_VERSION}" -f "${DOCKER_FILE}" -t "${IMAGE_NAME}" . && \
  docker volume create project-memory-proxy_data && \
  docker run --rm -it --platform "${DOCKER_PLATFORM}" --user "1000:1000" --env-file ./debug/.env -p 47111:8080 -v project-memory-proxy_data:/data "${IMAGE_NAME}"