# Definition of Done

A backlog item is complete only when all applicable conditions are met.

## Required

- Scope matches the active backlog item.
- Solution builds.
- Relevant automated tests pass.
- New business/security behavior has tests.
- No secrets or machine-specific paths are committed.
- Cancellation is supported for long-running I/O where applicable.
- Error paths return/log meaningful failures.
- No unrelated refactor is included.
- Documentation is updated if behavior/architecture changed.
- Diff has been reviewed.
- No TODO is left for work that belongs to the same backlog item.

## For tools

Additionally:

- tool has stable name,
- arguments are validated,
- risk level is declared by trusted code,
- permission pipeline is used,
- result is structured,
- failure behavior is tested,
- no arbitrary shell injection path is introduced.

## For providers

Additionally:

- provider is behind an interface,
- configuration is typed,
- provider unavailability is handled,
- timeout/cancellation is handled,
- implementation DTOs do not leak into Core.

## For UI

Additionally:

- business logic is not embedded in code-behind,
- error state is visible,
- long-running operations do not freeze the UI.

## Completion report

Codex should finish each task with:

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
- ...
```
