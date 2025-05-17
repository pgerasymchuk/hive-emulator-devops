FROM node AS build 
WORKDIR /app 

ARG VITE_API_BASE_URL
RUN echo "VITE_API_BASE_URL=$VITE_API_BASE_URL" > .env

COPY . . 

RUN npm install 
RUN npm run build

FROM nginx:stable-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]