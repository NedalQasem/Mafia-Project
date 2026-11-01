FROM node:22-alpine
WORKDIR /app
COPY package.json ./
COPY src ./src
ENV NODE_ENV=production
RUN mkdir /data && chown node:node /data   # the state volume starts with these permissions
USER node
EXPOSE 8787
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8787/health || exit 1
CMD ["node", "src/index.js"]
