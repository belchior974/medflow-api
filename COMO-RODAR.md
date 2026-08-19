# Como rodar o MedFlow

Guia prático de execução. Para arquitetura, decisões de projeto e mapeamento
Spring Boot → .NET, veja o [README.md](README.md).

---

## Pré-requisitos

| Ferramenta | Versão | Necessária para |
|---|---|---|
| .NET SDK | 9.0 ou superior | ambas as opções |
| Docker + Docker Compose | qualquer versão recente | Opção A e testes de integração |

Confira com `dotnet --list-sdks` e `docker info`.

---

## Opção A — stack completa (recomendada para a demonstração)

```bash
docker compose up -d --build
```

Sobe API, PostgreSQL, Redis, Prometheus, Grafana, Zipkin e o WireMock que simula o
serviço de convênio. A primeira execução leva ~1 min por causa do build da imagem.

| Serviço | URL | Credenciais |
|---|---|---|
| **Scalar (documentação da API)** | http://localhost:8080/scalar/v1 | — |
| API | http://localhost:8080 | — |
| Health | http://localhost:8080/health | — |
| Estado do circuito | http://localhost:8080/health/circuit | — |
| Métricas (Prometheus) | http://localhost:8080/metrics | — |
| Grafana | http://localhost:3000 | `admin` / `admin` |
| Prometheus | http://localhost:9090 | — |
| Zipkin | http://localhost:9411 | — |
| WireMock (convênio) | http://localhost:8089 | — |

Verifique que subiu:

```bash
curl http://localhost:8080/health          # Healthy
curl http://localhost:8080/api/pacientes   # devolve os dados do seed
```

Para derrubar: `docker compose down` (ou `down -v` para apagar também os volumes).

**O Scalar já funciona aqui sem configurar nada**, porque o compose define
`ASPNETCORE_ENVIRONMENT=Docker` e o `appsettings.Docker.json` traz `ExporOpenApi: true`.

---

## Opção B — sem Docker, para desenvolver

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:8080 \
  dotnet run --project src/MedFlow.Api
```

Roda com SQLite em arquivo e sem Redis — o cache degrada para L1 e a aplicação
funciona normalmente (é a degradação graciosa em ação).

As duas variáveis de ambiente **importam**:

- `ASPNETCORE_ENVIRONMENT=Development` — sem ela a aplicação sobe em `Production` e o
  `/scalar/v1` **não é exposto** (retorna 404). Veja `Program.cs`, onde o Scalar só é
  mapeado se o ambiente for `Development` ou se `ExporOpenApi` for `true`.
- `ASPNETCORE_URLS` — o projeto não tem `launchSettings.json`, então sem isso o
  ASP.NET Core tenta a porta 5000, **que no macOS costuma estar ocupada** pelo
  Control Center (AirPlay Receiver).

Confirme no log que o ambiente é o esperado:

```
info: Microsoft.Hosting.Lifetime[0] Hosting environment: Development
```

---

## Testando a API

**A raiz `/` não tem endpoint mapeado — `http://localhost:8080` retorna 404, e isso é
o comportamento correto.** Todas as rotas de negócio vivem sob `/api`.

```bash
# Health check
curl http://localhost:8080/health

# Cadastros (dados do seed)
curl http://localhost:8080/api/pacientes
curl http://localhost:8080/api/profissionais

# Disponibilidade de agenda
curl "http://localhost:8080/api/profissionais/1/disponibilidade?data=2026-08-24"

# Agendar consulta
curl -X POST http://localhost:8080/api/consultas \
  -H 'Content-Type: application/json' \
  -d '{"pacienteId":1,"profissionalId":1,"dataHora":"2026-08-24T14:00:00","procedimento":"CONSULTA_GERAL"}'

# Registrar atendimento no prontuário (caminho EF Core)
curl -X POST http://localhost:8080/api/registros-clinicos \
  -H 'Content-Type: application/json' \
  -d '{"consultaId":1,"diagnostico":"Faringite aguda","prescricao":"Amoxicilina 500mg"}'
```

Para navegar por todos os endpoints com interface, use o **Scalar** em
http://localhost:8080/scalar/v1.

---

## Testes

```bash
./scripts/verificar.sh          # restore + build + unitários + integração
```

Ou separadamente:

```bash
dotnet test tests/MedFlow.UnitTests -c Release          # 15 testes, não precisam de Docker
dotnet test tests/MedFlow.IntegrationTests -c Release   # 4 testes, sobem PostgreSQL e Redis reais
```

Os testes de integração usam Testcontainers: exigem Docker e criam os próprios
contêineres em portas aleatórias, sem conflitar com a stack do compose.

---

## Scripts de demonstração

Assumem a API em `http://localhost:8080` (ou seja, a Opção A nas portas padrão).
A porta pode ser sobrescrita com a variável `API`.

```bash
./scripts/demo-cache.sh            # cache L1/L2
./scripts/demo-circuit-breaker.sh  # circuit breaker + fallback (derruba o WireMock)
./scripts/demo-sse.sh              # push em tempo real via SSE
```

---

## Problemas comuns

### `docker compose up` falha com "container name is already in use"

```
Conflict. The container name "/medflow-prometheus" is already in use
```

Existe outra stack com os mesmos nomes — tipicamente a versão Spring Boot desta mesma
API. **Contêiner parado também reserva o nome**, então não basta que ela esteja
desligada. Derrube-a de verdade:

```bash
docker compose -p medflow-api down     # a partir do diretório da versão Spring Boot
```

Se quiser rodar as duas em paralelo, este compose aceita prefixo e portas próprios:

```bash
MEDFLOW_PREFIX=medflow-net MEDFLOW_PORTA_API=8090 MEDFLOW_PORTA_PG=5433 \
  MEDFLOW_PORTA_REDIS=6380 MEDFLOW_PORTA_GRAFANA=3001 \
  MEDFLOW_PORTA_PROMETHEUS=9091 MEDFLOW_PORTA_ZIPKIN=9412 \
  MEDFLOW_PORTA_WIREMOCK=8091 docker compose -p medflow-net up -d --build
```

### `docker compose up` falha por porta ocupada

Confira o que está usando a porta e encerre:

```bash
lsof -nP -iTCP:8080 -sTCP:LISTEN
```

Causa frequente: um `dotnet run` da Opção B esquecido rodando na mesma porta.

### O Scalar abre mas não lista nenhum endpoint

Sinal de que o documento OpenAPI não está sendo gerado. Verifique direto:

```bash
curl -i http://localhost:8080/openapi/v1.json     # tem que ser 200
```

Se der 400, o gerador está falhando e o Scalar renderiza vazio — a página em si
responde 200 mesmo assim, então **não confie no status da página** para concluir que
está tudo bem.

### O `/scalar/v1` retorna 404

A aplicação está rodando em `Production`. Veja a Opção B acima: defina
`ASPNETCORE_ENVIRONMENT=Development`.

### `http://localhost:8080` retorna 404

Esperado. Não há rota na raiz — use `/health`, `/scalar/v1` ou `/api/...`.

---

## Configuração

Tudo em `appsettings.json`, seção `MedFlow`. O `appsettings.Docker.json` sobrescreve
para PostgreSQL + Redis + Zipkin + WireMock. Qualquer chave pode ser sobrescrita por
variável de ambiente usando `__` como separador:

```bash
MedFlow__Banco__Provider=postgres \
MedFlow__Banco__ConnectionString="Host=localhost;Port=5432;Database=medflow;Username=medflow;Password=medflow" \
ConnectionStrings__Redis=localhost:6379 \
  dotnet run --project src/MedFlow.Api
```

| Chave | Padrão | Descrição |
|---|---|---|
| `MedFlow:Banco:Provider` | `sqlite` | `sqlite` ou `postgres` |
| `MedFlow:Banco:ConnectionString` | `Data Source=medflow.db` | |
| `ConnectionStrings:Redis` | `localhost:6379` | vazio = só cache L1 |
| `MedFlow:Convenio:BaseUrl` | `http://localhost:8089` | serviço externo simulado |
| `MedFlow:Convenio:MaxTentativas` | `3` | total de tentativas; `1` = sem retentativa |
| `OpenTelemetry:ZipkinEndpoint` | `http://localhost:9411/...` | vazio = sem tracing |
