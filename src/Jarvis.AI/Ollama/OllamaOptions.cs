namespace Jarvis.AI.Ollama;

public sealed class OllamaOptions
{
    public const string SectionName = "Jarvis:Llm";

    public Uri BaseUrl { get; init; } = new("http://localhost:11434", UriKind.Absolute);

    public string Model { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 120;

    public bool HasValidLocalBaseUrl()
    {
        if (BaseUrl is null
            || !BaseUrl.IsAbsoluteUri
            || !BaseUrl.IsLoopback
            || (BaseUrl.Scheme != Uri.UriSchemeHttp && BaseUrl.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(BaseUrl.UserInfo)
            || !string.IsNullOrEmpty(BaseUrl.Query)
            || !string.IsNullOrEmpty(BaseUrl.Fragment))
        {
            return false;
        }

        return BaseUrl.AbsolutePath is "" or "/";
    }

    public Uri GetNormalizedBaseUrl() =>
        new($"{BaseUrl.GetLeftPart(UriPartial.Authority)}/", UriKind.Absolute);
}
