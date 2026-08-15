# Tool System

## 1. Purpose

Tools are the only approved bridge between natural-language reasoning and real computer actions.

The LLM cannot call the operating system directly.

## 2. Core model

Conceptual contract:

```csharp
public interface IJarvisTool
{
    ToolDescriptor Descriptor { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}
```

Possible descriptor:

```csharp
public sealed record ToolDescriptor(
    string Name,
    string Description,
    ToolRiskLevel RiskLevel,
    Type ArgumentsType);
```

The exact implementation may evolve.

## 3. Naming

Use stable snake_case machine names.

Examples:

```text
open_application
open_url
get_system_status
find_file
open_file
open_project
git_status
```

Do not rename tools casually once prompts/tests depend on them.

## 4. Execution pipeline

All invocation paths must converge here:

```text
Input
  |
  v
Resolve Tool
  |
  v
Deserialize + Validate Arguments
  |
  v
Permission Evaluation
  |
  +---- denied ----------> Result: Denied
  |
  +---- confirmation ----> Confirmation Service
  |                            |
  |                         approved?
  |                            |
  +----------------------------+
  |
  v
Execute Tool
  |
  v
Structured Result
  |
  v
Conversation Response
```

The deterministic intent router and LLM tool-calling loop must use the same pipeline.

## 5. Arguments

Use typed argument objects.

Example:

```csharp
public sealed record OpenApplicationArguments(string ApplicationName);
```

Avoid dictionaries throughout the application after the provider boundary.

Validation examples:
- required values,
- length limits,
- enum validation,
- normalized file paths,
- allowed URL schemes.

## 6. Results

A tool should return structured information.

Example:

```csharp
public sealed record ToolExecutionResult(
    bool Success,
    string? UserMessage,
    string? ErrorCode,
    object? Data);
```

Do not return only human prose when structured data is available.

Example `git_status` result might contain:
- branch,
- staged count,
- modified count,
- untracked count,
- clean flag.

The conversational layer decides how to verbalize it.

## 7. Error model

Expected categories:

```text
InvalidArguments
PermissionDenied
ConfirmationRequired
Cancelled
NotFound
Unavailable
ExecutionFailed
Timeout
Unsupported
```

Provider failures and tool failures should be distinguishable.

## 8. Initial tools

### open_application
Risk: Safe

Input:
- application name or known application alias.

Rules:
- resolve only known/installed applications.
- do not interpret the argument as a shell command.

### open_url
Risk: Safe

Input:
- URL.

Rules:
- allow only `http` and `https` initially.

### get_system_status
Risk: Safe

Output:
- CPU usage,
- memory usage,
- disk usage.

### find_file
Risk: Safe

Input:
- query,
- optional allowed root.

Rules:
- read/search only.
- apply result limits.

### open_file
Risk: Safe or Confirm depending on future semantics.

For v0.1:
- open using associated application only.
- no modification.

### open_project
Risk: Safe

Input:
- project alias/path.

Rules:
- project aliases may later come from memory/settings.

### git_status
Risk: Safe

Read-only.

Do not generalize into arbitrary Git execution.

## 9. Tool registry

Registry responsibilities:

- register tools,
- enforce unique names,
- expose descriptors,
- resolve by name,
- expose schema metadata to LLM adapters.

The registry does not decide permissions.

Initial registry behavior:

- tools are registered explicitly through the composition root,
- names use ordinal, case-sensitive comparison,
- registrations and descriptors are captured as an immutable construction-time snapshot,
- resolving a tool does not authorize or execute it.

## 10. Permission evaluator

`IPermissionEvaluator` determines required behavior from trusted tool metadata and application policy.

The LLM must never provide:

```text
riskLevel
requiresConfirmation
permissionOverride
```

as authoritative inputs.

## 11. Confirmation

Confirmation UI should summarize:

- action,
- important arguments,
- expected impact.

Example:

```text
JARVIS wants to overwrite:
C:\Projects\Example\settings.json

Allow once?
```

Do not use vague prompts like:

```text
Continue?
```

for meaningful changes.

## 12. Tool-call loop

Pseudo-flow:

```text
LLM response
 -> contains tool calls?
    no -> final response
    yes
      -> validate max step count
      -> execute each allowed call safely
      -> append structured results
      -> request next LLM turn
```

Tool execution results, not model assumptions, are the source of truth.

## 13. Future tool packages

Potential future packages:

```text
Jarvis.Tools.Calendar
Jarvis.Tools.Email
Jarvis.Tools.HomeAssistant
Jarvis.Tools.Browser
Jarvis.Tools.Media
```

Each should remain separately testable.
