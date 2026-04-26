# HLTV bridge only — deploy as a second service; point HLTV_BRIDGE_URL at its public https URL + trailing path.
FROM node:20-alpine
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --omit=dev
COPY server.mjs ./
ENV HLTV_BRIDGE_HOST=0.0.0.0
# Render/Railway set PORT; keep 3847 as default for docker-compose
ENV HLTV_BRIDGE_PORT=3847
EXPOSE 3847
CMD ["node", "server.mjs"]
