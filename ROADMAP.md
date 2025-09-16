## Feature Roadmap

This document includes the roadmap for the Ariko project.
It outlines the features to be implemented and their current status.

> [!IMPORTANT]
> This roadmap is a work in progress and is subject to change without notice.

### Current Features

-   **Interactive Chat:** A dedicated editor window for conversing with the AI.
-   **Multiple AI Providers:** Support for OpenAI, Google, and local Ollama models.
-   **Context-Aware Help:**
    -   Right-click on a Component in the Inspector to ask for an explanation.
    -   Right-click on a console error to ask for an explanation.
    -   Automatically includes the currently selected asset as context for your questions.
-   **Agent Mode:** An experimental mode where the AI can execute actions in the editor (e.g., creating a GameObject).
-   **Chat History:** Sessions are saved and can be revisited later.
-   **Syntax Highlighting:** Code blocks in the chat are highlighted for readability.
-   **Customizable UI:** Change chat colors and fonts via the settings asset.

### Planned Features

-   **Code Generation and Modification:**
    -   [ ] Generate new C# scripts from a prompt.
    -   [ ] Add new methods to existing scripts.
    -   [ ] Modify existing code based on user requests.
-   **Editor Integration:**
    -   [ ] Create and modify assets (materials, prefabs, etc.).
    -   [ ] More context menu integrations (e.g., "Ask Ariko to refactor").
    -   [ ] Deeper integration with the Unity Profiler and other editor windows.
-   **Chat Experience:**
    -   [ ] Streaming responses from the AI.
    -   [ ] Better handling of chat history (e.g., summarizing and renaming sessions).
    -   [ ] Support for image-based prompts.
-   **Core Improvements:**
    -   [ ] More robust error handling.
    -   [ ] Support for more AI providers.
    -   [ ] Improved test coverage.
