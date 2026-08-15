# Codex First Prompt

Use this prompt when starting the first implementation task.

---

You are working on the JARVIS repository.

Before changing any code, read:

- `AGENTS.md`
- `README.md`
- `docs/PROJECT_CONTEXT.md`
- `docs/ARCHITECTURE.md`
- `docs/ROADMAP.md`
- `docs/SECURITY.md`
- `docs/TOOL_SYSTEM.md`
- `docs/LOCAL_AI_STACK.md`
- `docs/DEVELOPMENT_STANDARDS.md`
- `docs/TEST_STRATEGY.md`
- `docs/DEFINITION_OF_DONE.md`
- `docs/DECISIONS.md`

Implement **FOUND-001 — Solution Foundation** only.

Requirements:

1. Create a `.NET 10` solution named `Jarvis`.
2. Create these projects:

```text
src/
  Jarvis.Desktop          WPF
  Jarvis.Core             class library
  Jarvis.AI               class library
  Jarvis.Audio            class library
  Jarvis.Tools.Windows    class library
  Jarvis.Tools.Developer  class library
  Jarvis.Persistence      class library

tests/
  Jarvis.Core.Tests
  Jarvis.Tools.Tests
  Jarvis.IntegrationTests
```

3. Enforce the dependency direction described in `docs/ARCHITECTURE.md`.
4. Configure the desktop app with .NET Generic Host.
5. Add dependency injection, typed configuration and logging foundation.
6. Add a minimal WPF shell only; do not design the final UI.
7. Add shared build settings using `Directory.Build.props` where appropriate.
8. Enable nullable reference types.
9. Add xUnit test infrastructure.
10. Add at least one composition/smoke test that provides real value.
11. Do not add Ollama, Whisper.net, Piper, openWakeWord, SQLite/EF migrations, real tools or audio implementations yet.
12. Do not add arbitrary abstractions beyond what FOUND-001 actually needs.
13. Do not introduce secrets or machine-specific absolute paths.
14. Run:
   - `dotnet build`
   - `dotnet test`
15. Review the final diff.

Before editing, output a concise file-level implementation plan.

When finished, report exactly:

```text
Implemented:
- ...

Files changed:
- ...

Tests:
- ...

Known limitations:
- ...

Next recommended item:
- FOUND-002 — Core Contracts
```

If a requirement conflicts with the existing repository, stop changing code, explain the conflict and choose the smallest architecture-preserving solution.
