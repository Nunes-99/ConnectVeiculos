# ============================================================
# Build stage - compila Angular com SSR habilitado
# ============================================================
FROM node:22-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npx ng build --configuration=production

# ============================================================
# Runtime stage - Node Express servindo SSR + estaticos
# ============================================================
FROM node:22-alpine
WORKDIR /app

# Copiar apenas o output (browser + server) do stage de build
COPY --from=build /app/dist/connect-veiculos.web ./dist/connect-veiculos.web

# Copiar package.json minimo so com dependencias de runtime do server.mjs
# (Express e dependencias do Angular SSR ja estao bundled em server.mjs)
COPY --from=build /app/package*.json ./

# Instalar somente prod deps necessarias pelo SSR (express principalmente)
RUN npm ci --omit=dev

ENV NODE_ENV=production
ENV PORT=4000
EXPOSE 4000

CMD ["node", "dist/connect-veiculos.web/server/server.mjs"]
