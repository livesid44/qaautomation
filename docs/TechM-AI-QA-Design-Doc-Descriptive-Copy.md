# TechM – AI QA Design Document (QAAutomation Platform)

**Status:** Draft  
**Authors:** livesid44 (Repository Owner)  
**Supplier Company:** TechM / Internal  
**Team:** QA Automation Engineering  
**Repository:** livesid44/qaautomation (GitHub)  
**Last Major Revision:** April 2026  

---

## 1. Overview

### Objective

Build an intelligent, AI-driven call-centre Quality Assurance (QA) platform that automatically:
- Transcribes agent-customer calls
- Evaluates them against configurable evaluation forms using **Google Gemini 1.5 Pro**
- Surfaces actionable coaching insights

**Goal:** Reduce manual QA effort by at least **60%** and improve evaluation consistency across all teams.

**Key metrics targeted:**
- Average Handle Time (AHT) impact via faster QA turnaround
- QA Analyst throughput (evaluations per analyst per day)
- Evaluation consistency score (AI vs human inter-rater reliability)
- Agent CSAT / quality improvement over time

### Background

Traditional call-centre QA is labour-intensive: a QA analyst must listen to each recorded call, manually fill in a scoring form, and write coaching notes. This approach scales poorly, introduces human bias, and creates a bottleneck as call volumes grow.

**Key drivers:**
- High call volumes at TechM contact-centre operations requiring automated QA
- Google Gemini LLM capabilities now mature enough for reliable parameter scoring
- Existing manual QA tooling lacks RAG-based knowledge enrichment and AI coaching
- Alignment with the GVO innovation theme: *"Do more with less"*

### Scope

**IN SCOPE:**
- Automatic speech-to-text transcription of uploaded audio calls (WAV, MP3, OGG, FLAC)
- AI-driven scoring of configurable QA parameters using Google Gemini 1.5 Pro
- RAG using a project knowledge base (uploaded policy documents)
- Sentiment and emotion analysis on call transcripts
- PII redaction before data storage
- Human review and override of AI-suggested scores
- Analytics dashboard with trend charts and decision-assurance explainability views
- Training Needs Identification (TNI) report generation
- Insights Chat – natural language Q&A over audit data
- Role-based access control (Admin / QA Analyst / Agent)

**OUT OF SCOPE:**
- Real-time (live call) transcription and scoring
- CRM / ticketing system integration (future phase)
- Mobile native application
- Multi-language STT beyond en-US in this phase

---

## 2. Outcomes & Measurement

### Key Metrics

| Metric | Measurement Method | Baseline | Target |
|---|---|---|---|
| QA Analyst throughput (calls/day) | Audit-log count per analyst per day | 10 calls | 50+ calls |
| AI vs human score correlation | Pearson r in HumanReview module | N/A | >= 0.85 |
| Time per evaluation | Upload to reviewed result timer | ~30 min | < 5 min |
| Adoption rate | Active users / licensed users | 0% | 80% in 90 days |
| PII redaction accuracy | Manual spot-check sample | N/A | >= 99% |

### Target Outcomes
- Reduce per-call QA time from ~30 minutes to **< 5 minutes** within 90 days of pilot
- Achieve **>= 0.85** correlation between AI scores and human-reviewed scores
- Onboard **80%** of targeted QA analysts within 3 months
- **Zero** PII data leakage incidents post go-live

### Measurement Method
- AuditLog table tracks every action; Analytics API surfaces throughput metrics
- HumanReview module captures both AI and human scores – diff computed automatically
- GCP Cloud Monitoring dashboards for uptime, latency, error rate

### Qualitative Feedback
- User satisfaction survey at 30 / 60 / 90-day pilot checkpoints
- QA analyst interviews at end of each sprint
- In-app feedback form linked from the Review page
- Weekly review meeting with TechM GVO team to surface blockers

### Alignment with Objectives
All metrics map directly to the project objective of reducing manual QA effort by 60% while improving consistency. Throughput and time-saved metrics measure the "do more with less" goal; AI/human correlation measures quality; PII metrics address data-safety obligations.

---

## 3. Requirements

| Priority | User Requirement | User Story |
|---|---|---|
| P0 | Audio transcription | As a QA analyst, I upload an audio file and receive a full transcript within 2 minutes so I can start the evaluation. |
| P0 | AI-based QA scoring | As a QA analyst, I receive AI-suggested scores for each parameter so I can review rather than fill in from scratch. |
| P0 | Human review & override | As a QA analyst, I can accept, modify, or reject each AI-suggested score so final scores reflect my professional judgement. |
| P0 | Configurable evaluation forms | As an Admin, I design evaluation forms with sections, fields and rating scales tailored to each project. |
| P1 | RAG knowledge base | As an Admin, I upload policy documents that Gemini references when scoring parameters. |
| P1 | Call Pipeline batch processing | As a QA manager, I submit a batch of audio files and monitor real-time progress via SignalR. |
| P1 | Analytics dashboard | As a QA manager, I view trend charts, pass/fail rates, and agent comparison data. |
| P1 | TNI report generation | As a QA manager, I generate a Training Needs Identification report per agent automatically. |
| P2 | Insights Chat | As a QA manager, I ask natural-language questions about audit data and receive summarised answers. |
| P2 | Sentiment analysis | As a QA analyst, I see customer and agent sentiment labels on each call. |
| P2 | PII redaction | As a compliance officer, I require that all stored transcripts have personal data automatically redacted. |
| P3 | Azure OpenAI fallback | As an Admin, I can switch the AI engine between Gemini and Azure OpenAI via AI Settings. |
| P3 | Sampling policies | As a QA manager, I configure rules that auto-sample a percentage of calls from each LOB. |

---

## 4. Data Access, Security, and Retention

### Data Requirements
- Audio call recordings (WAV/MP3/OGG/FLAC, ≤10 MB inline; GCS URI for larger files)
- Call transcripts (Google Cloud STT output; PII-redacted before storage)
- Evaluation forms and scoring parameters (`EvaluationForms`, `Parameters` tables)
- Knowledge-base documents (`KnowledgeSources` table – chunked and indexed for RAG)
- User and role data (`AppUsers`, `UserProjectAccess` tables)
- AI configuration including API keys (`AiConfigs` table – encrypted at rest)
- Audit logs (`AuditLogs` table – immutable action history)

### Data Use & Minimisation
- Audio files are transcribed and discarded from memory; only text transcript persisted
- `PiiRedactionService` masks personal data (names, phone numbers, account IDs) before DB write
- Only minimum required fields (transcript, scores, reasoning) stored per evaluation

### Data Storage & Security
- **Primary storage:** SQL Server 2019+ on GCP Compute Engine VM (e2-standard-4)
- Transparent Data Encryption (TDE) recommended for the QAAutomation database
- API port 5018 bound to `127.0.0.1` only – not reachable externally
- Google API keys stored in `AiConfigs` table (encrypted) or OS environment variables; **never** in source code
- HTTPS-only enforced via Nginx + Let's Encrypt TLS on port 443

### Data Access Controls
- **Role-based access:** Admin (full), QA Analyst (own project), Agent (read-only own results)
- Project-level isolation: `UserProjectAccess` table restricts cross-project access
- `AuditLog` records every create/update/delete action with timestamp and actor

### Data Retention & Deletion
- Evaluation records retained **24 months** (regulatory minimum) then soft-deleted
- Soft-delete pattern throughout – no hard deletes except on explicit Admin request
- GDPR Article 17 erasure: Admin purge endpoint nulls transcript and deletes audio blobs
- GDPR Article 15 subject access: Admin export endpoint generates per-user data export

### Privacy Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Audio contains customer PII (name, account #, address) | `PiiRedactionService` uses Gemini to identify and mask PII; audio not persisted to DB |
| Google Gemini processes transcript data | Processed under Google API ToS with customer keys; no training opt-out needed |
| SQL Server credential exposure | Credentials in env vars only; TDE at rest; no secrets in source code |
| Unauthorised cross-project access | `UserProjectAccess` row-level security enforced in all API controllers |

---

## 5. Dependencies

### External Service Dependencies

| Dependency | Endpoint / Version | Purpose | If Unavailable |
|---|---|---|---|
| Google Gemini API | `generativelanguage.googleapis.com/v1beta` — gemini-1.5-pro | QA scoring, sentiment, PII, insights chat | Fallback to Azure OpenAI or mock; jobs queue with error state |
| Google Cloud STT | `speech.googleapis.com/v1` | Audio-to-transcript | Call pipeline fails; transcript-upload path still works |
| SQL Server 2019+ | localhost (same VM) | All persistent storage | Full application unavailability; HA via AlwaysOn optional |
| Nginx | System package | TLS termination, reverse proxy | Direct HTTP on port 5000 still works (no TLS) |
| SignalR | ASP.NET Core 8 built-in | Real-time pipeline progress | Graceful degradation – UI polls via HTTP fallback |

### Internal Code Dependencies
- `GeminiHttpHelper` – Gemini REST wrapper; used by all AI services
- `CallPipelineService` – orchestrates STT → PII → QA scoring → sentiment → TNI
- `ScoringCalculator` – aggregates per-field scores into section and form totals
- `RuntimeAiServices` – runtime switch between Gemini and Azure OpenAI providers
- `AudioFormatHelper` – normalises audio to LINEAR16 WAV before STT submission

### Potential Risks
- Gemini 429 rate limits: `GeminiHttpHelper` uses exponential back-off with jitter
- Google Cloud STT 60-min free-tier quota: production requires billing enabled
- SQL Server Linux licence: covered by TechM enterprise agreement or Azure SQL migration
- Audio format edge cases: `AudioFormatHelper` tested on WAV/MP3/OGG/FLAC

---

## 6. Design

### Overview

The platform consists of two cooperating **ASP.NET Core 8** applications on a single GCP Compute Engine VM (e2-standard-4, Ubuntu 22.04 LTS):
- **QAAutomation.Web** (MVC, port 5000) – browser UI
- **QAAutomation.API** (Web API, port 5018, internal only) – business logic and AI orchestration

Nginx terminates TLS on port 443 and reverse-proxies to the Web app. SQL Server provides persistent storage.

### Architecture

```
Browser
  | HTTPS 443
  v
Nginx (TLS termination, reverse proxy)
  | HTTP 127.0.0.1:5000
  v
QAAutomation.Web  (ASP.NET Core 8 MVC, port 5000)
  | HTTP /api/*
  v
QAAutomation.API  (ASP.NET Core 8 Web API, port 5018, internal only)
  |              |                     |
  v              v                     v
SQL Server   Google Gemini API   Google Cloud STT
(localhost)  (gemini-1.5-pro)   (speech.googleapis)
```

### Auto Audit Data Flow
1. User uploads transcript via `POST /AutoAudit/Upload`
2. Web forwards to API `POST /api/autoaudit/analyze`
3. API retrieves `EvaluationForm` + `Parameters` from SQL Server
4. [RAG] Top-K knowledge chunks fetched from `KnowledgeSources`
5. Gemini prompt built: system + form definition + transcript + RAG context
6. Gemini returns structured JSON: per-parameter score, confidence, reasoning
7. [Optional] Sentiment analysis via `GoogleGeminiSentimentService`
8. Review page rendered; analyst reviews/adjusts; stored to `Audits` + `EvaluationScores`

### Call Pipeline Data Flow
1. User uploads audio or provides URL / Azure Blob / SharePoint connector path
2. `CallPipelineService` downloads, validates, converts audio via `AudioFormatHelper`
3. Google Cloud STT produces raw transcript
4. `PiiRedactionService` masks PII entities
5. `NegativeIntentDetector` flags hostile/escalation language
6. QA Scoring (same Gemini flow as Auto Audit)
7. Sentiment analysis and TNI generation run in parallel
8. SignalR pushes real-time progress to browser
9. Results persisted to `CallRecords` + `Audits` tables

### Alternatives Considered

| Dimension | Proposed: Gemini on GCP VM | Alt 1: Azure OpenAI on Azure VM | Alt 2: On-prem LLM (Ollama) |
|---|---|---|---|
| LLM quality | ✅ Gemini 1.5 Pro state-of-art reasoning | ~ GPT-4o comparable quality | ❌ Smaller models; lower accuracy |
| Cost | ✅ Pay-per-token; Google API key only | ~ Azure OpenAI pricing similar | ✅ No per-token cost; GPU CapEx |
| Infra complexity | ✅ Single GCP VM; GCP APIs co-located | ~ Azure VM + Azure OpenAI separate billing | ❌ GPU VM + model management overhead |
| STT integration | ✅ Google STT same ecosystem | ❌ Azure Cognitive Services endpoint | ❌ Whisper on-prem; extra setup |
| Data residency | ~ Google data centres (region selectable) | ~ Azure region selectable | ✅ Fully on-prem |

---

## 7. Solution Risks & Mitigation

### Reliability & Stability
- Both services run as `systemd` units with `RestartPolicy=always` – auto-restart on crash
- `GeminiHttpHelper`: exponential back-off with jitter for rate-limit errors (429)
- SQL Server recovery model FULL; nightly backup to GCS bucket recommended
- Nginx + GCP uptime monitoring alert on `/api/ping` endpoint

### Scalability Considerations
- Current target: ~50 concurrent users, ~500 calls/day on e2-standard-4 VM
- Scale-out path: API to Cloud Run (stateless), SQL Server to Cloud SQL
- Redis / Memorystore required for multi-instance SignalR backplane
- Per-project API key quotas + request queuing in `CallPipelineService`

### Risk Register

| Risk | Likelihood | Impact | Mitigation | Owner |
|---|---|---|---|---|
| Gemini 429 rate limit during batch processing | Medium | High | Exponential back-off; configurable RPM throttle in `CallPipelineService` | Google / Internal |
| Audio format unsupported by STT | Low | Medium | `AudioFormatHelper` pre-converts to LINEAR16 WAV | Internal |
| SQL Server disk full from large transcripts | Low | High | PII-redacted text only; audio not persisted; disk alert at 80% | Internal |
| User adoption resistance | Medium | Medium | In-app onboarding, training sessions, feedback loop via InsightsChat | Internal |
| PII leakage in stored transcripts | Low | High | Gemini-based `PiiRedactionService`; compliance audit at 30-day checkpoint | Internal |
| Gemini scoring inconsistency across model versions | Medium | Medium | Pin model version in `AiConfig`; regression tests with golden transcripts | Internal |
| Port 5018 accidentally exposed externally | Low | High | GCP firewall blocks all non-443/80; 5018 bound to `127.0.0.1` only | Internal |

### Accessibility
- Bootstrap 5 throughout – WCAG 2.1 AA compliant components
- All form inputs have associated labels; screen-reader tested
- Chart.js charts include `aria-label` and data-table fallbacks

### Other Compliance
- GDPR Art. 17 (erasure): Admin purge endpoint removes transcript and audio data
- GDPR Art. 15 (subject access): Admin export endpoint generates per-user data export
- PCI-DSS: payment card numbers matched and redacted by `PiiRedactionService`

---

## 8. Project Management

### Timeline

| Phase | Activities | Duration | Target Date |
|---|---|---|---|
| Design | Architecture finalisation, DB schema review, API contract sign-off | 2 weeks | April 2026 |
| Dev Sprint 1 | Core API, EvaluationForms, Auto Audit with Gemini integration | 3 weeks | May 2026 |
| Dev Sprint 2 | Call Pipeline, STT, PII redaction, Human Review module | 3 weeks | May 2026 |
| Dev Sprint 3 | Analytics, TNI, Insights Chat, Sampling Policies | 3 weeks | June 2026 |
| Testing | Integration tests, security pen-test, performance load test | 2 weeks | June 2026 |
| Pilot | Selected GVO team pilot; feedback collection; bug fixes | 4 weeks | July 2026 |
| Go-Live | Production deployment on GCP VM, monitoring setup, user training | 1 week | August 2026 |

### Key Deliverables

| Deliverable | Description | Owner | Due |
|---|---|---|---|
| Working prototype | Auto Audit + Call Pipeline end-to-end on staging GCP VM | Engineering | May 2026 |
| Security review | Pen-test report and remediations signed off | Security team | June 2026 |
| Technical documentation | `docs/Technical-Architecture.md` (already complete) | Engineering | April 2026 |
| User training materials | Video walkthrough + PDF quick-start guide | Product | July 2026 |
| Pilot report | Metrics vs targets, qualitative feedback summary | QA Manager | Aug 2026 |
| Production deployment | Go-live on GCP VM with monitoring enabled | DevOps | Aug 2026 |
| GDPR compliance cert | Signed-off data-processing assessment | Legal | July 2026 |
