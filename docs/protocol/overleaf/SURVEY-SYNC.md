# Survey inventory: Overleaf sync

Project: https://www.overleaf.com/project/6a84bc097073254fbca6985b

## Current state (2026-09-27)

`survey-instruments.tex` was uploaded through the signed-in browser and renders in Overleaf. The researcher completed the Git clone at `/Users/pedro.poveda/Overleaf/gaze-contingency-study`. Its `main` branch tracks `origin/main`, the checkout is clean, and the survey file exactly matches the Unity repository copy. No automatic synchronization is running.

Last verified common baseline:

- Overleaf clone commit: `abaace768eda7dd9fddb2bf572066c7cd099983c`
- Survey SHA-256: `485a01778dd45864d8a85602371717778aca0c90500bd520ed74b1efdb9d5194`
- Git authentication was verified by a successful fetch; the IMI rationale update was pushed successfully to Overleaf. The dedicated checkout and Unity survey copy match.

## One-time local authentication

In Overleaf Account settings, under **Git integration → Your Git authentication tokens**, generate a token yourself. Do not paste it into chat or put it in a command, file, or remote URL.

Run in a local terminal:

```sh
git -c credential.helper= clone https://git@git.overleaf.com/6a84bc097073254fbca6985b /Users/pedro.poveda/Overleaf/gaze-contingency-study
```

Enter the token at the password prompt. The empty credential-helper override prevents this setup command from saving the token. Authentication for later fetch/push commands still needs a local token prompt or a credential helper separately configured by the user.

The clone is already complete; do not clone again. If the researcher wants future agent-driven sync without re-entering the token, they can run the following locally and enter the token at the password prompt. This explicitly uses macOS Keychain to store the credential after successful authentication:

```sh
git -C /Users/pedro.poveda/Overleaf/gaze-contingency-study -c credential.helper= -c credential.helper=osxkeychain fetch origin
```

The normal Git configuration already uses `osxkeychain`; no global configuration change is needed. Otherwise, keep credentials unsaved and run authenticated fetch/push steps interactively each time. Do not ask for the token in chat.

## File mapping

- Overleaf and dedicated Git checkout: `survey-instruments.tex` at the project root.
- Unity repository copy: `/tmp/gaze-v2-project-update-20260922/docs/protocol/overleaf/survey-instruments.tex`.

The dedicated checkout contains the other Overleaf documents too. Only the survey inventory is in scope for this sync setup; preserve unrelated files and collaborator edits.

## On-demand workflow

Ask Codex to **pull survey edits from Overleaf** or **push the survey edits to Overleaf**. Before either direction:

1. Fetch the current Overleaf revision in the dedicated checkout.
2. Inspect local changes before pulling. Use fast-forward-only updates when clean; if both copies changed, compare against the last synchronized revision and merge explicitly.
3. Compile the merged survey source before publishing it.
4. For upload, commit only `survey-instruments.tex` and push without force. A rejected push means fetch and reconcile, not overwrite.
5. For download, copy the reconciled file back to the mapped Unity repository path and compile it in the existing editor. Do not reset or switch the Unity branch.
6. Record the synchronized Overleaf commit and keep both survey copies identical. Never mark sync complete without verifying the remote file.

The common baseline above permits a three-way comparison: the Unity repository copy, the updated Overleaf copy, and `git show abaace768eda7dd9fddb2bf572066c7cd099983c:survey-instruments.tex`. Advance the recorded baseline only after both copies and the published Overleaf revision agree. Do not treat the older Overleaf run sheet as an instruction to overwrite the actively edited local run sheet.

## Full question companion (2026-09-27)

- Added `survey-questions-by-block.tex` to the same Overleaf project; source is standalone and compiled successfully.
- Maps Overleaf root `survey-questions-by-block.tex` to Unity `docs/protocol/overleaf/survey-questions-by-block.tex`.
- Published and verified remote commit: `476eed1c5bb5e2cac24396e85c00ce90d046b0fa`.
- Matching SHA-256: `3a9ff7558e19b2060243fe0c9c4c5ebd76148520943e17e89f31bafc2033a5c8`.
- Includes all local Qualtrics draft questions, scales/options, block timing, and clearly marked newer proposals. Does not change live Qualtrics forms or the run sheet.
- Sync this companion independently, staging only its intended edits. The existing inventory file and its last common file baseline above remain unchanged.
