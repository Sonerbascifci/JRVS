# JARVIS

Local-first, privacy-aware, extensible personal AI assistant for Windows.

JARVIS is intended to become a daily-use desktop assistant that can:

- Wake on a local wake word.
- Understand Turkish speech locally.
- Use a local LLM as its default reasoning engine.
- Execute explicitly registered tools on Windows.
- Ask for confirmation before risky actions.
- Remember useful user preferences and project context locally.
- Speak responses locally.
- Remain usable without a paid AI API.
- Support optional cloud providers later without coupling the core to them.

## Product principle

> Local by default. Explicit permissions. Small, auditable actions. Cloud only when the user opts in.

JARVIS v0.1 is **not** an autonomous computer-use agent and is **not** allowed to execute arbitrary shell commands by default.

## Target platform

- Windows 10/11
- .NET 10
- WPF desktop application
- SQLite + EF Core
- Ollama for local LLM inference
- Whisper.net for local speech-to-text
- Piper for local text-to-speech
- openWakeWord as the initial local wake-word candidate
- xUnit for tests

See:

- `AGENTS.md`
- `docs/ARCHITECTURE.md`
- `docs/ROADMAP.md`
- `docs/SECURITY.md`
- `docs/TOOL_SYSTEM.md`
- `docs/LOCAL_AI_STACK.md`
- `docs/DEVELOPMENT_STANDARDS.md`
- `docs/TEST_STRATEGY.md`
- `docs/DEFINITION_OF_DONE.md`
- `docs/DECISIONS.md`

## Current phase

**AI-001 — Ollama Provider** is complete.

The next backlog item is **TOOL-001 — Tool Registry**.
