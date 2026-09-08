# New-match draft observations

The second recording, `reference/MedalTVRounds20260903170709695.mp4`, is 791,095,141 bytes with SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`. Frames were decoded through imageio-ffmpeg's FFmpeg 7.1 with two threads, exact PTS selection, packed RGBA output, and no audio.

| State | PTS | Native RGBA SHA-256 | Observation |
|---|---:|---|---|
| first orange draft visible | 2039991840 | `7f8703810079f5beef741953d896175d70ee5602fe997b37be3349308c218da0` | Both five-pip score rows are empty, the prior badge stacks are gone, orange owns a five-card fan, and no `WAITING` overlay remains. |
| orange offer row exposed | 2045158486 | `237f617c493da1ca0270c57b0592ec82deb537ae725cff9b766f01d342cbc3c8` | Left to right: `DAZZLE`, `STEADY SHOT`, `TANK`, `TIMED DETONATION`, `HOMING`. Homing is hovered and reads “Bullets home towards visible targets”, “Slightly lower DMG”, “Slightly lower ATKSPD”, “+0.25s Reload time”. |
| blue offer row exposed | 2080158346 | `b39bed848ef852303eb31072c4852b0e5614648b82613029d88a82e22e39b186` | Left to right: `HUGE`, `STEADY SHOT`, `EXPLOSIVE BULLET`, `HEALING FIELD`, `PARASITE`. Parasite is hovered; all five titles and rule panels are visible. |

Each decode contains exactly 3,686,400 bytes at native 1280×720 RGBA. The retained command shape is:

```text
ffmpeg -hide_banner -loglevel error -threads 2 -ss <PTS seconds minus 2> -t 3 -copyts -i <recording> -an -vf select=eq(pts\,<PTS>),format=rgba -fps_mode passthrough -frames:v 1 -f rawvideo -y <output>
```

The first identified `WAITING` frame is PTS 2000158666. These frames prove that a reset and orange-then-blue draft follow within the recording, but they do not show the event that ends `WAITING`: no timer expiry, host action, peer vote, lobby-ready message, or local input is visible. The implementation may therefore expose reset semantics to a later lifecycle owner, but it cannot assign that trigger from this footage.

The source-visible rules identify catalog rows, not verified gameplay formulas. New rows remain catalog-only until their single-card, duplicate, cross-card, timing, targeting, and presentation behavior is independently supported.
