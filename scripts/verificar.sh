#!/usr/bin/env bash
# ============================================================
# Verificacao completa. Rode isto ANTES de gravar o video.
#   ./scripts/verificar.sh
# ============================================================
set -e
azul()  { printf "\n\033[1;34m== %s\033[0m\n" "$1"; }
verde() { printf "\033[1;32m%s\033[0m\n" "$1"; }

azul "1/4  Restaurando pacotes"
dotnet restore

azul "2/4  Compilando (warnings sao tratados como erro)"
dotnet build --no-restore -c Release

azul "3/4  Testes unitarios (nao precisam de Docker)"
dotnet test tests/MedFlow.UnitTests --no-build -c Release

azul "4/4  Testes de integracao (PostgreSQL + Redis reais)"
if docker info >/dev/null 2>&1; then
  dotnet test tests/MedFlow.IntegrationTests --no-build -c Release
else
  printf "\033[1;33mDocker indisponivel: testes de integracao PULADOS.\033[0m\n"
fi

verde ""
verde "Tudo certo. Proximos passos:"
verde "  docker compose up -d --build      # sobe a stack completa"
verde "  ./scripts/demo-cache.sh           # demonstra o cache L1/L2"
verde "  ./scripts/demo-circuit-breaker.sh # demonstra o circuit breaker + fallback"
verde "  ./scripts/demo-sse.sh             # demonstra o push em tempo real"
