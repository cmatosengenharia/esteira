#!/usr/bin/env bash
# Passo 0 da Fase 0: confere o ambiente no Mac e prepara whisper.cpp + modelo.
# Nunca usa sudo. Se faltar algo, imprime o comando brew e sai com erro.
set -euo pipefail
cd "$(dirname "$0")/.."

fail() { echo "ERRO: $*" >&2; exit 1; }
need() { command -v "$1" >/dev/null 2>&1 || fail "'$1' não encontrado. Instale com: brew install $2"; }

echo "== Arquitetura"; uname -m

echo "== Versões"
need dotnet dotnet-sdk
DOTNET_VER=$(dotnet --version)
echo "dotnet $DOTNET_VER"
[[ "$DOTNET_VER" == 9.* ]] || fail "dotnet precisa ser 9.x (atual: $DOTNET_VER). brew install --cask dotnet-sdk@9"

need ffmpeg ffmpeg; ffmpeg -version | head -1
need ffprobe ffmpeg; ffprobe -version | head -1

need node node@22
NODE_VER=$(node --version); echo "node $NODE_VER"
NODE_MAJOR=${NODE_VER#v}; NODE_MAJOR=${NODE_MAJOR%%.*}
(( NODE_MAJOR >= 22 )) || fail "node precisa ser 22+ (atual: $NODE_VER). brew install node@22"

need docker docker
docker --version
docker compose version || fail "docker compose (plugin v2) ausente. brew install docker-compose"

echo "== Docker daemon"
ensure_docker() {
  if docker info >/dev/null 2>&1; then echo "docker ativo"; return 0; fi
  if [[ -d /Applications/Docker.app ]]; then
    echo "abrindo Docker.app..."; open -a Docker
  elif command -v colima >/dev/null 2>&1; then
    echo "iniciando colima..."; colima start
  else
    fail "Docker não está rodando e não achei Docker.app nem colima. brew install --cask docker  (ou brew install colima)"
  fi
  for _ in $(seq 1 60); do
    docker info >/dev/null 2>&1 && { echo "docker ativo"; return 0; }
    sleep 2
  done
  fail "docker não respondeu em 120 s"
}
ensure_docker

echo "== whisper.cpp"
if ! command -v whisper-cli >/dev/null 2>&1 && ! command -v whisper-cpp >/dev/null 2>&1; then
  command -v brew >/dev/null 2>&1 || fail "brew ausente; instale o Homebrew primeiro"
  brew install whisper-cpp
fi
WHISPER_BIN=$(command -v whisper-cli || command -v whisper-cpp)
echo "whisper: $WHISPER_BIN"

MODEL=models/ggml-small.bin
if [[ ! -f "$MODEL" ]]; then
  mkdir -p models
  echo "baixando modelo small (~466 MB)..."
  curl -L --fail -o "$MODEL" "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
fi
ls -lh "$MODEL"

echo
echo "OK: ambiente pronto."
