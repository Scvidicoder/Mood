FROM node:22-alpine AS build
WORKDIR /app

ARG VITE_API_URL=http://localhost:8080/api/v1
ARG VITE_SIGNALR_URL=http://localhost:8080/hubs/notifications
ARG VITE_CSRF_COOKIE_NAME=__Secure-MoodPickup.Csrf
ARG VITE_CSRF_HEADER_NAME=X-CSRF-TOKEN
ENV VITE_API_URL=${VITE_API_URL}
ENV VITE_SIGNALR_URL=${VITE_SIGNALR_URL}
ENV VITE_CSRF_COOKIE_NAME=${VITE_CSRF_COOKIE_NAME}
ENV VITE_CSRF_HEADER_NAME=${VITE_CSRF_HEADER_NAME}

COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

FROM node:22-alpine AS final
WORKDIR /app

COPY --from=build /app/package.json /app/package-lock.json ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/dist ./dist

USER node
EXPOSE 5173

HEALTHCHECK --interval=10s --timeout=5s --retries=5 \
    CMD wget --quiet --spider http://127.0.0.1:5173 || exit 1

CMD ["npm", "run", "preview", "--", "--host", "0.0.0.0", "--port", "5173"]
