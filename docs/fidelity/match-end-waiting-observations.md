# Match-end waiting observations

The score-driven match-end evidence comes from `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`. Ticket 054 and ticket 057's shaping session independently decoded the two native frames below with imageio-ffmpeg's FFmpeg 7.1 pipeline, `-copyts`, `-fps_mode passthrough`, exact PTS selection, two threads, and SHA-256 over 3,686,400 packed 1280×720 RGBA bytes.

| Observation | PTS / seconds | RGBA SHA-256 | Visible facts |
|---|---:|---|---|
| waiting | 2000158666 / 200.015867 | `c4c9547151263157cf54afe9495c7f1b1103cc3c2da8d3b29314bb0c57a09a6d` | `WAITING` is centred over the still-live arena. Both fighters remain visible. The completed-round HUD shows three filled orange pips and five filled blue pips, and the upper-right badge stacks remain. |
| following new match | 2039991840 / 203.999184 | `7f8703810079f5beef741953d896175d70ee5602fe997b37be3349308c218da0` | Both five-pip rows are empty, the upper-right badges are absent, and a fresh orange five-card fan is mid-reveal. |

The first recording independently shows a concluded 4–5 match before `REMATCH?`. Together the recordings establish five completed rounds as a winning score even when the other player has three or four. They do not establish a win-by-two rule, because neither terminal score can distinguish it from first-to-five.

The 3.983317-second interval between the two frames does not reveal what leaves `WAITING`. It may be automatic, host-driven, peer-driven, or caused by an unobserved network event. The first frame therefore owns a stable match-end state; the second remains evidence for a later new-match reset ticket, not permission to invent its trigger here. The recording also does not expose the final elimination tick closely enough to bind the duration of the ordinary half/round result envelope that precedes `WAITING`.

Ticket 057 may use a constructed source-backed match point to exercise the state through a real authoritative elimination. Such a profile must label its prehistory as constructed and must not claim that its arena, fighter route, cards, or frame as a whole reproduces the pink-fortress interval.
