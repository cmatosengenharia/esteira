#!/usr/bin/env bash
# Prova de ponta a ponta: sobe infra, API e Workers, gera (se preciso) um vídeo de 20 s, envia para POST /jobs
# e acompanha até Stage=Rendered ou Failed. Funciona no Mac (say) e em Linux (espeak-ng).
#
# Uso: scripts/demo.sh [--no-docker] [--no-build] [arquivo.mp4]
#   --no-docker  não toca no Docker (Postgres/RabbitMQ já estão de pé, ex.: services da CI)
#   --no-build   não roda dotnet build (já compilado)
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT=$(pwd)
RUN_DIR="$ROOT/.run"; mkdir -p "$RUN_DIR" samples data
API_URL=${API_URL:-http://localhost:5080}
TIMEOUT_SECONDS=${TIMEOUT_SECONDS:-600}
USE_DOCKER=1; BUILD=1; INPUT=""
for arg in "$@"; do
  case "$arg" in
    --no-docker) USE_DOCKER=0 ;;
    --no-build) BUILD=0 ;;
    *) INPUT="$arg" ;;
  esac
done

log() { printf '\n\033[1;34m== %s\033[0m\n' "$*"; }
fail() { echo "ERRO: $*" >&2; exit 1; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

# ---------- 1. Docker ----------
if [[ $USE_DOCKER == 1 ]]; then
  log "Docker"
  if ! docker info >/dev/null 2>&1; then
    if [[ -d /Applications/Docker.app ]]; then open -a Docker
    elif command -v colima >/dev/null 2>&1; then colima start
    else fail "Docker não está rodando (Docker.app ou colima)"; fi
    for _ in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 2; done
    docker info >/dev/null 2>&1 || fail "docker não respondeu"
  fi
  docker compose up -d postgres rabbitmq
  log "Aguardando Postgres e RabbitMQ"
  for _ in $(seq 1 60); do
    pg=$(docker inspect -f '{{.State.Health.Status}}' esteira-postgres 2>/dev/null || echo starting)
    rb=$(docker inspect -f '{{.State.Health.Status}}' esteira-rabbitmq 2>/dev/null || echo starting)
    [[ "$pg" == healthy && "$rb" == healthy ]] && break
    sleep 2
  done
  [[ "$pg" == healthy && "$rb" == healthy ]] || fail "infra não ficou saudável (postgres=$pg rabbitmq=$rb)"
fi

# ---------- 2. ffmpeg com libass ----------
# O `ffmpeg` do Homebrew core (9.x) vem sem libass e o render falha ("No option name near 'captions-9x16.ass'").
# Ordem: $ESTEIRA_FFMPEG_BIN, ffmpeg@7 (keg-only), ffmpeg do PATH; vale o primeiro que tiver o filtro `subtitles`.
FFMPEG_BIN=""
for cand in "${ESTEIRA_FFMPEG_BIN:-}" "$(brew --prefix 2>/dev/null)/opt/ffmpeg@7/bin/ffmpeg" "$(command -v ffmpeg || true)"; do
  [[ -n "$cand" && -x "$cand" && "$("$cand" -hide_banner -filters 2>/dev/null || true)" == *" subtitles "* ]] && { FFMPEG_BIN="$cand"; break; }
done
[[ -n "$FFMPEG_BIN" ]] || fail "nenhum ffmpeg com libass (filtro subtitles); rode scripts/check-env.sh"
export Media__FfmpegPath="$FFMPEG_BIN"
echo "ffmpeg: $FFMPEG_BIN"

# ---------- 2b. whisper ----------
WHISPER_BIN=${ESTEIRA_WHISPER_BIN:-$(command -v whisper-cli || command -v whisper-cpp || true)}
[[ -n "$WHISPER_BIN" ]] || fail "whisper.cpp não encontrado; rode scripts/check-env.sh"
[[ -f models/ggml-small.bin ]] || fail "models/ggml-small.bin não encontrado; rode scripts/check-env.sh"
export Media__WhisperPath="$WHISPER_BIN"
export Media__WhisperModelPath="$ROOT/models/ggml-small.bin"
export Media__StorageRoot="$ROOT/data/storage"
export Media__WorkRoot="$ROOT/data/work"

# ---------- 3. Build ----------
if [[ $BUILD == 1 ]]; then
  log "dotnet build"
  dotnet build Esteira.sln -c Release --nologo -v q
fi

# ---------- 4. API e Workers ----------
log "Iniciando API e Workers (logs em .run/)"
bash scripts/stop.sh --apps-only >/dev/null 2>&1 || true
export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
( cd "$ROOT" && exec dotnet run --project src/Esteira.Api -c Release --no-build --no-launch-profile --urls "$API_URL" ) > "$RUN_DIR/api.log" 2>&1 &
echo $! > "$RUN_DIR/api.pid"
for _ in $(seq 1 90); do
  curl -fsS "$API_URL/health" >/dev/null 2>&1 && break
  kill -0 "$(cat "$RUN_DIR/api.pid")" 2>/dev/null || { tail -40 "$RUN_DIR/api.log"; fail "API caiu"; }
  sleep 1
done
curl -fsS "$API_URL/health" >/dev/null || { tail -40 "$RUN_DIR/api.log"; fail "API não respondeu em $API_URL"; }
( cd "$ROOT" && exec dotnet run --project src/Esteira.Workers -c Release --no-build --no-launch-profile ) > "$RUN_DIR/workers.log" 2>&1 &
echo $! > "$RUN_DIR/workers.pid"
sleep 3
kill -0 "$(cat "$RUN_DIR/workers.pid")" 2>/dev/null || { tail -40 "$RUN_DIR/workers.log"; fail "Workers caíram"; }

# ---------- 5. Vídeo de teste ----------
if [[ -z "$INPUT" ]]; then
  INPUT=$(ls samples/*.mp4 2>/dev/null | head -1 || true)
fi
if [[ -z "$INPUT" ]]; then
  log "Gerando samples/demo.mp4 (20 s: fala com pausas + testsrc)"
  TMP=$(mktemp -d)
  SENTENCES=(
    "Olá, esta é a esteira de vídeo. Ela recebe um vídeo bruto e devolve um vídeo editado."
    "Primeiro ela transcreve o áudio com timestamps por palavra."
    "Depois corta os silêncios e gera legendas por frase."
    "No final, renderiza em nove por dezesseis com legenda queimada."
  )
  i=0; INPUTS=(); FILTER=""
  for s in "${SENTENCES[@]}"; do
    if command -v say >/dev/null 2>&1; then
      say -v Luciana -o "$TMP/s$i.aiff" "$s" 2>/dev/null || say -o "$TMP/s$i.aiff" "$s"
      ffmpeg -y -v error -i "$TMP/s$i.aiff" -ac 1 -ar 16000 "$TMP/s$i.wav"
    elif command -v espeak-ng >/dev/null 2>&1; then
      espeak-ng -v pt-br -s 150 -w "$TMP/s$i.wav" "$s"
    else
      fail "sem TTS (say ou espeak-ng) para gerar o vídeo de teste; passe um mp4 como argumento"
    fi
    INPUTS+=(-i "$TMP/s$i.wav")
    i=$((i+1))
  done
  # concatena falas com 1.5 s de silêncio entre elas e 1 s no fim
  n=${#SENTENCES[@]}; parts=""; k=0
  for ((j=0; j<n; j++)); do
    parts+="[$j:a]"
    if (( j < n-1 )); then parts+="[sil$k]"; k=$((k+1)); fi
  done
  silences=""
  for ((j=0; j<k; j++)); do silences+="anullsrc=r=16000:cl=mono,atrim=0:1.5[sil$j];"; done
  ffmpeg -y -v error "${INPUTS[@]}" -filter_complex "${silences}${parts}concat=n=$((n+k)):v=0:a=1,apad=pad_dur=1[a]" -map "[a]" -ar 16000 -ac 1 "$TMP/speech.wav"
  DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$TMP/speech.wav")
  ffmpeg -y -v error -f lavfi -i "testsrc=size=1280x720:rate=30" -i "$TMP/speech.wav" -t "$DUR" \
    -c:v libx264 -preset veryfast -pix_fmt yuv420p -c:a aac -b:a 128k -shortest samples/demo.mp4
  rm -rf "$TMP"
  INPUT=samples/demo.mp4
fi
echo "vídeo de entrada: $INPUT ($(ffprobe -v error -show_entries format=duration -of csv=p=0 "$INPUT") s)"

# ---------- 6. Seed + job ----------
log "Seed de tenant/usuário"
TOKEN=$(curl -fsS -X POST "$API_URL/dev/seed" -H 'Content-Type: application/json' -d '{}' | json "d['token']")
log "POST /jobs"
JOB=$(curl -fsS -X POST "$API_URL/jobs" -H "Authorization: Bearer $TOKEN" -F "file=@$INPUT" -F "targets=YouTubeShorts")
JOB_ID=$(echo "$JOB" | json "d['id']")
echo "job: $JOB_ID"

# ---------- 7. Polling ----------
log "Acompanhando (timeout ${TIMEOUT_SECONDS}s)"
START=$(date +%s); LAST=""
while :; do
  BODY=$(curl -fsS "$API_URL/jobs/$JOB_ID" -H "Authorization: Bearer $TOKEN")
  STAGE=$(echo "$BODY" | json "d['stage']")
  [[ "$STAGE" != "$LAST" ]] && { echo "$(date +%H:%M:%S) stage=$STAGE"; LAST=$STAGE; }
  [[ "$STAGE" == Rendered || "$STAGE" == Failed ]] && break
  (( $(date +%s) - START > TIMEOUT_SECONDS )) && fail "timeout esperando o job (último stage: $STAGE)"
  sleep 2
done
echo "$BODY" > "$RUN_DIR/last-job.json"

log "Resultado"
echo "$BODY" | python3 scripts/print-job.py
echo
echo "EDL: curl -s $API_URL/jobs/$JOB_ID/edl -H 'Authorization: Bearer \$TOKEN'"
echo "para parar tudo: scripts/stop.sh"
[[ "$STAGE" == Rendered ]] || { echo; tail -60 "$RUN_DIR/workers.log"; exit 1; }

# ---------- 8. Verificação do master ----------
OUT=$(echo "$BODY" | json "d['outputs'][0]['localPath']")
[[ -f "$OUT" ]] || fail "master não encontrado em $OUT"
DIMS=$(ffprobe -v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 "$OUT")
OUT_DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT")
IN_DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$INPUT")
[[ "$DIMS" == "1080,1920" ]] || fail "master deveria ser 1080x1920, veio $DIMS"
python3 -c "import sys; i,o=float(sys.argv[1]),float(sys.argv[2]); sys.exit(0 if 0 < o < i else 1)" "$IN_DUR" "$OUT_DUR" \
  || fail "master ($OUT_DUR s) deveria ser mais curto que a entrada ($IN_DUR s)"
ASS=$(ls data/work/*/captions-9x16.ass 2>/dev/null | head -1 || true)
[[ -n "$ASS" ]] && echo "legendas: $(grep -c '^Dialogue' "$ASS") frases em $ASS"
echo "OK: master 1080x1920, $OUT_DUR s (entrada $IN_DUR s)"
