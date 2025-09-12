// csharp
// File: Assets/Ariko/Editor/KnownModels.cs

using System.Collections.Generic;

public static class KnownModels
{
    public static readonly List<string> OpenAI = new()
    {
        "gpt-4.1-nano",
        "gpt-4.1-mini",
        "gpt-4.1",
        "codex",
        "gpt-4o",
        "gpt-5-nano",
        "gpt-5-mini",
        "gpt-5"
    };

    public static readonly List<string> Google = new()
    {
        "models/gemini-2.5-pro",
        "models/gemini-2.5-flash"
    };
}
