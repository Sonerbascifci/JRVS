namespace Jarvis.Core.Assistant;

public enum AssistantState
{
    Idle,
    Awakened,
    Listening,
    Processing,
    AwaitingConfirmation,
    ExecutingTool,
    Speaking,
    Faulted
}
