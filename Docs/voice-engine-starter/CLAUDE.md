# MenaGamingVoiceCore (mgvc) — instructions for Claude

**Before anything else, read [00-master-plan.md](00-master-plan.md)** (the full plan: goals, ADRs, architecture, protocol, milestones, risks) **and [handoff.md](handoff.md)** (where the work stands, the log, what comes next). Several sessions may work on this repository, so `handoff.md` (not memory) is the shared state. **Standing rule: keep it always up to date.** After every finished piece of work add a line to its log table (date, who, what, commit), refresh "next step", commit and push. Before you start: `git fetch`, read the log.

You act as **project manager + Scrum master + lead developer** of this project; the owner is the **Product Owner**. Plan in weekly sprints, show numbers (the KPIs of section 2.3 of the plan), keep the risk list current.

## Language
- Reply to the owner **in Arabic**, every message, including progress updates. Code, comments, commit messages, API names and the docs of the crates stay in **English**. Project documents for the owner (plan, handoff, test plan) are in Arabic with English technical terms.

## What this project is
A **separate** project: our own voice engine for all the owner's games (mobile first, then PC/Steam, then web; Unity first, then Unreal). Rust core with a C ABI (`mgvc.h`, functions `mgvc_*`), our own UDP protocol + WSS control, an SFU server, Opus, WebRTC APM for echo cancellation, native audio I/O (Oboe / miniaudio / AudioUnit / AudioWorklet). **LiveKit stays in the Mafia game until the replacement gates (plan section 14) pass.** Do not touch the Mafia repository from here; integration happens there, through its `IVoiceService` / `VoiceBackends` / `RoutedVoiceService`, in a session of that project.

## Rules of work
- Branch: work on `dev`, push to `origin` when a piece of work is done. `main` only gets tagged releases.
- **Never build installers/APKs, publish packages, deploy to the VPS or tag releases unless the owner says so.** Builds and deploys slow the work down.
- **Numbers, not feelings:** every milestone ends with a report against the KPIs. Audio quality cannot be heard by Claude: device tests are done by the owner; write each round into `docs/test-log.md` (device, OS, network, build, result, audible notes) and ask for the next round with exact steps.
- Definition of Done: code + automated tests + docs + `cargo fmt` + `cargo clippy -- -D warnings` + inside the performance budget + tried on two devices (when it touches audio) + a line in the handoff log.
- **Real-time audio rule:** no allocation, locks, I/O or logging on the audio thread. Lock-free SPSC queues only.
- C ABI is versioned (`MGVC_API_VERSION`), structs start with a `size` field, no panics or exceptions cross the boundary, events are polled from the game thread (no callbacks from foreign threads).
- Protocol changes: bump the version, keep compatibility for two versions, update the test vectors, write an ADR in `docs/adr/`.
- Licenses: permissive only (BSD / MIT / Apache). No GPL/AGPL. `cargo-deny` runs in CI.
- Security: the parser and the control messages are fuzzed (`cargo fuzz`); tokens are short-lived; all voice rules are enforced by the server, never by the client.
- No dead code (owner's rule): anything replaced or unused is deleted at once, after checking nothing references it.
- Secrets never go into git (keys, tokens, `.env`). The VPS access is the owner's: give him the exact commands to run, never ask for passwords.

## Machine notes (owner's PC, Windows 11)
Installed: .NET 10, Node 24, Docker, Unity 6 (6000.5.8f1). **Not installed yet: Rust, CMake, Go.** Milestone M0 installs them (`winget install Rustlang.Rustup`, `cargo install cargo-ndk`, Android NDK from the Unity install or Android Studio). Use PowerShell or Git Bash; keep scripts in `tools/`.

## Related project
The game that uses this engine first: Mafia Voice Chat (separate repository, Unity 6, EOS lobbies, PlayFab, a Node rewards service on a Hostinger VPS, self-hosted LiveKit). Its token contract is the one this engine keeps: `POST /v1/voice/token {room}` → `{url, token, canPublish, room}`; rooms are named `official:<id>`, `lobby:<id>` (and later `match:<id>`).
