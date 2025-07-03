# 🐝 Hive Emulator: CI/CD, Containers and Kubernetes

A small drone "hive" emulator (two .NET 8 web APIs, Redis and a React + OpenLayers map) packaged, tested and deployed end to end: Docker images, docker-compose, Kubernetes manifests, a GitHub Actions pipeline that ships to DigitalOcean, an Azure deployment of the frontend, and Datadog / Prometheus monitoring. The application itself started from a demo project from the university DevOps course; my work here is everything that builds, tests, ships and monitors it.

DevOps course, Igor Sikorsky Kyiv Polytechnic Institute (KPI), spring semester 2025.

## 🧩 How the system works

```
 ┌──────────────┐   connect + telemetry every 5 s   ┌─────────────────────────┐
 │  Hive Mind   │ ────────────────────────────────▶ │  Communication Control  │
 │  API (.NET)  │ ◀──────────────────────────────── │       API (.NET)        │
 └──────────────┘    POST api/v1/command (move)     └──────┬──────────▲───────┘
                                                           │          │ area / hives / move
                                  Hive:<id> JSON + events  ▼          │
                                                    ┌─────────────┐ ┌─┴────────────┐
                                                    │    Redis    │ │  Map Client  │
                                                    └─────────────┘ └──────────────┘
```

- **Communication Control** registers hives, hands each one its operational area (centre, radius, height, speed, telemetry and ping intervals), keeps the latest telemetry in Redis under `Hive:<id>` and publishes every connect, reconnect, disconnect, telemetry and move event to the `HiveChannel` pub/sub channel. Client endpoints list hives and send move commands to the selected ones.
- **Hive Mind** is an emulated drone: on start-up it connects with a Polly retry policy, sends telemetry every 5 s and, on a move command, flies towards the target, covering 10 % of the remaining distance every 3 s.
- **Map Client** (React, Vite, OpenLayers) shows the operational area on an OSM map, refreshes hive markers every 5 s and sends all hives to a clicked point.
- **Message Listener** is a console app that prints everything published to `HiveChannel`.

## 🚀 DevOps work

**Containers.** Multi-stage Dockerfiles for Communication Control and Hive Mind (`dotnet/sdk:8.0` → `dotnet/aspnet:8.0`) and for the map client (`node` build with the API URL as a build arg → `nginx:stable-alpine`). `.dockerignore` files keep `bin/`, `obj/` and `node_modules` out of the build context.

**Compose.** `docker-compose.yml` builds and starts Redis, Communication Control (`:8080`) and the map client (`:3000`); `docker-compose-digitalocean.yml` runs the same stack from the registry images.

**Kubernetes.** Manifests for Redis, Communication Control (pulled from the private registry via the `do-registry` secret, Redis address injected through `REDIS__CONNECTIONSTRING`) and the map client behind a LoadBalancer service.

**CI/CD** (`src/.github/workflows/ci-cd.yml`), triggered by pushes to `main` and `dev`:

| Job | Steps |
|---|---|
| `backend-test-build-push` | `dotnet restore` → `dotnet test` → build and push `communication-control:<sha>` to DigitalOcean Container Registry |
| `frontend-test-build-push` | `npm install` → `npm test` → build and push `mapclient:<sha>` with the branch-specific API URL |
| `codeql-analysis` | CodeQL for C# and JavaScript with the `security-and-quality` suite |
| `deploy` | `doctl` kubeconfig for the prod (`main`) or dev (`dev`) cluster → `kubectl set image` → `kubectl rollout status` |

Registry credentials and the DigitalOcean token come from GitHub Secrets; images are tagged with the commit SHA and carry OCI labels.

**Azure.** A separate workflow deploys the map client to Azure Static Web Apps, while Communication Control runs on Azure App Service; `public/config.json` and the Hive Mind settings point to it.

**Monitoring.** A `DatadogAgent` resource (Datadog Operator, EU site) collects logs from all containers and enables network performance monitoring; Communication Control exposes Prometheus metrics at `/metrics` via `prometheus-net`.

## ✅ Tests

- `DevOpsProject.Tests` (MSTest + Moq, 25 tests): `CommunicationControlService` with mocked Redis, publisher and a stubbed hive HTTP endpoint (connect vs. reconnect, failed writes, telemetry updates, move commands and the exact request sent to the hive), the Redis key-value and publish services, `SpatialService` and Hive Mind movement.
- `MapClient/tests` (mocha + chai, 6 tests): `mapService` with a stubbed axios, covering request URLs, mapping hive telemetry to markers, error handling and the move request body.

Both suites run in the pipeline before any image is pushed.

## 🖥️ Running locally

Requirements: Docker with Compose; for running without containers, .NET 8 SDK and Node.js 18+.

```bash
git clone https://github.com/pgerasymchuk/hive-emulator-devops.git
cd hive-emulator-devops/src
docker compose up --build
# Swagger:  http://localhost:8080/swagger
# Metrics:  http://localhost:8080/metrics
# Map:      http://localhost:3000
```

The map client reads its API address from `MapClient/public/config.json`; set `"API"` to `http://localhost:8080/api/v1/client` to use the local backend.

An emulated hive can join the running stack as a container:

```bash
cd CommunicationControl
docker build -f HiveMind.Dockerfile -t hive-mind .
docker run --network src_default -p 5149:5149 --name hive-mind \
  -e CommunicationConfiguration__RequestSchema=http \
  -e CommunicationConfiguration__CommunicationControlIP=hive-communication-control \
  -e CommunicationConfiguration__CommunicationControlPort=8080 \
  -e CommunicationConfiguration__HiveIP=hive-mind \
  -e CommunicationConfiguration__HivePort=5149 \
  hive-mind
```

Tests:

```bash
cd CommunicationControl && dotnet test
cd ../MapClient && npm install && npm test
```

Kubernetes: create the `do-registry` pull secret, then `kubectl apply -f k8s/`.

## 🗂 Project structure

```
src/
├── .github/workflows/     # CI/CD pipeline
├── CommunicationControl/  # .NET solution: both APIs, logic, shared lib, tests, Dockerfiles
├── MapClient/             # React + OpenLayers map, Dockerfile, Azure workflow, tests
├── k8s/                   # Kubernetes manifests
├── docker-compose*.yml    # local and registry-based stacks
└── datadog-agent.yaml     # Datadog agent for the cluster
```

## 🛠️ Tech stack

- **Backend:** C#, .NET 8, ASP.NET Core, StackExchange.Redis, Polly, Serilog, Swagger
- **Frontend:** React 18, Vite, OpenLayers, axios
- **Infrastructure:** Docker, Docker Compose, nginx, Kubernetes (DigitalOcean), Azure Static Web Apps and App Service
- **CI/CD:** GitHub Actions, DigitalOcean Container Registry, CodeQL
- **Monitoring:** Datadog Operator, Prometheus (`prometheus-net`)
- **Testing:** MSTest, Moq, mocha, chai
