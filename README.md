# Status of the project
The solution was developed over approximately 45 days by two developers, Amin Ovčina and Emir Prljača. 
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
Amina

### File Downloader
Amina

### Global Error Handling
To enable centralized error handling, we created a global error handler located in
***CodesceneReeinventTest/CodesceneReeinventTest/Application/ErrorHandling/ErrorsHandler.cs.***
Currently, it logs information in the Output Window, but once the extension is in production, it should be integrated with a well-known analytics service such as Application Insights, Raygun, or Google Analytics.

### Code lense
Amina

### Supported type of issues (smells)
Amina


### Status window
Amina