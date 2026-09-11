# ============================================================
# Build stage - compila Angular com SSR habilitado
# ============================================================
FROM node:22-alpine AS build
WORKDIR /app

# A imagem e construida na propria VM de producao, que tem 1 nucleo e 954MB de
# RAM. Sem teto, o Node do `ng build` cresce ate o limite da maquina, empurra o
# container do SSR pro swap e o site passa a responder em segundos ou a devolver
# 504 no meio do deploy — foi o que aconteceu em producao.
#
# 640MB de heap e o suficiente pra este bundle e deixa folga pro resto da VM.
# Se o build passar a falhar com "JavaScript heap out of memory", o caminho nao
# e subir este numero: e construir a imagem fora da VM.
ENV NODE_OPTIONS=--max-old-space-size=640

COPY package*.json ./
# --prefer-offline e --no-audit cortam trabalho e rede que nao mudam o resultado.
RUN npm ci --prefer-offline --no-audit --no-fund

COPY . .
RUN npx ng build --configuration=production

# ============================================================
# Runtime stage - Node Express servindo SSR + estaticos
# ============================================================
FROM node:22-alpine
WORKDIR /app

# Copiar apenas o output (browser + server) do stage de build
COPY --from=build /app/dist/connect-veiculos.web ./dist/connect-veiculos.web

# Angular 17+ gera o template como `index.csr.html`, mas o middleware
# express.static tem fallback hardcoded em `index.html`. Disponibiliza
# o mesmo arquivo nos dois nomes para o servidor encontrar em qualquer caminho.
RUN cp ./dist/connect-veiculos.web/browser/index.csr.html \
       ./dist/connect-veiculos.web/browser/index.html

# Copiar package.json minimo so com dependencias de runtime do server.mjs
# (Express e dependencias do Angular SSR ja estao bundled em server.mjs)
COPY --from=build /app/package*.json ./

# Instalar somente prod deps necessarias pelo SSR (express principalmente)
RUN npm ci --omit=dev --prefer-offline --no-audit --no-fund

ENV NODE_ENV=production
ENV PORT=4000
EXPOSE 4000

CMD ["node", "dist/connect-veiculos.web/server/server.mjs"]
