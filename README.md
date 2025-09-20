<div align="center">
  <picture>
    <img alt="Ariko Logo" src="logo.svg" height="20%" width="20%">
  </picture>
<br>

<h2>Ariko</h2>

[![Tests](https://img.shields.io/github/actions/workflow/status/pixel-clover/ariko/tests.yml?branch=main&label=tests&style=flat&labelColor=282c34&logo=github)](https://github.com/pixel-clover/ariko/actions/workflows/tests.yml)
[![Code Quality](https://img.shields.io/codefactor/grade/github/pixel-clover/ariko?style=flat&label=code%20quality&labelColor=333333&logo=codefactor&logoColor=white)](https://www.codefactor.io/repository/github/pixel-clover/ariko)
[![Unity Version](https://img.shields.io/badge/unity-2021.3+-green?style=flat&labelColor=282c34&logo=unity)](https://unity.com)
[![OpenUPM](https://img.shields.io/npm/v/io.github.pixel-clover.ariko?label=openupm&registry_uri=https://package.openupm.com&style=flat&labelColor=282c34)](https://openupm.com/packages/io.github.pixel-clover.ariko/)
[![License](https://img.shields.io/badge/License-MIT-007ec6?style=flat&labelColor=282c34&logo=open-source-initiative&label=license)](https://github.com/pixel-clover/ariko/blob/main/LICENSE)

A friendly AI assistant for Unity

</div>

---

Ariko is a friendly AI assistant for Unity game developers (with a user interface similar to GitHub Copilot).
It can help you with everyday tasks like generating C# code, answering questions about your Unity project, and more.

### Features

* **Interactive Chat:** A chat window where you can ask questions in natural language.
* **Context Awareness:** The assistant will know which asset, GameObject, or script you have selected to provide relevant help.
* **Code Generation:** Generate C# scripts, methods, or snippets based on your prompts.
* **Project Awareness:** Answer questions about your existing project.
* **Unity API Helper:** Explain how to use Unity components and functions with relevant examples.

See the [ROADMAP.md](ROADMAP.md) file for the project roadmap.

> [!IMPORTANT]
> This project is in early development, so bugs and breaking changes are expected.
> Please use the [issues page](https://github.com/pixel-clover/ariko/issues) to report bugs or request features.

---

### Installation

Ariko can be either installed from the [OpenUPM registry](https://openupm.com/packages/io.github.pixel-clover.ariko/)
or directly from the Git URL.

#### Install from OpenUPM

You can install Ariko using [OpenUPM-cli](https://github.com/openupm/openupm-cli) by running the following command:

```shell
openupm add io.github.pixel-clover.ariko
```

#### Install from Git URL

1.  In the Unity Editor, go to `Window > Package Manager`.
2.  Click the `+` icon in the top-left corner of the Package Manager window and select "Add package from git URL...".
3.  Paste the following URL and click "Add":
    ```
    https://github.com/pixel-clover/ariko.git?path=/Packages/io.github.pixel-clover.ariko
    ```
4.  When the package is installed, you can open the assistant window by going to `Tools > Ariko Assistant`.

### Configuring the Model Providers

To use Ariko, you need to provide an API key for the AI provider service you want to use (currently OpenAI and Google).
To do this, follow these steps:

1.  In the Ariko window, click the "Settings" button. This will open the settings panel.
2.  Enter API Keys:
    *   For better security, you can set your API keys as environment variables on your system, and Ariko will automatically load them.
        *   `OPENAI_API_KEY`: Your API key for OpenAI.
        *   `GOOGLE_API_KEY`: Your API key for Google.
        *   `OLLAMA_URL`: The URL for your local Ollama instance (default: `http://localhost:11434`).
    *   Alternatively, you can paste your keys directly into the corresponding fields in the settings panel.
3.  Click the "Save and Close" button to save your settings.

> [!NOTE]
> API keys are not stored between sessions due to security reasons.
> You will need to re-enter them each time you restart Unity unless you set them as environment variables.

### Work Modes

Ariko can operate in two modes, which you can switch between at the top of the window:

-   **Ask Mode**: A standard question-and-answer chat mode. Ariko will use its knowledge and the context you provide to answer your questions.
-   **Agent Mode**: An mode where Ariko can perform actions in the editor, like creating, deleting, or modifying files and GameObjects.

> [!NOTE]
> Agent mode is currently experimental and may be buggy.

---

### Contributing

Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details on how to get started.

### Supporting the Project

If you find this project useful and want to support its development,
please consider supporting it by making a donation via
[GitHub Sponsors](https://github.com/sponsors/habedi) and giving it a star on GitHub.
Thank you!

### License

Ariko is licensed under the MIT License (see [LICENSE](LICENSE)).

### Acknowledgements

- The project logo is from [SVG Repo](https://www.svgrepo.com/svg/125334/reload) with some modifications.
