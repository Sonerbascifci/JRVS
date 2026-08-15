# Architecture

## 1. Goal

JARVIS is a Windows desktop personal AI assistant built as a modular monolith.

The architecture optimizes for:

- local-first operation,
- zero mandatory API cost,
- replaceable AI/audio providers,
- safe OS interaction,
- maintainability,
- testability,
- gradual expansion.

## 2. System context

```text
User
 |
 | voice / UI
 v
+----------------------+
|   Jarvis.Desktop     |
|   WPF + App Host     |
+----------+-----------+
           |
           v
+----------------------+
|     Jarvis.Core      |
| Orchestration/State  |
| Intent/Permissions   |
+----+-------------+---+
     |             |
     |             |
     v             v
+---------+   +----------------+
| AI      |   | Audio          |
| Ollama  |   | Wake/STT/TTS   |
+----+----+   +-------+--------+
     |                |
     +--------+-------+
              |
              v
      +---------------+
      | Tool Registry |
      +-------+-------+
              |
        +-----+------+
        |            |
        v            v
+---------------+ +----------------+
| Windows Tools | | Developer Tools|
+---------------+ +----------------+
              |
              v
      +----------------+
      | Permission     |
      | / Confirmation |
      +----------------+

Persistence:
Jarvis.Core <-> repository abstractions <-> Jarvis.Persistence <-> SQLite
```

## 3. Architectural style

Use a **modular monolith**.

Reasons:

- Single-user desktop product.
- In-process calls are simpler than network services.
- Easier debugging and deployment.
- Lower operational overhead.
- Clear module boundaries can still be enforced through project references.

Do not introduce microservices for v0.1.

## 4. Project dependency direction

Preferred dependency graph:

```text
Jarvis.Desktop
  -> Jarvis.Core
  -> Jarvis.AI
  -> Jarvis.Audio
  -> Jarvis.Tools.Windows
  -> Jarvis.Tools.Developer
  -> Jarvis.Persistence

Jarvis.AI
  -> Jarvis.Core

Jarvis.Audio
  -> Jarvis.Core

Jarvis.Tools.Windows
  -> Jarvis.Core

Jarvis.Tools.Developer
  -> Jarvis.Core

Jarvis.Persistence
  -> Jarvis.Core
```

`Jarvis.Core` should not depend on implementation projects.

## 5. Runtime state model

Initial assistant states:

```text
Idle
Awakened
Listening
Processing
AwaitingConfirmation
ExecutingTool
Speaking
Faulted
```

Typical voice interaction:

```text
Idle
 -> wake word
Awakened
 -> capture speech
Listening
 -> transcription complete
Processing
 -> optional tool request
AwaitingConfirmation? 
 -> tool execution
ExecutingTool
 -> model/user response
Speaking
 -> follow-up window or timeout
Idle
```

Transitions should be represented explicitly rather than inferred from UI flags.

## 6. AI routing strategy

JARVIS uses a hybrid model.

### Deterministic path

For simple, low-ambiguity commands:

```text
"Spotify'ı aç"
"Sesi yüzde 30 yap"
"CPU kullanımım ne?"
```

Pipeline:

```text
Speech/Text
 -> deterministic intent matcher
 -> typed tool request
 -> permission
 -> execution
```

### LLM path

For ambiguous or reasoning-heavy commands:

```text
"Dün üzerinde çalıştığım projeyi aç ve Git durumunu özetle."
```

Pipeline:

```text
Speech/Text
 -> LLM
 -> structured tool call(s)
 -> validation
 -> permission
 -> execution
 -> tool result
 -> LLM response
```

Deterministic routing is an optimization, not a duplicate business-rule layer. It must still call the same tool registry and permission engine.

## 7. Provider abstractions

Core must not know provider details.

Expected capability abstractions:

```text
ILlmProvider
ISpeechToTextProvider
ITextToSpeechProvider
IWakeWordDetector
IAudioCapture
IAudioPlayback
IMemoryRepository
IConversationRepository
IToolRegistry
IPermissionEvaluator
IUserConfirmationService
```

Initial adapters:

```text
ILlmProvider
 -> OllamaLlmProvider

ISpeechToTextProvider
 -> WhisperNetSpeechToTextProvider

ITextToSpeechProvider
 -> PiperTextToSpeechProvider

IWakeWordDetector
 -> OpenWakeWordAdapter/Sidecar adapter
```

Cloud providers are future optional adapters.

## 8. Wake-word integration note

openWakeWord is Python-oriented.

For the initial design, treat wake-word detection as an adapter boundary. Acceptable implementations include:

1. a minimal local Python sidecar communicating over loopback/stdin/stdout, or
2. a later native/.NET-compatible local wake-word provider.

The rest of JARVIS must not depend on which implementation is chosen.

Do not let the sidecar become a general-purpose Python backend.

## 9. Ollama integration

Ollama is the default local LLM runtime.

JARVIS should communicate with Ollama through a dedicated provider adapter over loopback HTTP.

Requirements:

- configurable base URL,
- configurable model,
- startup health check,
- clear error when Ollama is unavailable,
- request timeout,
- cancellation,
- provider DTOs mapped into Core DTOs,
- support structured tool-call exchange when the selected model supports it.

Initial recommended model should remain configurable. Do not hard-code Qwen, GPT-OSS or any single model into Core.

## 10. Memory

v0.1 memory is intentionally simple.

SQLite entities may include:

### UserPreference
- Id
- Key
- Value
- CreatedAt
- UpdatedAt

### Memory
- Id
- Category
- Content
- Importance
- CreatedAt
- LastAccessedAt

### ConversationSession
- Id
- StartedAt
- EndedAt
- Summary

Avoid storing full raw audio.

Avoid storing full raw conversations indefinitely by default.

Semantic/vector memory is deferred.

## 11. Tool execution architecture

Tools are capabilities, not prompts.

Example tool names:

```text
open_application
open_url
get_system_status
find_file
open_file
open_project
git_status
```

Each tool:

- receives typed arguments,
- validates arguments,
- declares risk,
- returns typed structured result,
- supports cancellation,
- contains no LLM-specific code.

The orchestrator owns the call loop.

## 12. Local REST API

Not required for v0.1 foundation.

Future API design:

- disabled by default,
- loopback-only by default,
- authentication/token required before non-trivial actions,
- no tool endpoint bypassing permission policy.

This will support a future Flutter remote client.

## 13. Future modules

Deferred intentionally:

- Home Assistant
- mobile Flutter client
- computer vision
- web research agent
- semantic/vector memory
- proactive automation
- guest mode
- local REST control API
- cloud providers

Their future existence must not distort v0.1 design.
