#!/usr/bin/env bash
set -euo pipefail
# Apenas desenvolvimento: os valores são aleatórios, produzidos por scripts/init-local.mjs.
psql --username "$POSTGRES_USER" --dbname postgres -v ON_ERROR_STOP=1 \
  -v migrator_password="$MIGRATOR_PASSWORD" -v runtime_password="$RUNTIME_PASSWORD" -v identity_password="$IDENTITY_DB_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE api_migrator LOGIN PASSWORD %L', :'migrator_password') \gexec
SELECT format('CREATE ROLE api_runtime LOGIN PASSWORD %L', :'runtime_password') \gexec
SELECT format('CREATE ROLE identity_owner LOGIN PASSWORD %L', :'identity_password') \gexec
CREATE DATABASE app OWNER api_migrator;
CREATE DATABASE identity_provider OWNER identity_owner;
REVOKE CONNECT ON DATABASE app FROM PUBLIC;
REVOKE CONNECT ON DATABASE identity_provider FROM PUBLIC;
GRANT CONNECT ON DATABASE app TO api_migrator, api_runtime;
GRANT CONNECT ON DATABASE identity_provider TO identity_owner;
SQL
