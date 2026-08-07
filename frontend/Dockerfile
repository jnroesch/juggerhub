# JuggerHub web frontend — multi-stage build.
# Build context is ./frontend (see docker-compose.yml).

# --- deps: install node_modules (cached on lockfile) ------------------------
# Node pinned to match frontend/.nvmrc, package.json "engines", and CI.
FROM node:26.5.0-alpine AS deps
WORKDIR /app
ENV NX_DAEMON=false
# Optional egress-proxy CA. Behind a TLS-terminating egress proxy (e.g. Claude Code on
# the web) `npm ci` fails with SELF_SIGNED_CERT_IN_CHAIN unless the proxy's CA is trusted.
# The CA arrives as an OPTIONAL build secret (`proxy_ca`, wired in docker-compose.yml from
# ${PROXY_CA_FILE:-./.proxy-ca-empty}); npm reads it via NODE_EXTRA_CA_CERTS. With no
# secret the file is empty and this is a no-op, so ordinary local builds and CI are
# unaffected. Node is used directly (no apk/update-ca-certificates) — installing that
# package would itself need the CA that is not yet trusted.
RUN --mount=type=secret,id=proxy_ca,required=false \
    if [ -s /run/secrets/proxy_ca ]; then cp /run/secrets/proxy_ca /usr/local/share/proxy-ca.pem; \
    else : > /usr/local/share/proxy-ca.pem; fi
ENV NODE_EXTRA_CA_CERTS=/usr/local/share/proxy-ca.pem
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
# Official Playwright image bundles browsers matching the installed version, so this tag
# and the @playwright/test version must move together. @playwright/test is pinned exactly
# in package.json for that reason: under a caret range any lockfile regeneration can float
# it past this image, and the mismatch fails every test at browser launch rather than at
# install — which reads as a total app failure instead of a version skew.
FROM mcr.microsoft.com/playwright:v1.62.1-noble AS playwright
WORKDIR /app
ENV NX_DAEMON=false CI=true
# Optional egress-proxy CA — see the note in the `deps` stage above.
RUN --mount=type=secret,id=proxy_ca,required=false \
    if [ -s /run/secrets/proxy_ca ]; then cp /run/secrets/proxy_ca /usr/local/share/proxy-ca.pem; \
    else : > /usr/local/share/proxy-ca.pem; fi
ENV NODE_EXTRA_CA_CERTS=/usr/local/share/proxy-ca.pem
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
