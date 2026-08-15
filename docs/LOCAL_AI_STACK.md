# Local AI Stack

## 1. Objective

JARVIS must remain useful without a paid runtime AI subscription.

The default stack is local.

```text
Wake word  -> local
STT        -> local
LLM        -> local
TTS        -> local
Memory     -> local
Tools      -> local
```

## 2. LLM — Ollama

Default runtime:
- Ollama on Windows.

Integration:
- HTTP on loopback.
- Provider adapter behind `ILlmProvider`.

Configuration example:

```json
{
  "Jarvis": {
    "Llm": {
      "Provider": "Ollama",
      "BaseUrl": "http://localhost:11434",
      "Model": "qwen3:8b",
      "TimeoutSeconds": 120
    }
  }
}
```

The model value is an example, not a hard dependency.

Requirements:
- configurable model,
- availability check,
- helpful startup diagnostics,
- cancellation,
- graceful degradation if unavailable.

## 3. Model selection

Do not optimize prematurely.

Start by benchmarking a small number of local models for:

- Turkish comprehension,
- structured tool calling,
- latency,
- memory consumption,
- hallucination rate,
- instruction following.

Candidate families may include:
- Qwen,
- GPT-OSS,
- other Ollama-compatible tool-capable models.

Model choice is runtime configuration.

## 4. Speech-to-text — Whisper.net

Goals:
- local transcription,
- Turkish,
- provider abstraction,
- configurable model size,
- CPU/GPU runtime selection later.

Do not package huge speech models into source control.

Expected local model assets should live under a user/configurable data directory.

## 5. Text-to-speech — Piper

Goals:
- local Turkish TTS,
- low latency,
- interruptible playback,
- configurable voice.

Treat Piper as an external local runtime adapter.

Do not couple Core to Piper CLI syntax.

## 6. Wake word — openWakeWord

Initial candidate:
- local wake-word detection,
- `Hey Jarvis` / `Jarvis`.

Important:
openWakeWord is Python-centric.

Preferred containment strategy:

```text
Jarvis.Audio
   |
   v
WakeWord Adapter
   |
   v
Minimal local sidecar
   |
   v
openWakeWord
```

The sidecar should:
- do wake-word detection only,
- expose the smallest possible protocol,
- bind locally only,
- terminate with the desktop app,
- contain no business logic.

A future native/.NET-compatible detector can replace it without affecting Core.

## 7. Fallback behavior

Subsystem availability should be independent where practical.

Examples:

- Wake-word unavailable:
  - allow push-to-talk/UI activation.

- TTS unavailable:
  - show text response.

- Ollama unavailable:
  - show a clear local model error.
  - do not silently switch to a paid provider.

- Microphone unavailable:
  - allow typed commands.

This makes development and debugging easier.

## 8. Optional cloud providers

Future adapters:

```text
OpenAiLlmProvider
GeminiLlmProvider
OtherProvider
```

Rules:
- opt-in,
- disabled by default,
- clearly labeled as potentially billable,
- secrets stored outside source,
- never silently selected.

## 9. Performance philosophy

Voice assistants feel slow if every operation requires an LLM.

Use:
- deterministic intent routing,
- cached application aliases,
- local models,
- streaming where useful,
- short spoken responses.

Measure before optimizing.

Important metrics:
- wake detection latency,
- end-of-speech to transcript latency,
- transcript to first tool call,
- tool execution duration,
- tool result to first spoken audio.

## 10. Asset policy

Do not commit:
- large LLM weights,
- Whisper model binaries,
- TTS model binaries,
- generated caches.

Document download/setup commands instead.
