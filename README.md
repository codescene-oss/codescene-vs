# Status of the project
The solution was developed over approximately 6 weeks by two developers, Amina Ovčina and Emir Prljača. 
Our primary source of information for requirements was the existing and released VS Code extensions. We aimed to replicate as many functionalities and features as possible.

We followed the same approach that the VS Code extension uses with the CS executable file, which involves:

* Downloading the executable file from the Codescene server
* Using the executable file to review code files


## Visual studio supported versions
Currently, we support only Visual Studio 2022. The code project responsible for this version is CodesceneReeinventTest. 
If support is required for earlier versions, such as Visual Studio 2019 or 2017, we would need to create a new project with version-specific referenced libraries and packages. 
All projects are implemented on .NET version 4.8.

## Project solution structure
The solution consists of five projects, organized following a clean architecture approach:

* Core – Contains the main domain logic.
* CodesceneReeinventTest – Contains logic specific to Visual Studio 2022.
* CodescenCodeLensShared – The top-level project that supports CodeLens functionality.
* CodeLensProvider – A CodeLens project tailored for Visual Studio 2022.
* CredentialManagerPersistenceAuthProvider – An authentication persistence provider, implemented using Windows Credential Manager.

## Completed functionalities
The following sections outline the status of the major functional components.

### Authentication
The main logic for this functionality is located in
***CodesceneReeinventTest/Core/Application/Services/Authentication/AuthenticationService.cs.***
To make this feature fully functional, support from the Codescene team is required. 
They need to implement a redirect mechanism that accepts our custom redirect URL and returns a URL containing authentication data in the query string.

Once the user completes the authentication process, we need to store the secret data to enable seamless access the next time they use the extension. 
Currently, this is managed with Windows Credential Manager, though it can be easily swapped out for any other provider. This only requires implementing the interface
***CodesceneReeinventTest/Core/Application/Services/Authentication/IPersistenceAuthDataProvider.cs.***

### File review
File review involves monitoring document events. When a file is opened, its review is triggered and cached until the file is closed. 
Additionally, a file review is performed whenever the document is saved. 
For complete functionality, file review is has to be triggered 3 seconds after the last modification, following the logic in the Visual Studio Code extension which is not yet done.

### File Downloader
In the status window, there is a button to manually initiate the download, extraction, and renaming of a zip file, similar to the Visual Studio Code extension. 
For production, this process should be automated upon extension load.

### Global Error Handling
To enable centralized error handling, we created a global error handler located in
***CodesceneReeinventTest/CodesceneReeinventTest/Application/ErrorHandling/ErrorsHandler.cs.***
Currently, it logs information in the Output Window, but once the extension is in production, it should be integrated with a well-known analytics service such as Application Insights, Raygun, or Google Analytics.

### CodeLens
CodeLens functionality is implemented and displays above methods and types. 
It does not currently appear above specific lines within methods due to IDE limitations. 
The production version will include automatic refreshing of CodeLens indicators.

### Underline
An underline tagger has been implemented as in the Visual Studio Code extension. 
Currently, it refreshes on every change instead of using debouncing, as this implementation was a preliminary version and not intended for production.

### Supported type of issues (smells)
Based on the Visual Studio Code extension, there are three types of code smells: file-level, function-level, and expression-level. 
Currently supported code smells are: Complex Conditional, Code Health Score, Brain Method, Bumpy Road Ahead, Code Duplication, Complex Method, Deep Nested Complexity, Excessive Function Arguments, and Large Method. 
Additional providers can be added in the future by following the same creation pattern.

### Status window
A menu item, CodeScene, has been added under the Extensions menu in the IDE, providing access to options like Sign In, Options, and Status Window—all displaying the same data as the status window in the Visual Studio Code extension.
