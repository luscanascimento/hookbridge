# Stage 1: Build Angular Application
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies with caching
COPY package*.json ./
RUN npm ci

# Copy application source and build production bundle
COPY . .
RUN npm run build -- --configuration production

# Stage 2: Runtime Web Server
FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

# Clean default nginx files
RUN rm -rf /usr/share/nginx/html/*

# Copy built artifacts from modern esbuild output
COPY --from=build /app/dist/hookbridge-web/browser .
COPY nginx.conf /etc/nginx/nginx.conf

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost/healthz || exit 1

CMD ["nginx", "-g", "daemon off;"]
