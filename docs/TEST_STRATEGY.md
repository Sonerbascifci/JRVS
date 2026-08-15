# Test Strategy

## 1. Goal

The most important JARVIS behaviors must be testable without:

- a real microphone,
- an Ollama server,
- actual speech playback,
- modifying the user's machine,
- real file deletion,
- external network access.

## 2. Test projects

```text
tests/
  Jarvis.Core.Tests
  Jarvis.Tools.Tests
  Jarvis.IntegrationTests
```

## 3. Core unit tests

High priority:

### Assistant state transitions
Examples:
- Idle -> Awakened
- Listening -> Processing
- Processing -> AwaitingConfirmation
- ExecutingTool -> Speaking
- cancellation/fault transitions

### Permission rules
Examples:
- Safe tool executes without confirmation.
- Confirm tool cannot execute before approval.
- Critical tool is blocked when disabled.
- changing arguments invalidates prior confirmation.

### Tool registry
Examples:
- duplicate tool names rejected.
- unknown tool returns predictable error.
- descriptors exposed correctly.

### Agent step limit
Examples:
- stops after configured maximum.
- cancellation terminates the loop.

### Intent routing
Examples:
- deterministic high-confidence intent routes correctly.
- ambiguous text falls back to LLM.
- deterministic route still uses permission engine.

## 4. Tool tests

Use abstractions/fakes around OS interactions where practical.

Examples:
- `open_url` rejects unsafe schemes.
- `open_application` never treats name as shell text.
- `git_status` parses representative output safely.
- `find_file` respects limits and allowed roots.

Tests must not rely on a developer's specific installed programs.

## 5. Provider contract tests

For Ollama adapter:
- map request correctly,
- map response correctly,
- handle timeout,
- handle unavailable server,
- handle malformed JSON,
- cancellation.

Use a fake HTTP handler/server.

Do not require a real LLM for normal CI.

## 6. Persistence tests

Use isolated temporary SQLite databases.

Test:
- migrations,
- preference CRUD,
- memory persistence,
- expected indexes/constraints.

## 7. Integration tests

Focus on boundaries:

```text
Orchestrator
 -> ToolRegistry
 -> PermissionEngine
 -> Fake Tool
```

and:

```text
Ollama Provider
 -> fake HTTP endpoint
```

Keep real hardware tests out of standard CI.

## 8. Manual smoke tests

Maintain a small manual checklist for hardware integrations:

- microphone enumeration,
- Turkish STT,
- wake word,
- Turkish TTS,
- application opening,
- tray behavior.

Manual tests supplement automated tests; they do not replace them.

## 9. Test naming

Prefer descriptive names.

Example:

```csharp
ExecuteAsync_WhenToolRequiresConfirmation_DoesNotExecuteBeforeApproval()
```

## 10. Regression rule

Every bug fix should add a regression test when the failure is reproducible in automated tests.
