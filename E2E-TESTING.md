# VS Extension E2E Tests

The e2e suite lives in `Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022.E2ETests` and drives a real Visual Studio 2022 experimental instance against the built VSIX.

## Runner Requirements

- Windows self-hosted GitHub Actions runner with `self-hosted`, `windows`, and `x64` labels.
- Visual Studio 2022 `17.12+` installed. Launch it at least once on the runner so `%LocalAppData%\Microsoft\VisualStudio\` contains a `17.0_*` instance folder (used to build the experimental hive path `17.0_<id>Exp`).
- Interactive desktop session available for UI automation.
- WebView2 runtime installed.
- Git on `PATH` (the harness runs `git init` and an initial commit in each temp workspace).
- Access to `CODESCENE_IDE_DOCS_AND_WEBVIEW_TOKEN`.

Optional overrides:

- `DEVENV_EXE` to point at a specific `devenv.exe` (should match the installation whose hive under `%LocalAppData%\Microsoft\VisualStudio\` you intend to use when multiple VS 2022 installs exist).

## Local Execution

Build the extension assets first, then run:

```powershell
make e2e
```

The runner script sets `CODESCENE_E2E=true`, builds against `Release`, deploys the generated VSIX into the experimental instance, and writes artifacts under `Codescene.VSExtension.VS2022/TestResults/E2E`.

## VSIX deployment (experimental instance)

The harness does **not** use `VSIXInstaller.exe /quiet` for automation: that mode is unreliable here (for example failures involving non-seekable streams), so silent installs are not used.

Unzipping the `.vsix` under `Extensions\` alone is not enough: Visual Studio must still **merge `.pkgdef`** into the experimental configuration. Without that, CodeScene can show as **installed but disabled** and packages will not load.

**Steps the harness takes:**

1. Clear experimental hives (`*Exp` under `%LocalAppData%` and `%AppData%` … `\Microsoft\VisualStudio\`).
2. Extract the `.vsix` into `%LocalAppData%\Microsoft\VisualStudio\17.0_<id>Exp\Extensions\<Publisher>\<IdentityId>\<Version>\` (from `extension.vsixmanifest` inside the package).
3. Touch `extensions.configurationchanged` under that hive’s `Extensions` folder (same signal the installer uses to force a refresh).
4. Run `devenv /rootSuffix Exp /updateconfiguration`, then delete that hive’s `ComponentModelCache` folder when it exists so the next IDE start rebuilds MEF state. **`devenv /rootSuffix Exp /setup` is not used:** it requires elevation (administrator) and fails under a normal test account with a UAC-style error.

If more than one `17.0_*` (non-`Exp`) folder exists, the newest by last write time is chosen. Set `DEVENV_EXE` when that does not match the Visual Studio build you want to test.

## GitHub Copilot onboarding dialog

If **GitHub Copilot** is installed on the runner, Visual Studio may show a **“GitHub Copilot Free”** modal on startup. That blocks other UI automation.

**In the harness (default):** After the main window appears, the e2e host tries to dismiss it by activating **“Maybe later”** (invoke or click) or closing a top-level window whose title contains **“Copilot”** via the **Close** button. This is best-effort and depends on UI text staying stable.

To turn that off (for example while debugging the dialog itself), set:

- `CODESCENE_E2E_SKIP_COPILOT_DISMISS=1`

**On the machine (most reliable):** Use [Visual Studio Administrative Templates](https://learn.microsoft.com/en-us/visualstudio/ide/visual-studio-github-copilot-admin?view=visualstudio) (**Computer Configuration → Administrative Templates → Visual Studio → Copilot Settings**) to disable Copilot for the whole box or for **Copilot Free** (supported from VS 17.13+). That avoids the splash entirely and does not rely on UI automation.

## Test workspaces and scenarios

Visual Studio is started with a solution path so the main IDE opens instead of the start window.

- Scenario templates live under `Codescene.VSExtension.VS2022/Codescene.VSExtension.VS2022.E2ETests/TestAssets/<ScenarioName>/`. Each scenario folder must contain at least one `.sln` at its root.
- At run time, the harness copies the template into a unique directory under `%TEMP%\CodesceneE2E\`, runs `git init`, configures a local user, and creates an initial commit.
- Add new scenarios by creating a new folder under `TestAssets` (for example `TestAssets/LargeSolution/`) and passing that name into `VisualStudioTestHost.Start("LargeSolution")` from a test class.

## GitHub Actions

Use `.github/workflows/e2e.yml` to run the suite on a self-hosted runner. The workflow is `workflow_dispatch` only while the lane is stabilizing.

Artifacts uploaded on every run:

- `*.trx`
- `Codescene.VSExtension.VS2022/TestResults/E2E/**`

## Promotion Criteria

Keep the workflow manual until these are true for several consecutive runs on the dedicated runner:

- VSIX deployment (extract + `devenv /updateconfiguration`) and experimental-instance reset succeed without operator intervention.
- The three smoke tests pass without rerun.
- Failure artifacts are sufficient to diagnose flaky UI failures.

After that, add `pull_request` as a trigger and decide whether the workflow should become a required check.
