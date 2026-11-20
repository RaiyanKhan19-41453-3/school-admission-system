FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
ARG VITE_API_URL=http://localhost:8080
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

FROM node:22-alpine
# Run as a non-root user; the preview server only needs to read static files.
RUN addgroup -S appuser && adduser -S -G appuser appuser
WORKDIR /app
COPY --chown=appuser:appuser package*.json ./
RUN npm install
COPY --from=build --chown=appuser:appuser /app/dist ./dist
COPY --chown=appuser:appuser vite.config.js ./
RUN chown -R appuser:appuser /app && chmod -R u+w /app
EXPOSE 5173
USER appuser
CMD ["npx", "vite", "preview", "--host", "0.0.0.0", "--port", "5173"]
