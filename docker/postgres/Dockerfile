FROM postgres:15

# Copy initialization scripts
COPY *.sql /docker-entrypoint-initdb.d/

# Expose default port
EXPOSE 5432
