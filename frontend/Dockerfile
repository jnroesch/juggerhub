# JuggerHub web frontend — multi-stage build.
# Build context is ./frontend (see docker-compose.yml).

# --- deps: install node_modules (cached on lockfile) ------------------------
# Node pinned to match frontend/.nvmrc, package.json "engines", and CI.
FROM node:26.5.0-alpine AS deps
WORKDIR /app
ENV NX_DAEMON=false
COPY package.json package-lock.json ./
RUN npm ci

# --- build: produce the static SPA ------------------------------------------
FROM deps AS build
COPY . .
RUN npx nx build web --configuration=production

# --- test: Jest unit tests (used by docker-compose.test.yml) ----------------
FROM deps AS test
ENV CI=true
COPY . .
ENTRYPOINT ["npx", "nx", "test", "web", "--watch=false"]

# --- e2e: Playwright against the running stack (desktop + mobile) ------------
# Official Playwright image bundles browsers matching the installed version.
FROM mcr.microsoft.com/playwright:v1.61.1-noble AS playwright
WORKDIR /app
ENV NX_DAEMON=false CI=true
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ENTRYPOINT ["npx", "nx", "e2e", "web-e2e"]

# --- runtime: nginx serving the SPA + proxying /api -------------------------
FROM nginx:1.31.3-alpine AS runtime
# Config is a TEMPLATE, not a finished file: the nginx image's own entrypoint
# (/docker-entrypoint.d/20-envsubst-on-templates.sh) renders /etc/nginx/templates/*.template
# into /etc/nginx/conf.d/ at container start. That is what lets one built image carry
# per-environment analytics config (feature 033) with no rebuild and no added shell script.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/apps/web/browser /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
