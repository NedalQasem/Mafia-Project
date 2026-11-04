# Narrator clips with ElevenLabs

Generates the narrator's voice-over (Arabic and English, a man and a woman) from the tables in [Docs/narrator-lines.md](../../Docs/narrator-lines.md) and puts each clip where the game loads it (`Assets/_Project/Resources/Narrator/<lang>/<m|f>/<file>.mp3`).

Needs Node 18+ and an ElevenLabs account. **The API key is never written to a file or committed**: it is read from the environment of the terminal you run it in.

```bash
# PowerShell:  $env:ELEVENLABS_API_KEY = "..."      bash:  export ELEVENLABS_API_KEY=...
node Tools/narrator/generate-narrator.js voices                       # lists the account's voices (ids)
# set ELEVENLABS_VOICE_M and ELEVENLABS_VOICE_F to the chosen ids, then:
node Tools/narrator/generate-narrator.js generate --dry-run           # what it would do and the credit cost
node Tools/narrator/generate-narrator.js generate --only dawn_kill,call_mafia --lang ar   # audition a few lines first
node Tools/narrator/generate-narrator.js generate                     # every missing clip
```

- A rerun only generates the clips that are missing; `--force` redoes them (use it after changing a voice or a line).
- It prints each clip's approximate length and warns when it is longer than its scene in the game.
- Style: `ELEVENLABS_SPEED` (default 0.9), `ELEVENLABS_STABILITY` (default 0.5), `ELEVENLABS_TAG` (e.g. `[low, calm]`, v3 model only), `ELEVENLABS_MODEL`.
- After changing a line in `MatchCues.cs`, update `Docs/narrator-lines.md` (a test fails otherwise), then rerun with `--force --only <file>`.
- Rotate the API key at elevenlabs.io after a session where it was shared.
