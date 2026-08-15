# Architecture Decisions

This is a lightweight decision log.

---

## ADR-001 — Use .NET 10

Status: Accepted

Decision:
Use .NET 10 as the primary runtime.

Reason:
Modern LTS .NET foundation for a long-lived Windows desktop project.

---

## ADR-002 — Use WPF for initial desktop UI

Status: Accepted

Decision:
Use WPF rather than Flutter/Electron/MAUI for the first Windows desktop client.

Reason:
- Windows is the initial target.
- Strong .NET integration.
- Mature desktop APIs.
- Low cross-platform complexity.

Future mobile UI can be Flutter without changing Core architecture.

---

## ADR-003 — Modular monolith

Status: Accepted

Decision:
Use multiple projects in one process rather than microservices.

Reason:
JARVIS v0.1 is a single-user desktop application.

---

## ADR-004 — Local-first AI

Status: Accepted

Decision:
No paid AI API is required for core runtime.

Default:
- Ollama,
- Whisper.net,
- Piper,
- local wake word,
- SQLite.

Cloud adapters remain optional.

---

## ADR-005 — Provider abstractions

Status: Accepted

Decision:
LLM, STT, TTS and wake-word engines must be replaceable behind interfaces.

Reason:
Prevent vendor lock-in and allow zero-cost local operation.

---

## ADR-006 — Explicit tools only

Status: Accepted

Decision:
The model may only interact with the computer through explicitly registered tools.

Rejected:
General unrestricted shell execution.

---

## ADR-007 — Permission engine is outside the LLM

Status: Accepted

Decision:
Risk classification and user confirmation are enforced by trusted application code.

The model cannot override the decision.

---

## ADR-008 — Hybrid intent routing

Status: Accepted

Decision:
Simple high-confidence commands may bypass the LLM but must still use the same tool/permission pipeline.

Reason:
Lower latency and lower compute cost.

---

## ADR-009 — SQLite before vector database

Status: Accepted

Decision:
Use relational/local memory first.

Semantic/vector retrieval is deferred until real use cases justify it.

---

## ADR-010 — openWakeWord treated as replaceable adapter

Status: Accepted

Decision:
Because the initial local wake-word candidate is Python-centric, any Python integration must be isolated behind `IWakeWordDetector`.

No application/business logic should move into the Python sidecar.

---

## ADR-011 — No silent paid fallback

Status: Accepted

Decision:
If local AI is unavailable, JARVIS must not automatically call a billable cloud API.

A cloud provider can only be used after explicit user configuration/opt-in.
