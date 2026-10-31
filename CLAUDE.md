# Mafia Voice Chat — instructions for Claude

**Before anything else, read [Docs/12-handoff.md](Docs/12-handoff.md)**: where the work stands, what was done, what comes next, the rules of this project's owner and the known pitfalls. Several Claude accounts work on this repository, so that file (not memory) is the shared state. **Standing rule (set by the owner): keep it always up to date.** After every finished piece of work (not only at the end of a session) add a line to the "log" table in section 7 of that file (date, who: your account/session name, what, commit), refresh "next step" if it changed, then commit it and push. Before you start, run `git fetch` and read the log, so you know what the other accounts finished and don't redo or overwrite it.

- Reply to the owner **in Arabic**, every message, including progress updates. Code, comments and commit messages stay in English.
- Work on the branch `refactor/architecture` and push it to `origin` when a piece of work is done.
- New screens: an HTML mockup in `Docs/mockups/` first, the owner's approval, then Unity built to match it.
- Never commit the Unity files that change by themselves (font `*SDF.asset`, URP/ProjectSettings assets, gradle `.backup` files).

## Design standards (set by the owner, apply to every screen)

1. **Content size must never break the look.** Text of any length (a one-word title, a 600-character body, Arabic or English) keeps the same shape and beauty: heights come from the real text (TMP preferred size, never a guess by letter count), long single lines shrink (auto-size) or are cut with "…", long bodies scroll or the whole panel scales down. Test each new screen with a very short and a very long text.
2. **Responsive on every device.** The canvas is 1080x2400 scaled by width; screens must also hold on short, wide and tablet-like aspect ratios (panels scale down to fit with a margin, nothing is cut or overlapped, safe areas respected). Screenshot a new screen at more than one aspect ratio before showing it.
3. **The player chooses the language** (Settings → الحساب → اللغة, saved in PlayerPrefs `mafia.lang`, Arabic by default). Never guess it from the device language.
