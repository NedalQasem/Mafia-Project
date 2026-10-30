# Mafia Voice Chat — instructions for Claude

**Before anything else, read [Docs/12-handoff.md](Docs/12-handoff.md)**: where the work stands, what was done, what comes next, the rules of this project's owner and the known pitfalls. Several Claude accounts work on this repository, so that file (not memory) is the shared state. Update it at the end of every session.

- Reply to the owner **in Arabic**, every message, including progress updates. Code, comments and commit messages stay in English.
- Work on the branch `refactor/architecture` and push it to `origin` when a piece of work is done.
- New screens: an HTML mockup in `Docs/mockups/` first, the owner's approval, then Unity built to match it.
- Never commit the Unity files that change by themselves (font `*SDF.asset`, URP/ProjectSettings assets, gradle `.backup` files).
