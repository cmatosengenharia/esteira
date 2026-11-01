#!/usr/bin/env bash
# Para API, Workers e (salvo --apps-only) os containers do docker-compose.
cd "$(dirname "$0")/.."
RUN_DIR=.run
for app in api workers; do
  if [[ -f "$RUN_DIR/$app.pid" ]]; then
    pid=$(cat "$RUN_DIR/$app.pid")
    if kill -0 "$pid" 2>/dev/null; then
      pkill -TERM -P "$pid" 2>/dev/null || true
      kill -TERM "$pid" 2>/dev/null || true
      echo "parado: $app (pid $pid)"
    fi
    rm -f "$RUN_DIR/$app.pid"
  fi
done
# processos dotnet dos projetos que possam ter sobrado
pkill -f "Esteira.Api" 2>/dev/null || true
pkill -f "Esteira.Workers" 2>/dev/null || true
if [[ "${1:-}" != "--apps-only" ]]; then
  docker compose down >/dev/null 2>&1 && echo "containers parados" || true
fi
