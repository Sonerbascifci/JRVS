# Roadmap

## Guiding rule

Complete work vertically and incrementally.

A backlog item is not complete until it satisfies `DEFINITION_OF_DONE.md`.

---

# Phase 0 — Foundation

## FOUND-001 — Solution Foundation

### Goal
Create a healthy .NET 10 solution with project boundaries, dependency injection, configuration, logging and test projects.

### Scope
- Create solution.
- Create WPF desktop project.
- Create Core library.
- Create AI library.
- Create Audio library.
- Create Windows Tools library.
- Create Developer Tools library.
- Create Persistence library.
- Create unit/integration test projects.
- Enable nullable reference types.
- Configure common build settings.
- Configure Generic Host in desktop application.
- Configure structured logging foundation.
- Add strongly typed application options.
- Add test smoke tests for composition.
- Add basic CI if repository environment is available.

### Out of scope
- Ollama calls.
- Audio capture.
- Wake word.
- Tool execution.
- SQLite schema.
- WPF visual design beyond the minimum shell.

### Acceptance criteria
- `dotnet build` succeeds.
- `dotnet test` succeeds.
- Desktop project starts.
- DI resolves the initial application services.
- No secrets are committed.
- Project references follow `ARCHITECTURE.md`.

---

## FOUND-002 — Core Contracts

### Goal
Introduce provider, tool, state and result contracts without implementing real integrations.

### Scope
- `ILlmProvider`
- `ISpeechToTextProvider`
- `ITextToSpeechProvider`
- `IWakeWordDetector`
- tool contracts
- risk levels
- permission contracts
- assistant state model
- result/error primitives

### Acceptance criteria
- Core remains implementation-independent.
- Contract behavior covered by unit tests where applicable.

---

# Phase 1 — Local AI

## AI-001 — Ollama Provider

### Goal
Connect to local Ollama through `ILlmProvider`.

### Scope
- configuration,
- health check,
- chat request/response,
- cancellation,
- timeout/error handling,
- model selection.

### Out of scope
- tool-calling loop.

---

## AI-002 — Structured Tool Calling

### Goal
Allow supported local models to request model-visible tools through a typed,
provider-independent protocol.

### Scope
- project trusted tool descriptors without security-policy metadata,
- map typed argument schemas to provider-native tool definitions,
- parse ordered provider-native tool calls into typed Core contracts,
- round-trip assistant tool-call history and structured tool results,
- reject unavailable tools and malformed arguments without execution.

### Out of scope
- tool resolution and execution,
- permission and confirmation orchestration,
- agent-loop and maximum-step enforcement.

---

## CORE-003 — Hybrid Intent Router

### Goal
Route simple commands without invoking the LLM when confidence is sufficiently high.

### Initial deterministic candidates
- open known application,
- open URL,
- basic system status.

### Rule
Deterministic routes must use the same tool and permission pipeline as LLM routes.

---

# Phase 2 — Tool Platform

## TOOL-001 — Tool Registry

- registration,
- unique name validation,
- metadata,
- typed argument validation,
- execution abstraction.

## TOOL-002 — Tool Execution Pipeline

- resolve requested tools through the trusted registry,
- evaluate permission and confirmation policy before execution,
- execute approved calls and preserve real tool failures,
- append structured results for the next model turn,
- enforce maximum agent steps without bypassing the permission engine.

## SEC-001 — Permission Engine

Risk levels:
- Safe
- Confirm
- Critical

Critical actions disabled by default.

## WIN-001 — Open Application

Safe.

## WIN-002 — Open URL

Safe.

## WIN-003 — System Status

Safe.

Return:
- CPU,
- memory,
- disk summary.

## WIN-004 — Find File

Safe/read-only.

## DEV-001 — Open Project

Safe.

## DEV-002 — Git Status

Safe/read-only.

No arbitrary Git command execution.

---

# Phase 3 — Voice

## AUDIO-001 — Audio Capture

- enumerate microphones,
- select configured/default device,
- capture frames,
- cancellation,
- basic diagnostics.

## VOICE-001 — Whisper.net STT

- Turkish transcription,
- model configuration,
- local model lifecycle,
- errors and cancellation.

## VOICE-002 — Piper TTS

- local executable/provider adapter,
- Turkish voice configuration,
- playback,
- interruption/cancellation.

## WAKE-001 — Wake Word

Initial target: local `Hey Jarvis` / `Jarvis`.

Keep implementation behind `IWakeWordDetector`.

If openWakeWord requires Python:
- use only a minimal sidecar,
- communicate locally,
- document startup/shutdown lifecycle,
- do not move application logic into Python.

---

# Phase 4 — Conversation

## CONV-001 — Assistant State Machine

States:
- Idle
- Awakened
- Listening
- Processing
- AwaitingConfirmation
- ExecutingTool
- Speaking
- Faulted

## CONV-002 — Voice Conversation Loop

Goal:

```text
"Jarvis"
 -> wake
 -> user speech
 -> STT
 -> intent/LLM
 -> optional tool
 -> response
 -> TTS
```

## CONV-003 — Interruption / Barge-In

Allow user to stop or interrupt spoken output.

---

# Phase 5 — Memory

## MEM-001 — SQLite Foundation

- EF Core SQLite,
- migrations,
- repository abstractions,
- user preferences.

## MEM-002 — Explicit Memory

Examples:
- preferred IDE,
- project aliases,
- preferred browser.

Memory must not automatically persist sensitive information.

## MEM-003 — Session Summary

Store compact summaries rather than indefinite raw transcript history.

---

# Phase 6 — Desktop Experience

## UI-001 — Tray Application

- launch to tray,
- open/exit,
- state indicator.

## UI-002 — Minimal Assistant Window

Show:
- current state,
- last user command,
- latest response,
- tool execution status,
- confirmation prompt.

## UI-003 — Settings

- LLM provider/model,
- Ollama base URL,
- STT model,
- TTS voice,
- microphone,
- wake word enable/disable,
- privacy/history options.

---

# Phase 7 — Safety Enhancements

## SEC-002 — Dry Run Mode

Show planned actions without executing.

## SEC-003 — Guest Mode

Restrict:
- memory access,
- write actions,
- sensitive tools.

## SEC-004 — Agent Step Limit

Hard maximum on sequential tool/LLM steps.

## SEC-005 — Tool Audit Log

Store structured action metadata without unnecessary private content.

---

# Phase 8 — Vision

## VISION-001 — Screenshot Provider

Explicit user-triggered screenshot capture.

## VISION-002 — Local Vision Model

Optional Ollama vision provider.

No continuous screen recording.

---

# Phase 9 — Remote / Home

## API-001 — Local API

Loopback by default.

## MOBILE-001 — Flutter Remote Client

Remote UI for the same JARVIS Core.

## HOME-001 — Home Assistant

Smart-home tool provider.

---

# Phase 10 — Optional Cloud

## CLOUD-001 — OpenAI Provider

Optional only.

## CLOUD-002 — Other Providers

Optional adapters only.

No cloud provider may become mandatory for core operation.
