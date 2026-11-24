# CodeScene - Code Analysis and Refactoring
[![CodeScene Average Code Health](https://codescene.io/projects/66574/status-badges/average-code-health)](https://codescene.io/projects/66574)

## What is CodeScene for Visual Studio? ##
[CodeScene](http://www.codescene.com) is the only code analysis tool with a **proven business impact**. It serves as a **safeguard** against introducing code changes that could negatively affect your business outcomes.

CodeScene promotes **healthy, maintainable code** by providing clear, actionable insights and guidance on how to improve your codebase.

With the bundled **CodeScene Refactoring Agent**, CodeScene can even automate parts of the improvement process—helping developers enhance code quality and maintainability faster and more accurately.

By using CodeScene, developers can spend less time deciphering and refactoring code, and more time focusing on what truly matters: **solving problems and delivering value**.

## The CodeScene CodeHealth™ metric ##
CodeScene’s [CodeHealth™](https://codescene.io/docs/guides/technical/code-health.html) metric is the software industry’s only code-level metric with proven business impact, measured through fact-based, winning research. It’s a metric that you can trust. The extension analyses and scores your code as you type, and adds diagnostic items that highlights any [code smells](#code-smells).
CodeScene supports [most popular languages](https://codescene.io/docs/usage/language-support.html#supported-programming-languages).

## Safeguarding your code changes ##
Instant feedback is vital for maintaining high-quality code during development. By analyzing code as it’s written, developers may identify issues immediately. This interactive feedback loop encourages better coding habits, speeds up learning, and reduces the need for extensive rework later. 

Continuous feedback fosters a sense of flow and confidence, as you instantly can see the impact of your changes on overall code health. In short, interactive monitoring turns code quality from a delayed review process into a **continuous, integrated part of writing code**, ensuring long-term maintainability and faster, safer development.

The **Code Health Monitor** flags for drops in code health in real time and offers instant recommendations to keep your code maintainable.

<img style="margin: 10px 10px 10px 10px;" src="screenshots/monitor.gif" width="500">

> **_NOTE:_** _The Code Health Monitor is currently available to all users for a limited time period. However, this capability will become accessible only to CodeScene customers in future_.

## Automatic Refactoring with CodeScene Refactoring Agent ##
The [CodeScene Refactoring Agent](https://codescene.io/docs/auto-refactor/index.html) helps you with the hardest part of software development: maintaining and improving existing code. While other AI tools focus on code generation, the Refactoring Agent fixes technical debt and refactors code smells directly in your IDE. With fact-based metrics, the agent focuses on the impactful improvements you can do now to simplify your tasks.

The extension bundles a local Refactoring Agent (version 1.1.13) that runs on your machine with your own LLM provider key. Sign in to CodeScene with your personal access token or browser-based authentication, then set your provider key in **Tools > Options > CodeScene > General > Refactoring Agent**. Test your setup with **Extensions > CodeScene > Test provider connectivity**.

<img style="margin: 10px 10px 10px 10px;" src="screenshots/auto-refactor-showcase.gif" width="1000">

> **_NOTE:_** _The Refactoring Agent is an AI-powered feature that requires your own API key from a supported LLM provider (Anthropic, OpenAI, or Google). The agent runs locally on your machine and communicates directly with your chosen provider. CodeScene does not host or provide LLM services_.

## Overview of Available Features ##
| Feature | Description | What it looks like |
|---------|-------------|--------------------|
| **Code Health Monitor*** | The Code Health Monitor continuously tracks changes in your code, highlighting any improvements or degradations you introduce. Each file displays both its previous and current Code Health score, along with a clear delta value showing the overall change. You can easily see the impact of your modifications at the file or function level, including the status of any associated Code Smells—whether you’ve introduced new ones or resolved existing issues. With this level of visibility, there’s no longer any excuse for allowing Code Health to decline in your codebase. | <img style="margin: 10px 10px 10px 10px;" src="screenshots/monitor.png" alt="Monitor" width="2000px"/> |
| **Automatic Refactoring**** | <div>The CodeScene Refactoring Agent enables automatic refactoring within the extension. Code Smells detected during code analysis can be flagged for automatic refactoring, and any changes identified by the Code Health Monitor that have refactoring potential are also considered. The agent uses your configured LLM model and validates the results to ensure that only relevant and high-quality refactorings are presented. Each suggested refactoring includes a confidence evaluation and highlights any potential concerns, giving developers clear guidance and control over improvements to their code.</div><p></p><div>The agent is offered for every Code Smell in every language the extension reviews.</div> | <img style="margin: 10px 10px 10px 10px;" src="screenshots/ace.png" alt="Refactoring Agent" width="2000px"/> |
| **Inline Code Smell detection** | Code smells often lead to issues such as increased technical debt, more bugs, and reduced overall quality of the software. You can find detailed information for each code smell by either clicking the corresponding inline action notation in the editor, by examining the diagnostics (squigglies or in the Problems view), or by using the Quick Fix action menu (light bulb).| <img style="margin: 10px 10px 10px 10px;" src="screenshots/codesmells.png" alt="Code Smells" width="2000px"/> |
| **Refactoring Guidance** | Our mission is to educate and raise awareness about Code Health and its impact on your efficiency as a developer. The CodeScene extension equips you with rich insights into Code Smells and provides clear, actionable guidance on how to address issues that may exist in your codebase. We include relevant examples that illustrate the essence of Code Smells, along with common patterns and practical solutions to help you write cleaner, more maintainable code. | <img style="margin: 10px 10px 10px 10px;" src="screenshots/documentation.png" alt="Code Smell Documentation" width="2000px"/> |
| **Error List View** | When a file is opened in the editor, it is instantly scanned for existing Code Health issues. All discovered issues are then listed as warnings in the IDE Error List View. This way you instantly get an overview of all opportunities a file has for improvements. | <img style="margin: 10px 10px 10px 10px;" src="screenshots/problems.png" alt="Problem View" width="2000px"/> |
| **Custom Code Health rules** | To customize the code analysis you can either use local [Code Comment Directives](https://codescene.io/docs/guides/technical/code-health.html#disable-local-smells-via-code-comment-directives) or create a `code-health-rules.json` file which applies to the entire project. | <img style="margin: 10px 10px 10px 10px;" src="screenshots/custom_directive.png" alt="ACR" width="2000px"/> |

_* Available time-limited for non CodeScene customers._

_** Paid add-on feature._

## Configuration ##

You can add configuration files to the git repository to customize and control behavior in the Extension. They should be located in a `.codescene` folder, at the repository root. In addition, we also have a Settings page for personalized configuration of the extension.

### code-health-rules.json ###

Location: `.codescene/code-health-rules.json`

You can find more documentation on its content [here](https://codescene.io/docs/guides/technical/code-health.html#customize-the-code-health-rules-via-json)

### config.json ###

Location: `.codescene/config.json`

The bundled CLI (since 0.9.0) picks the Git merge base used for delta analysis. See [Baseline resolution](docs/baseline-resolution.md) for the full algorithm, examples, and troubleshooting.

Example configuration:
```json
{
  "baseline_branch": "develop"
}
```
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| **baseline_branch** | String | Automatic (see [Baseline resolution](docs/baseline-resolution.md)) | Branch name used to resolve the merge base with `HEAD` (remote `origin/<branch>` preferred over local). Replaces the default ref list entirely when set.

### Settings ###

Location: `Code Health Monitor` > `Extension Settings`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| **Auth Token** | String | - | Personal access token — an alternative to signing in. Also used for refactoring. The token is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings. |
| **Show Debug Logs** | Boolean | false | Enable/Disable debug logging in the CodeScene log file, and Output window. |

Location: `Refactoring Agent` > `Extension Settings`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| **Enable Refactoring Agent** | Boolean | false | Enable CodeScene Refactoring Agent |
| **Agent Model** | String | anthropic/claude-sonnet-5-5 | Model to use for the Refactoring Agent. |
| **Agent Model Override** | String | - | Custom model identifier. If set, overrides the dropdown selection above. |
| **Amazon Bedrock Profile** | String | - | AWS profile name for Amazon Bedrock. Check the setup with Extensions > CodeScene > Test provider connectivity. |
| **Amazon Bedrock Region** | String | - | AWS region for Amazon Bedrock. Check the setup with Extensions > CodeScene > Test provider connectivity. |
| **Anthropic API Key** | String | - | API key for Anthropic models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings. |
| **OpenAI API Key** | String | - | API key for OpenAI models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings. |
| **Google API Key** | String | - | API key for Google (Gemini) models. The key is encrypted with Windows DPAPI for the current user before being stored in Visual Studio settings. |

### Telemetry ###

We collect anonymized telemetry for tracking usage and feature engagement. CodeScene adheres to Visual Studio's privacy settings to enable/disable this.

## Release notes ##

See the [GitHub Releases](https://github.com/codescene-oss/codescene-vs/releases) page for release notes and version history.
