# Room bot

A .NET program that keeps the official rooms (Mafia Office...) open on EOS and runs the made-up players (bots). It uses the game's own code: the EOS C# wrapper
(`Assets/ThirdParty/EOS-SDK`) and the pure room / match rules (`Assets/_Project/Scripts/MatchRules`).

Facts found by running it (2026-11-01):
- EOS needs `Tick` on the thread that created the platform (RTC fails otherwise): everything runs on the main thread.
- **A lobby with a voice room holds at most 16 members** (`Lobbies that generate conference rooms must have <= 16 max players`); a lobby without voice holds 64.
- EOS Device ID is one identity per machine: the bot cannot log in many real EOS users, so bots are *virtual members* run by the bot / the room host, not EOS users.

Local run (Windows): set `EOS_PRODUCT_ID`, `EOS_SANDBOX_ID`, `EOS_DEPLOYMENT_ID`, `EOS_CLIENT_ID`, `EOS_CLIENT_SECRET` (the game's `Environment_Dev.asset` has them; never commit them), then `dotnet run -c Release`.
Do not run it while the Unity editor is playing on the same PC: both use the same Device ID, so they are the same EOS user.
