# Technical Architecture — QA Automation Platform

> **Deployment Target:** Google Cloud Platform (GCP) Compute Engine VM  
> **AI Engine:** Google Gemini LLM (Gemini 1.5 Pro)  
> **Speech Engine:** Google Cloud Speech-to-Text API  
> **Version:** 1.0

---

## 1. System Overview

The QA Automation Platform is an intelligent call-centre quality-assurance tool that automatically transcribes agent–customer calls, evaluates them against configurable QA forms using a large language model, and surfaces actionable coaching insights. The system is built as two cooperating ASP.NET Core applications deployed side-by-side on a single GCP Compute Engine VM instance.

```
┌─────────────────────────────────────────────────────────────────┐
│                 GCP Compute Engine VM (e2-standard-4)            │
│                                                                   │
│   ┌─────────────────────┐     ┌──────────────────────────────┐   │
│   │  QAAutomation.Web   │────▶│     QAAutomation.API         │   │
│   │  (ASP.NET Core MVC) │     │  (ASP.NET Core Web API)      │   │
│   │  Port 5000          │     │  Port 5018 (internal)         │   │
│   └─────────────────────┘     └──────────┬───────────────────┘   │
│            │                              │                        │
│         Nginx                             │                        │
│      (reverse proxy)                      │                        │
│                                           │                        │
│   ┌────────────────────────────────────── ▼ ─────────────────┐   │
│   │               SQL Server (MSSQL)                          │   │
│   │    Audit data · AI settings · Users · Knowledge base      │   │
│   └───────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
          │                          │
          ▼                          ▼
  Google Gemini API         Google Cloud Speech-to-Text API
  (gemini-1.5-pro)          (STT v1 / v2)
```

---

## 2. Component Inventory

### 2.1 Frontend — QAAutomation.Web

| Attribute | Detail |
|---|---|
| Framework | ASP.NET Core 8 MVC (Razor Views) |
| Port | 5000 (behind Nginx) |
| Auth | Cookie-based with role claims |
| Key Views | Auto Audit, Call Pipeline, Analytics, AI Settings, Training Plans, Human Review |
| Static Assets | Bootstrap 5, Bootstrap Icons, Chart.js |
| Real-time | SignalR (pipeline progress hub) |

### 2.2 Backend API — QAAutomation.API

| Attribute | Detail |
|---|---|
| Framework | ASP.NET Core 8 Web API |
| Port | 5018 (internal only, not exposed through Nginx) |
| ORM | Entity Framework Core 8 (SQL Server provider) |
| Background | `IHostedService` for long-running pipeline jobs |
| Real-time | SignalR hub (`/hubs/pipeline`) |

### 2.3 AI / LLM — Google Gemini

| Attribute | Detail |
|---|---|
| Model | `gemini-1.5-pro` (configurable via AI Settings) |
| Transport | HTTPS REST — `generativelanguage.googleapis.com` |
| Auth | Google API Key (stored encrypted in `AiConfigs` table) |
| Implemented In | `GeminiHttpHelper`, `GoogleGeminiAutoAuditService`, `GoogleGeminiSentimentService` |
| Capabilities Used | QA parameter scoring, sentiment / emotion analysis, PII redaction, coaching recommendations, insights chat |
| Context Strategy | RAG (Retrieval-Augmented Generation) for knowledge-based parameters; full transcript for LLM-type parameters |
| Temperature | 0.1 (configurable) |

### 2.4 Speech-to-Text — Google Cloud Speech API

| Attribute | Detail |
|---|---|
| Service | Google Cloud Speech-to-Text v1 |
| Auth | Google API Key (shared with Gemini key in `AiConfigs`) |
| Implemented In | `GoogleSpeechService` |
| Input Formats | WAV, MP3, OGG/OPUS, FLAC (converted internally via `AudioFormatHelper`) |
| File Size | Up to 10 MB inline; GCS URIs supported for larger files |
| Encoding | Automatic via `AudioFormatHelper` → LINEAR16 / OGG_OPUS |
| Language | `en-US` (default) |

### 2.5 Database — SQL Server

| Attribute | Detail |
|---|---|
| Engine | Microsoft SQL Server 2019+ (or Azure SQL compatible) |
| Schema | `dbo` |
| Key Tables | `AiConfigs`, `Audits`, `EvaluationForms`, `Parameters`, `CallRecords`, `KnowledgeSources`, `AppUsers`, `TrainingPlans`, `AuditLogs` |
| Migrations | Script-based (`QAAutomation_MSSQL.sql`, `SqlServer_Migration.sql`) |
| Connection | Windows Auth (dev) / SQL Auth (prod) via `ConnectionStrings:DefaultConnection` |

### 2.6 Reverse Proxy — Nginx

| Attribute | Detail |
|---|---|
| Role | Terminates TLS, routes `/auraai/*` to Web on port 5000 |
| PathBase | `/auraai` (set via `appsettings.json: "PathBase": "/auraai"`) |
| TLS | Let's Encrypt (Certbot) recommended |
| Static Files | Served by ASP.NET Core (Nginx passes through) |

---

## 3. Data Flow

### 3.1 Auto Audit (Transcript Upload)

```
User (browser)
    │  POST /AutoAudit/Upload (transcript text or .txt file)
    ▼
QAAutomation.Web
    │  HTTP POST /api/autoaudit/analyze
    ▼
QAAutomation.API
    ├─ Retrieve EvaluationForm + Parameters from SQL Server
    ├─ [RAG] Fetch top-K knowledge chunks from KnowledgeSources
    ├─ Build Gemini prompt (system prompt + form + transcript + RAG context)
    │  HTTPS POST → generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent
    ├─ Parse structured JSON response → parameter scores + reasoning
    ├─ [Optional] Sentiment analysis via GoogleGeminiSentimentService
    └─ Return AutoAuditResult JSON
    ▼
QAAutomation.Web
    │  Renders Review page with AI-suggested scores
    ▼
User reviews / adjusts → POST /AutoAudit/Save → stored in Audits table
```

### 3.2 Call Pipeline (Audio File)

```
User uploads audio file  OR  URL / Azure Blob / SharePoint connector
    ▼
QAAutomation.API — CallPipelineService
    ├─ Download & validate audio (AudioFormatHelper)
    ├─ Convert to LINEAR16 WAV if needed
    │  HTTPS POST → speech.googleapis.com/v1/speech:recognize
    ├─ Receive transcript text
    ├─ PII Redaction (GoogleGeminiAutoAuditService / PiiRedactionService)
    ├─ Negative Intent Detection (NegativeIntentDetector)
    ├─ QA Scoring (same flow as Auto Audit above)
    ├─ Sentiment Analysis (GoogleGeminiSentimentService)
    ├─ TNI Generation (TniGenerationService)
    └─ Push progress updates via SignalR → browser
    ▼
Results stored in CallRecords + Audits tables
```

### 3.3 Insights Chat

```
User types question in InsightsChat view
    │  POST /api/insightschat/message
    ▼
QAAutomation.API — InsightsChatService
    ├─ Load conversation history
    ├─ Inject project audit summary as system context
    │  HTTPS POST → gemini-1.5-pro
    └─ Stream or return answer
    ▼
Response displayed in chat UI
```

---

## 4. GCP Deployment Architecture

### 4.1 Recommended VM Specification

| Parameter | Recommended Value |
|---|---|
| Machine Type | `e2-standard-4` (4 vCPU, 16 GB RAM) |
| OS | Ubuntu 22.04 LTS |
| Boot Disk | 50 GB SSD (balanced persistent disk) |
| Network | VPC with internal IP; external IP via Cloud NAT or static IP |
| Firewall | Allow inbound TCP 443 (HTTPS) and 80 (HTTP redirect) only |

### 4.2 Required GCP APIs

Enable the following APIs in the GCP project:

```
gcloud services enable \
    speech.googleapis.com \
    generativelanguage.googleapis.com \
    compute.googleapis.com
```

### 4.3 VM Software Stack

```
Ubuntu 22.04 LTS
├── .NET 8 Runtime (apt-get / dotnet-runtime-8.0)
├── SQL Server 2022 for Linux  (mssql-server package)
├── Nginx (reverse proxy)
├── Certbot (Let's Encrypt TLS)
├── systemd units
│   ├── qaautomation-api.service   → dotnet QAAutomation.API.dll
│   └── qaautomation-web.service   → dotnet QAAutomation.Web.dll
└── Google Cloud Ops Agent (optional, for monitoring)
```

### 4.4 Nginx Configuration Sketch

```nginx
server {
    listen 443 ssl;
    server_name <your-domain>;

    ssl_certificate     /etc/letsencrypt/live/<your-domain>/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/<your-domain>/privkey.pem;

    location /auraai/ {
        proxy_pass         http://127.0.0.1:5000/auraai/;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name <your-domain>;
    return 301 https://$host$request_uri;
}
```

### 4.5 Environment Variables / Secrets

Sensitive values should be supplied via environment variables injected into the systemd unit files, **never** committed to source control:

| Variable | Service | Purpose |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | API | SQL Server connection string |
| `Google__ApiKey` | API | Google API Key for Gemini + Cloud STT |
| `Google__GeminiModel` | API | Model name, e.g. `gemini-1.5-pro` |

Example systemd override (`/etc/systemd/system/qaautomation-api.service.d/override.conf`):

```ini
[Service]
Environment="ConnectionStrings__DefaultConnection=Server=localhost;Database=QAAutomation;User Id=qa;Password=***;"
Environment="Google__ApiKey=AIza..."
Environment="Google__GeminiModel=gemini-1.5-pro"
```

---

## 5. Security Considerations

| Area | Approach |
|---|---|
| TLS | HTTPS-only via Nginx + Let's Encrypt |
| API access | API port 5018 bound to `127.0.0.1` only — not reachable from outside VM |
| Secrets | Stored in `AiConfigs` DB table (encrypted at rest via SQL Server TDE recommended) or environment variables — never in source code |
| Authentication | Cookie-based sessions with role-based access control (Admin / QA Analyst / Agent) |
| PII | Call transcripts are PII-redacted before storage via `PiiRedactionService` |
| Prompt safety | Gemini safety settings applied (HARM_BLOCK_THRESHOLD_UNSPECIFIED) |
| Firewall | GCP VPC firewall: only ports 80/443 exposed externally |

---

## 6. Key External Dependencies

| Dependency | Version / Endpoint | Purpose |
|---|---|---|
| Google Gemini API | `generativelanguage.googleapis.com/v1beta` | LLM quality scoring, sentiment, PII, chat |
| Google Cloud STT | `speech.googleapis.com/v1` | Audio → transcript |
| SQL Server | 2019+ | Persistent data store |
| .NET | 8.0 | Runtime for both services |
| SignalR | Included in ASP.NET Core 8 | Real-time pipeline progress |
| Bootstrap | 5.x (CDN) | UI framework |
| Chart.js | 4.x (CDN) | Analytics charts |

---

## 7. Scalability Notes

- **Single VM**: The current architecture targets a single GCP VM. This is suitable for up to ~50 concurrent users and ~500 calls/day.
- **Scale-out path**: Migrate to GKE (Google Kubernetes Engine) or Cloud Run for stateless API containers; move SQL Server to Cloud SQL; use Memorystore (Redis) for SignalR backplane.
- **Gemini rate limits**: Gemini 1.5 Pro has per-minute token limits. The `GeminiHttpHelper` includes retry logic with exponential back-off for `429 RESOURCE_EXHAUSTED` responses.
- **Speech quotas**: Google Cloud STT free tier allows 60 minutes/month; production usage requires billing enabled.

---

## 8. Monitoring & Observability

| Tool | Purpose |
|---|---|
| GCP Cloud Logging | VM system logs, Nginx access/error logs |
| Google Cloud Ops Agent | App metrics (CPU, memory, disk) pushed to Cloud Monitoring |
| ASP.NET Core built-in logging | Application logs streamed to `journald` → Cloud Logging via Ops Agent |
| SignalR progress hub | Real-time pipeline progress visible in browser UI |
| `/api/ping` endpoint | Health-check endpoint polled by the Web app's connectivity diagnostics |

---

*Document maintained in `docs/Technical-Architecture.md`. Last updated: March 2026.*
