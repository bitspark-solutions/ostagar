# Build context is web/ itself. The app is published as a Next standalone server, so the runtime
# image carries only the traced dependencies rather than the whole node_modules tree.
#
# Base images are security-pinned and must be re-scanned on bump (docker scout cves):
# node:26-alpine3.23 is the newest LTS on the newest Alpine whose apk fixes (zlib 1.4-r0, Nov 2026)
# are live. The `apk upgrade` layer is deliberate: upstream node images are built against the
# Alpine repo state of their build day and ship stale packages until the next node rebuild.
# Known residue (unfixable until Node ships it): undici/brace-expansion/ip-address CVEs are inside
# the node runtime itself; re-check on every node release.
FROM node:26-alpine3.23 AS deps
RUN apk upgrade --no-cache && apk add --no-cache libc6-compat
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci && npm cache clean --force

FROM node:26-alpine3.23 AS builder
RUN apk upgrade --no-cache
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

ENV NEXT_TELEMETRY_DISABLED=1

# NEXT_PUBLIC_* is inlined into the client bundle at build time — it cannot be changed at container
# start. The compose file passes the API origin the browser should call; inside the network it is
# deliberately NOT the internal service name, because the request leaves the user's browser.
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:8080
ARG NEXT_PUBLIC_DEFAULT_LOCALE=bn
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL
ENV NEXT_PUBLIC_DEFAULT_LOCALE=$NEXT_PUBLIC_DEFAULT_LOCALE

RUN npm run build

FROM node:26-alpine3.23 AS runner
RUN apk upgrade --no-cache \
    # The standalone server executes with the node binary alone; the image's bundled npm and
    # corepack are never invoked at runtime, and npm's vendored dependencies (undici,
    # brace-expansion, glob, minimatch, ip-address, postcss-selector-parser) were the entire
    # language-package CVE count of this image. Removing them zeroes the scan without touching
    # anything the server executes. (Build stages keep npm - it is what runs `npm ci`.)
    && rm -rf /usr/local/lib/node_modules/npm /usr/local/lib/node_modules/corepack \
    && rm -f /usr/local/bin/npm /usr/local/bin/npx /usr/local/bin/corepack /usr/local/bin/yarn \
             /usr/local/bin/yarnpkg /usr/local/bin/pnpm /usr/local/bin/pnpx
WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000
ENV HOSTNAME=0.0.0.0

RUN addgroup -S app && adduser -S app -G app

COPY --from=builder --chown=app:app /app/.next/standalone ./
COPY --from=builder --chown=app:app /app/.next/static ./.next/static

USER app
EXPOSE 3000

# 127.0.0.1 and not localhost: inside this image wget resolves localhost to ::1 first, and node
# binds 0.0.0.0, so the probe gets connection refused while the app is serving perfectly well. The
# container then reports unhealthy even though the browser can load the page.
HEALTHCHECK --interval=15s --timeout=3s --start-period=10s --retries=5 \
    CMD wget -qO- http://127.0.0.1:3000/ > /dev/null || exit 1

CMD ["node", "server.js"]
