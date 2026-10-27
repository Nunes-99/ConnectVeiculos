FROM nginx:alpine

# Remove default config
RUN rm -f /etc/nginx/conf.d/default.conf

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY dominio-proprio.conf /etc/nginx/snippets/dominio-proprio.conf
RUN mkdir -p /etc/nginx/dominios

EXPOSE 80 443
