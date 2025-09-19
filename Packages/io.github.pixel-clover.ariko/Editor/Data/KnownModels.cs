using System.Collections.Generic;

/// <summary>
///     Contains lists of known, supported model names for each AI provider.
///     This is used to filter the full list of models returned by the APIs.
/// </summary>
public static class KnownModels
{
    /// <summary>
    ///     A list of known model names for the OpenAI provider.
    /// </summary>
    public static readonly List<string> OpenAI = new()
    {
        "gpt-4.1-nano",
        "gpt-4.1-mini",
        "gpt-4.1",
        "gpt-4o",
        "gpt-o3-mini",
        "gpt-o4-mini",
        "gpt-5-nano",
        "gpt-5-mini",
        "gpt-5"
    };

    /// <summary>
    ///     A list of known model names for the Google provider.
    /// </summary>
    public static readonly List<string> Google = new()
    {
        "models/gemini-2.5-pro",
        "models/gemini-2.5-flash"
    };
}
