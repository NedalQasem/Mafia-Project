# Handoff — MenaGamingVoiceCore (mgvc)

> **Last update:** (fill in) · **Branch:** `dev` · **Milestone:** M0 (not started)
> New Claude session: read this file first, then `00-master-plan.md`, then `git log --oneline -30` and `git status`.

## 0. Where we are
Nothing is built yet. The plan is approved: name **MenaGamingVoiceCore**, Rust for the core and the server (owner's decision 2026-11-02). LiveKit stays in the Mafia game for now.

## 1. Owner's rules
- Reply in Arabic; code, comments and commits in English.
- No builds, deploys or releases unless he asks.
- He tests on real devices; give him short exact steps and ask for the numbers back.
- He is price-sensitive: everything runs on one VPS (Hostinger KVM 1: 1 vCPU, 4 GB RAM, 50 GB, 4 TB/month); no paid services.

## 2. Next step
**M0 — foundation (3–4 days):** install Rust + cargo-ndk + NDK + CMake; create the repository layout of section 5.1 of the plan; CI (build, fmt, clippy, tests, cargo-deny); protocol spec v0 + test vectors; a `hello` call through the C ABI from a Unity project on Windows. Done = CI green, spec file, hello from Unity.

## 3. Open questions for the owner
1. Names: domain `voice.mena-gaming.com` OK?
2. Device matrix (manufacturer, model, Android version) of him and the team.
3. Which game after the Mafia will be the second user (decides Unreal vs Web order)?
4. Product decisions parked in the plan: recording on report, minors, E2EE, spatial audio.

## 4. Known pitfalls
(grows with the work)

## 5. Log (newest at the bottom)

| Date | Who | What | Commit |
|---|---|---|---|
| 2026-11-02 | Mafia-project session | Plan written and approved; starter pack created | — |
