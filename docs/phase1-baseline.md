# Phase 1 baseline: Azure video translation, clip A

Baseline for the spike comparison. Clip A is `samples/voa10/media/coffee-shop.mp4` from Trackdub Director
(20.0 s, 6 lines, two speakers). Clip B was dropped; clip A is the only baseline.

## Run

- API: video translation REST, `2026-03-01`, Entra token auth (create, list and get-iteration accept it; get-operation does not).
- Settings: en-US to es-ES, `PlatformVoice`, `speakerCount` 2, one iteration, no manual edits.
- Translation `td2-phase1-clipA-s2`, expires 2026-12-12 (31-day retention on this API version).
- A first run with `speakerCount` 1 merged six lines into four cues with one voice; it is not used.

## Scoring

Source windows are Director's `corrected_segments` (Trackdub-Director `samples/voa10/live-dub-2026-11-04.json`).
Overrun is the dubbed line's end past its window end. Cue ends come from Azure's metadata file and were
checked against ffmpeg silence gaps (`-30dB`, 0.1 s) at 14.36 s and 17.12 s. Lines 3 to 5 run together in
the audio, so the audio alone cannot separate them.

| Line | Window (s) | Azure cue (s) | Overrun (s) |
|---|---|---|---|
| voa10-001 | 0.06-1.26 | 0.33-1.35 | 0.09 |
| voa10-002 | 1.66-6.06 | 1.35-5.84 | 0 |
| voa10-003 | 6.46-10.94 | 6.45-10.27 | 0 |
| voa10-004 | 11.10-14.70 | 11.07-14.51 | 0 |
| voa10-005 | 14.78-17.38 | 14.51-17.17 | 0 |
| voa10-006 | 17.82-19.62 | 17.18-19.20 | 0 |

- Fit rate, cue end vs window end: 6/6 if the 10% tolerance applies (line 1 is 0.09 s over a 1.2 s window, limit 0.12 s); 5/6 with zero tolerance.
- Fit rate, cue span vs window duration at 10%: 5/6. Line 6 spans 2.02 s against a 1.98 s limit (1.8 s window). Cue span is not measured audio: the line 6 cue contains a short internal pause, and speech runs 17.23-19.07 s (1.84 s) in the audio gaps.
- These are not the same method as Director, which compares each line's measured audio duration with its window, so the Azure and Director rates are not directly comparable. Azure audio was not re-measured with the Director method.
- Worst overrun: 0.09 s. Early starts: line 5 by 0.27 s, line 6 by 0.64 s.
- Director, same windows (Trackdub-Director `samples/voa10/live-dub-2026-11-04.json`, line 5 window 2.6 s): first pass 6/6, final 6/6, 0 revisions, measured durations 1.12, 3.84, 4.08, 3.76, 2.80, 1.84 s. An earlier Director run (Trackdub-Director `docs/metrics-snapshot.md`, line 5 window 2.2 s) was 5/6 first pass, 2 revisions; it is not comparable to these windows.
- Not measured: meaning verdicts and the 70% stress set.
- Billed cost: pending. Azure usage had no rows for `rg-trackdub2-spike` on 2026-11-11.
