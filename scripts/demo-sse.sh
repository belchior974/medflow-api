#!/usr/bin/env bash
# ============================================================
# DEMONSTRACAO DO PUSH EM TEMPO REAL (Server-Sent Events)
#
# Terminal 1:  ./scripts/demo-sse.sh
# Terminal 2:  curl -X PATCH http://localhost:8080/api/consultas/1/cancelar
# ============================================================
API="${API:-http://localhost:8080}"
PROFISSIONAL_ID="${PROFISSIONAL_ID:-1}"

printf "\033[1;34mOuvindo a agenda do profissional %s em tempo real...\033[0m\n" "$PROFISSIONAL_ID"
printf "\033[1;33m(cancele uma consulta em outro terminal para ver o evento LIBERADO)\033[0m\n\n"

curl -N -H "Accept: text/event-stream" \
  "$API/api/profissionais/$PROFISSIONAL_ID/disponibilidade/stream"
