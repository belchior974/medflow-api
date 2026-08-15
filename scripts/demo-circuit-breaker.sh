#!/usr/bin/env bash
# ============================================================
# ROTEIRO DE ESTRESSE PARA O VIDEO (Etapa 3, item 4)
# Prova, ao vivo, o acionamento do circuit breaker e do fallback.
# ============================================================
set -uo pipefail
API="${API:-http://localhost:8080}"
PACIENTE_ID="${PACIENTE_ID:-1}"      # paciente COM convenio (vem do seed)
PROFISSIONAL_ID="${PROFISSIONAL_ID:-1}"

azul()    { printf "\033[1;34m%s\033[0m\n" "$1"; }
verde()   { printf "\033[1;32m%s\033[0m\n" "$1"; }
amarelo() { printf "\033[1;33m%s\033[0m\n" "$1"; }

estado_circuito() { curl -s "$API/health/circuit" | grep -o '"estado":"[A-Za-z]*"' | cut -d'"' -f4; }

proxima_segunda() {
  if date -v+1d >/dev/null 2>&1; then date -v+mon "+%Y-%m-%d"; else date -d "next monday" "+%Y-%m-%d"; fi
}
DIA="$(proxima_segunda)"

azul "=== 1. Estado inicial do circuito ==="
echo "Circuito: $(estado_circuito)"; echo

azul "=== 2. Chamada normal (WireMock respondendo) ==="
curl -s -X POST "$API/api/consultas" -H 'Content-Type: application/json' \
  -d "{\"pacienteId\":$PACIENTE_ID,\"profissionalId\":$PROFISSIONAL_ID,\"dataHora\":\"${DIA}T08:00:00\",\"procedimento\":\"CONSULTA_CARDIOLOGIA\"}"
echo -e "\n"

amarelo "=== 3. DERRUBANDO o servico de convenio (docker compose stop wiremock) ==="
docker compose stop wiremock >/dev/null 2>&1 || echo "(rode manualmente se o compose nao estiver no PATH)"
sleep 2; echo

azul "=== 4. Bateria de estresse: 15 agendamentos com o convenio fora do ar ==="
for i in $(seq 1 15); do
  HORA=$(printf "%02d" $((8 + i / 2)))
  MIN=$([ $((i % 2)) -eq 0 ] && echo "00" || echo "30")
  RESP=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API/api/consultas" \
    -H 'Content-Type: application/json' \
    -d "{\"pacienteId\":$PACIENTE_ID,\"profissionalId\":$PROFISSIONAL_ID,\"dataHora\":\"${DIA}T${HORA}:${MIN}:00\",\"procedimento\":\"CONSULTA_CARDIOLOGIA\"}")
  printf "  chamada %2d -> HTTP %s | circuito: %s\n" "$i" "$RESP" "$(estado_circuito)"
done
echo

verde "=== 5. Resultado ==="
echo "Estado final do circuito: $(estado_circuito)"
echo
echo "Repare que TODAS as requisicoes voltaram 201 (Created), nao 500."
echo "As consultas foram criadas com status AguardandoValidacaoConvenio:"
curl -s "$API/api/pacientes/$PACIENTE_ID/consultas" | head -c 600
echo -e "\n"

verde "=== 6. Metricas que comprovam o fallback ==="
curl -s "$API/metrics" | grep -E "medflow_convenio_fallback|medflow_consultas_criadas" | head -10
echo

amarelo "=== 7. Religando o servico de convenio ==="
docker compose start wiremock >/dev/null 2>&1 || true
echo "Aguarde ~10s (DuracaoCircuitoAbertoSegundos) e o circuito passa a HalfOpen e depois Closed."
