# Mafia Voice Chat — instructions for Claude

**Before anything else, read [Docs/12-handoff.md](Docs/12-handoff.md)**: where the work stands, what was done, what comes next, the rules of this project's owner and the known pitfalls. Several Claude accounts work on this repository, so that file (not memory) is the shared state. **Standing rule (set by the owner): keep it always up to date.** After every finished piece of work (not only at the end of a session) add a line to the "log" table in section 7 of that file (date, who: your account/session name, what, commit), refresh "next step" if it changed, then commit it and push. Before you start, run `git fetch` and read the log, so you know what the other accounts finished and don't redo or overwrite it.

- Reply to the owner **in Arabic**, every message, including progress updates. Code, comments and commit messages stay in English.
- Work on the branch `refactor/architecture` and push it to `origin` when a piece of work is done.
- New screens: an HTML mockup in `Docs/mockups/` first, the owner's approval, then Unity built to match it.
- **Never build an APK on your own (owner's rule, 2026-11-01):** builds take 4-8 minutes and slow the development. Build only when the owner says so; until then keep working, commit and push.
- **Every build (APK) is a numbered version (owner's rule):** before building, bump `PlayerSettings.bundleVersion` (`major.feature.build`, e.g. `1.1.0`) and `AndroidBundleVersionCode` (+1), name the file `Builds/Android/MafiaNight-v<version>-dev.apk`, and add an entry at the top of `Docs/CHANGELOG.md` saying what changed since the last version (in Arabic, short). The version shows in the settings screen only (the owner does not want a tag over other screens). Commit the bump with the changelog.
- **MenaGamingVoiceCore (our own voice engine) is a separate project with its own repository and its own sessions.** Never commit its plans, code or notes to this repository (it is public); integration with it happens here only through `IVoiceService` / `VoiceBackends` / `RoutedVoiceService`.
- Never commit the Unity files that change by themselves (font `*SDF.asset`, URP/ProjectSettings assets, gradle `.backup` files).

- **No dead code or old screens (owner's rule):** anything old or unused (a replaced screen, scene, script, art, prefab, a flow that nothing starts) is **deleted** from the project, not left behind: it only adds size and tempts a fix in the wrong place. Before deleting, check that nothing references it (GUID search in scenes/prefabs/assets, `grep` in scripts); git keeps the history. The living code is only the UIKit scenes (`UIKit*`) + `Bootstrap`.

## Design standards (set by the owner, apply to every screen)

1. **Content size must never break the look.** Text of any length (a one-word title, a 600-character body, Arabic or English) keeps the same shape and beauty: heights come from the real text (TMP preferred size, never a guess by letter count), long single lines shrink (auto-size) or are cut with "…", long bodies scroll or the whole panel scales down. Test each new screen with a very short and a very long text.
2. **Responsive on every device.** The canvas is 1080x2400 scaled by width; screens must also hold on short, wide and tablet-like aspect ratios (panels scale down to fit with a margin, nothing is cut or overlapped, safe areas respected). Screenshot a new screen at more than one aspect ratio before showing it.
3. **The player chooses the language** (Settings → الحساب → اللغة, saved in PlayerPrefs `mafia.lang`, Arabic by default). Never guess it from the device language.
