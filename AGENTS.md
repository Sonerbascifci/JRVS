# AGENTS.md

This file defines the mandatory operating rules for AI coding agents working on JARVIS.

## 1. Mission

Build JARVIS as a reliable, local-first personal AI assistant for Windows.

The primary goal is not to produce a flashy demo. The goal is to build an extensible desktop software system that can safely evolve into a long-running personal assistant.

## 2. Non-negotiable product principles

1. **Local-first**
   - Core functionality must not require a paid AI API.
   - The default LLM provider is local Ollama.
   - The default STT provider is local Whisper.net.
   - The default TTS provider is local Piper.
   - Wake-word detection must run locally.
   - SQLite is the default persistent store.

2. **Provider abstraction**
   - JARVIS must not be tightly coupled to Ollama, Whisper.net, Piper, OpenAI, Gemini, or any other provider.
   - Provider-specific code belongs behind interfaces.
   - Cloud providers may be added later as optional adapters.

3. **Safe tool execution**
   - The LLM never receives arbitrary OS authority.
   - Every executable capability must be represented by an explicitly registered tool.
   - Every tool must have a declared risk level.
   - Destructive or sensitive actions require confirmation.
   - The model cannot bypass the permission engine.

4. **Deterministic before agentic**
   - Simple, unambiguous commands should be handled by deterministic routing when practical.
   - Use the LLM when language interpretation, reasoning, ambiguity resolution, summarization, or multi-step planning is genuinely useful.

5. **Small changes**
   - Prefer the smallest change that satisfies the active backlog item.
   - Do not implement future roadmap items "while already here".
   - Avoid speculative abstractions.

6. **Testable behavior**
   - Business rules, permission decisions, tool routing, state transitions, and parsing logic must be testable without a real microphone, model, or OS side effect.

7. **Observable behavior**
   - Important lifecycle events must be logged.
   - Never log secrets.
   - Avoid logging raw private conversation content by default.

8. **No fake success**
   - Never tell the user an action succeeded until the real tool result reports success.
   - Errors must remain errors; do not silently convert them into success messages.

## 3. Required reading before changing code

Before implementing a backlog item, read the relevant files:

- `docs/ARCHITECTURE.md`
- `docs/ROADMAP.md`
- `docs/SECURITY.md`
- `docs/TOOL_SYSTEM.md`
- `docs/DEVELOPMENT_STANDARDS.md`
- `docs/TEST_STRATEGY.md`
- `docs/DEFINITION_OF_DONE.md`
- `docs/DECISIONS.md`

For voice/AI work also read:

- `docs/LOCAL_AI_STACK.md`

## 4. Workflow for every task

For each backlog item:

1. Read the relevant documentation and existing implementation.
2. Identify the smallest set of files that need to change.
3. State the implementation plan briefly before editing.
4. Implement only the current scope.
5. Add or update tests.
6. Run relevant tests.
7. Run formatting/static checks if configured.
8. Review the diff for accidental changes.
9. Update documentation only when behavior or architecture changed.
10. Summarize:
   - files changed,
   - behavior added,
   - tests run,
   - known limitations,
   - next logical backlog item.

## 5. Prohibited behavior

Do not:

- Add a paid service as a mandatory runtime dependency.
- Hard-code API keys, secrets, user paths, usernames, machine names, tokens, or passwords.
- Execute arbitrary LLM-generated PowerShell, CMD, Bash, Python, JavaScript, or other code.
- Add "run_shell_command(string command)" as an unrestricted tool.
- Allow the LLM to choose or override tool risk levels.
- Allow tools to execute before permission checks.
- Delete files permanently by default.
- Modify unrelated files during a focused task.
- Introduce a microservice architecture for v0.1.
- Add Docker/Kubernetes unless a future requirement clearly needs it.
- Add a vector database before semantic retrieval is actually required.
- Add an event bus or message broker for in-process communication in v0.1.
- Store complete raw conversations indefinitely by default.
- Swallow exceptions without logging or returning a meaningful failure result.
- Claim tests passed if they were not run.
- rewrite working modules just to match personal style.

## 6. Architecture boundaries

Expected projects:

```text
src/
  Jarvis.Desktop
  Jarvis.Core
  Jarvis.AI
  Jarvis.Audio
  Jarvis.Tools.Windows
  Jarvis.Tools.Developer
  Jarvis.Persistence

tests/
  Jarvis.Core.Tests
  Jarvis.Tools.Tests
  Jarvis.IntegrationTests
```

Responsibilities:

### Jarvis.Core
Pure application/domain abstractions and orchestration.

Must not depend on:
- WPF
- Ollama HTTP details
- Whisper implementation details
- Piper process details
- EF Core
- Windows-specific tool implementations

### Jarvis.Desktop
WPF composition root and UI.

### Jarvis.AI
LLM provider adapters, prompting, tool-call protocol mapping, agent loop.

### Jarvis.Audio
Microphone, wake word, speech-to-text and speech playback abstractions/adapters.

### Jarvis.Tools.Windows
Explicit Windows capabilities.

### Jarvis.Tools.Developer
Developer-focused tools such as project opening and safe Git inspection.

### Jarvis.Persistence
SQLite / EF Core persistence and repository adapters.

## 7. Core interface direction

Prefer dependency inversion.

Examples:

```csharp
public interface ILlmProvider
{
    Task<LlmResponse> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken);
}

public interface ISpeechToTextProvider
{
    Task<SpeechRecognitionResult> TranscribeAsync(
        AudioInput input,
        CancellationToken cancellationToken);
}

public interface ITextToSpeechProvider
{
    Task SpeakAsync(
        string text,
        CancellationToken cancellationToken);
}

public interface IWakeWordDetector
{
    Task WaitForWakeWordAsync(CancellationToken cancellationToken);
}
```

Exact signatures may evolve when implementation begins. Do not force these examples if real requirements suggest a cleaner contract.

## 8. Tool rule

Every tool must expose:

- stable machine-readable name,
- human-readable description,
- typed argument contract,
- risk level,
- execution result,
- cancellation support.

Every execution must pass through:

```text
Request
  -> Tool resolution
  -> Argument validation
  -> Permission evaluation
  -> Optional confirmation
  -> Tool execution
  -> Tool result
  -> Response generation
```

Never skip the permission stage.

## 9. Risk model

Use:

```text
Safe
Confirm
Critical
```

Risk is declared by code/policy, never inferred by the LLM at runtime.

Examples:

Safe:
- get system status
- open an application
- open a URL
- inspect git status
- search for files

Confirm:
- close an application with unsaved-work risk
- modify a file
- git pull
- create or overwrite user content

Critical:
- delete files
- shutdown/restart
- install software
- execute privileged actions
- send sensitive external communications
- arbitrary shell execution

Critical actions should be disabled by default until explicitly implemented.

## 10. Coding expectations

- Nullable reference types enabled.
- Async APIs use `CancellationToken`.
- Prefer immutable records for messages/results/value objects.
- Do not use `dynamic` unless a provider integration genuinely requires it.
- Prefer strongly typed configuration with options validation.
- Do not expose provider DTOs into Core.
- Use structured logging.
- Keep public interfaces intentionally small.
- Prefer composition over inheritance.
- Avoid static global service locators.
- Avoid reflection-based magic when explicit registration is clearer.
- Use `DateTimeOffset` for persisted timestamps unless there is a specific reason not to.

## 11. Security expectations

Consult `docs/SECURITY.md`.

At minimum:

- secrets never committed,
- local HTTP endpoints bind to loopback by default,
- tool inputs validated,
- file operations restricted and normalized,
- path traversal defended against,
- no uncontrolled command execution,
- permission prompts cannot be forged by model output,
- memory is treated as untrusted data,
- prompt injection from files/web content must never directly grant tool authority.

## 12. First task

The first implementation task is:

**FOUND-001 — Solution Foundation**

Only after FOUND-001 is complete should work begin on audio, wake word, LLM or tool execution.
