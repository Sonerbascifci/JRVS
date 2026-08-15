namespace Jarvis.Core.Audio;

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

public sealed record AudioInput
{
    public AudioInput(ReadOnlyMemory<byte> data, string format)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("Audio data cannot be empty.", nameof(data));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        Data = data;
        Format = format;
    }

    public ReadOnlyMemory<byte> Data { get; }

    public string Format { get; }
}

public sealed record SpeechRecognitionResult
{
    public SpeechRecognitionResult(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
    }

    public string Text { get; }
}
