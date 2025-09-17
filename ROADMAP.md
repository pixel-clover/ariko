## Feature Roadmap

This document includes the roadmap for the Ariko project.
It outlines the features to be implemented and their current status.

> [!IMPORTANT]
> This roadmap is a work in progress and can change without notice.

### Features

-   **Chat and UI**
    -   `[x]` A dedicated editor window for conversing with the AI.
    -   `[x]` Sessions are saved and can be revisited later.
    -   `[x]` Code blocks in the chat are highlighted for readability.
    -   `[x]` Change chat colors and fonts via the settings asset.
    -   `[ ]` Streaming responses from the AI.
    -   `[ ]` Better handling of chat history (like summarizing and renaming sessions).
    -   `[ ]` Support for image-based prompts.

-   **Unity Editor Integration**
    -   `[x]` Right-click a Component in the Inspector to ask for an explanation.
    -   `[x]` Right-click a console error to ask for an explanation.
    -   `[x]` Automatically includes the currently selected asset as context.
    -   `[ ]` More context menu integrations (like "Ask Ariko to refactor").
    -   `[ ]` Deeper integration with the Unity Profiler and other editor windows.

-   **Agentic Features**
    -   `[x]` An experimental agent mode to execute actions in the editor.
    -   `[x]` Create and modify assets (materials, prefabs, GameObjects, etc.).
    -   `[x]` Generate new C# scripts from a prompt.
    -   `[x]` Add new methods to existing scripts.
    -   `[x]` Modify existing code based on user requests.

-   **Core Features**
    -   `[x]` Support for OpenAI, Google, and local models using Ollama.
    -   `[ ]` More robust error handling.
    -   `[ ]` Support for more AI providers.
    -   `[ ]` Improved test coverage.
