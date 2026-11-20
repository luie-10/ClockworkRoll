# CLOCKWORK ROLL 랭킹 서버 — 의존성 없는 Node 하나
FROM node:22-alpine
WORKDIR /app
COPY ranking-server.js package.json ./
ENV PORT=8787 HOST=0.0.0.0 DATA_DIR=/data TRUST_PROXY=1
VOLUME ["/data"]
EXPOSE 8787
CMD ["node", "ranking-server.js"]
