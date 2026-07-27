# port

Port a commit from ../codescene-vscode to this Visual Studio extension project.

## Arguments

- Commit SHA (required)

## Instructions

1. Run `git -C ../codescene-vscode show <sha>` to see the commit
2. Port the changes faithfully while:
   - Adding good test coverage
   - Respecting VSSDK threading rules
   - Honoring this project's customs (see CLAUDE.md)
