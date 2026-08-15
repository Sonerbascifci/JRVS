# Development Standards

## 1. General

Primary language: C#.

Target: .NET 10.

UI: WPF.

Use modern C# features when they improve clarity, not novelty.

## 2. Build settings

Expected:
- nullable enabled,
- implicit usings enabled where appropriate,
- warnings treated seriously,
- analyzers added only when useful and maintained.

Prefer a shared `Directory.Build.props`.

## 3. Naming

Follow normal .NET conventions.

Examples:

```text
ILlmProvider
OllamaLlmProvider
ToolExecutionResult
OpenApplicationTool
PermissionDecision
AssistantState
```

Machine-readable tool names use snake_case.

## 4. Async

Any I/O-bound or long-running operation should be asynchronous.

Accept `CancellationToken` for:
- AI calls,
- audio operations,
- tool execution,
- persistence,
- external process waits.

Do not use `.Result` or `.Wait()` in async flows.

## 5. Time

Use `DateTimeOffset` for persisted timestamps.

Avoid assuming system local time inside Core.

## 6. Configuration

Use strongly typed options.

Example domains:

```text
JarvisOptions
OllamaOptions
AudioOptions
PersistenceOptions
SecurityOptions
```

Validate configuration at startup where useful.

Never hard-code:
- user directories,
- executable paths,
- model paths,
- secrets.

## 7. Dependency injection

Use .NET Generic Host and constructor injection.

Avoid:
- service locator,
- global mutable state,
- static singleton accessors.

## 8. Error handling

Expected failures should become meaningful result/error types where they are part of normal behavior.

Unexpected exceptions:
- log once at the appropriate boundary,
- preserve stack information,
- surface a safe user-facing message.

Do not:

```csharp
catch
{
}
```

## 9. Logging

Use structured logging.

Good:

```csharp
logger.LogInformation(
    "Tool {ToolName} completed in {ElapsedMs}ms",
    toolName,
    elapsedMs);
```

Avoid logging sensitive arguments by default.

## 10. Processes

When starting external applications/processes:
- avoid shell interpretation unless required,
- validate executable/arguments,
- capture exit codes where relevant,
- support cancellation when waiting,
- set timeouts for helper processes.

## 11. Persistence

Core defines repository abstractions.

Persistence implements them.

EF entities should not leak throughout the application.

Migrations belong to the Persistence project or an intentionally selected migrations assembly.

## 12. UI

WPF UI should be thin.

ViewModels may coordinate presentation state but should not:
- call Ollama directly,
- manipulate SQLite directly,
- execute Windows tools directly.

Use application services/orchestrators.

## 13. Comments

Comment:
- non-obvious constraints,
- security rationale,
- interoperability quirks.

Do not comment obvious syntax.

## 14. Scope control

Do not refactor unrelated code during feature work.

If a prerequisite defect is discovered:
- fix only what is necessary,
- document the reason.

## 15. External packages

Before adding a dependency:
- confirm it is maintained,
- check license,
- check whether BCL/.NET already solves the need,
- avoid packages for trivial helpers.

Keep dependency count intentionally low.
