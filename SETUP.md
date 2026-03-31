## Table of contents

- [Getting started](#getting-started)
- [Webview Framework (CWF) management](#webview-framework-cwf-management)
- [Feature Flags](#feature-flags)
- [License](#license)

## Getting started

## Webview Framework (CWF) management

The Centralized Webviews Framework (CWF) used by the extension, which provides shared webview components across all supported IDEs, is maintained in a private repository.

**Note:** Access to the CWF repository requires appropriate permissions.

## Feature Flags

This plugin supports simple feature flags that can be controlled at build time using environment variables. 
Feature flags allow you to enable or disable experimental functionality without modifying the source code.

### How feature flags work

Feature flags are defined in `Codescene.VSExtension.VS2022.csproj` using environment variables and compilation symbols, for example:

Example:
```
<EnableAceRefactoring Condition="'$(FEATURE_ACE)' == 'true'">true</EnableAceRefactoring>
<EnableAceRefactoring Condition="'$(FEATURE_ACE)' != 'true'">false</EnableAceRefactoring>
...
<!-- Add conditional compilation symbol for features -->
<PropertyGroup>
  <DefineConstants Condition="'$(EnableAceRefactoring)' == 'true'">$(DefineConstants);FEATURE_ACE</DefineConstants>
</PropertyGroup>
```

During the build, compilation symbols (using same names as env variables) are being created with appropriate value according to env variable.
They are then used in the codebase to conditionally include or exclude feature-specific code blocks, for example:

```
#if FEATURE_ACE
                    await AceUtils.CheckContainsRefactorableFunctionsAsync(result, code);
#endif
```

### Using feature flags when building the plugin locally

To test feature flags locally set the corresponding env variable before building the extension.
Also make sure to close the IDE before setting the env variable to ensure that the build process picks up the new value.


If a property is not provided, it defaults to false.

| Flag name                    | Description                                                                                            |
|------------------------------|--------------------------------------------------------------------------------------------------------|
| FEATURE_PERIODIC_GIT_SCAN    | Enables detailed logging for periodic git status scanning. Disabled by default.                        |
| FEATURE_INITIAL_GIT_OBSERVER | Enables detailed logging for initial GitChangeObserver invocation on startup. Disabled by default.     |

## License

See LICENSE file.
