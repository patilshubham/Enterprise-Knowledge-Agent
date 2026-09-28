# syntax=docker/dockerfile:1.7
# Build context: src/frontend
#   docker build -f src/frontend/Dockerfile src/frontend

FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite inlines VITE_* variables at build time. Default is empty (same-origin),
# relying on the nginx reverse proxy wired in TASK-FND-06 to forward /api and
# /hubs to the API service - see src/api/apiClient.ts. Override with
# `docker build --build-arg VITE_API_BASE_URL=https://example.com` if a
# deployment ever needs a cross-origin API.
ARG VITE_API_BASE_URL=""
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime

COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

# nginxinc/nginx-unprivileged already runs as a non-root user and listens on
# an unprivileged port by default; nginx.conf explicitly binds 8080 to match.
EXPOSE 8080
