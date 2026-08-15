#!/usr/bin/env bash
# ============================================================
# DEMONSTRACAO DO CACHE MULTINIVEL
# ============================================================
set -uo pipefail
API="${API:-http://localhost:8080}"
PROFISSIONAL_ID="${PROFISSIONAL_ID:-1}"
DATA="${DATA:-$(date -d "next monday" "+%Y-%m-%d" 2>/dev/null || date -v+mon "+%Y-%m-%d")}"

azul()  { printf "\033[1;34m%s\033[0m\n" "$1"; }
verde() { printf "\033[1;32m%s\033[0m\n" "$1"; }

azul "=== 1. Primeira chamada: MISS (calcula no banco) ==="
time curl -s "$API/api/profissionais/$PROFISSIONAL_ID/disponibilidade?data=$DATA" > /dev/null; echo

azul "=== 2. Segunda chamada: HIT em L1 (IMemoryCache) ==="
time curl -s "$API/api/profissionais/$PROFISSIONAL_ID/disponibilidade?data=$DATA" > /dev/null; echo

verde "=== 3. O valor esta no Redis (L2), em JSON puro e legivel ==="
docker exec medflow-redis redis-cli GET "medflow:disponibilidade:$PROFISSIONAL_ID:$DATA" 2>/dev/null \
  || echo "(execute com o docker compose de pe)"
echo

verde "=== 4. Todas as chaves do MedFlow no Redis ==="
docker exec medflow-redis redis-cli KEYS "medflow:*" 2>/dev/null; echo

azul "=== 5. Metricas de cache ==="
curl -s "$API/metrics" | grep -E "medflow_cache_(hit|miss)" | head -10; echo

azul "=== 6. Agendando uma consulta -> o cache deve ser INVALIDADO ==="
curl -s -X POST "$API/api/consultas" -H 'Content-Type: application/json' \
  -d "{\"pacienteId\":1,\"profissionalId\":$PROFISSIONAL_ID,\"dataHora\":\"${DATA}T14:00:00\",\"procedimento\":\"CONSULTA_CARDIOLOGIA\"}" \
  -o /dev/null -w "  HTTP %{http_code}\n"

verde "=== 7. A chave do Redis sumiu (evict) e o 14:00 saiu da lista ==="
docker exec medflow-redis redis-cli GET "medflow:disponibilidade:$PROFISSIONAL_ID:$DATA" 2>/dev/null
curl -s "$API/api/profissionais/$PROFISSIONAL_ID/disponibilidade?data=$DATA" | head -c 400
echo
