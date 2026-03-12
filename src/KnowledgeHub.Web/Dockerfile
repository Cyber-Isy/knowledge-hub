# Stage 1: Build
FROM node:22-alpine AS build
WORKDIR /app

# Copy package files for layer caching
COPY src/KnowledgeHub.Web/package*.json ./

RUN npm ci

# Copy source and build
COPY src/KnowledgeHub.Web/ .

RUN npx ng build --configuration=production

# Stage 2: Serve
FROM nginx:alpine AS runtime

# Copy built files
COPY --from=build /app/dist/KnowledgeHub.Web/browser /usr/share/nginx/html

# Create nginx config for SPA routing
COPY src/KnowledgeHub.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
