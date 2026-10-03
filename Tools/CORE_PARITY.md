# Gameplay parity evidence

The native C# state machine is a port of HTML edition 13, source commit
`1665a124f2a60c380989dd53af80a943386e2d7e` in `rintrint/rintrint.github.io`.
The source inheritance chain is `tide-core.js → pulse-core.js → duet-core.js → drift-core.js → floe-core.js → gugu-core.js`.

`generate-core-golden.cjs` executes the original JavaScript directly. It records
every state, fish outcome and emitted event at 6,014 checkpoints in 64 scenarios.
The Unity Editor build gate can deserialize `core-golden.json` with `JsonUtility`
and execute `SealGugu.CoreParity.Run(chartDocument, goldenSuite)`. A mismatch
throws an exception and must prevent release. `test-core-parity.ps1` also runs
the same pure C# port independently with PowerShell 7.

Coverage includes all three songs and three difficulty levels with perfect play
at both 30 and 144 FPS; varied judgement windows and calibration; early, late,
wrong-lane and repeated input; permitted empty beats; music-intro oxygen grace;
automatic departures; pause/resume; opening timing; surface tap refill;
song-specific fish totals, growth and ending boundaries; all four endings;
and deliberate button mashing leading to failure. A separate 10,000-tap
invariant checks that later breaths approach, but never reach/display, 100%.

Initial native execution result: **64 scenarios, 6,014 checkpoints, 589,032
assertions passed**. This verifies gameplay logic and timestamps; it does not
replace visual review, physical device latency calibration or audible playtest.

To regenerate from the HTML source:

```powershell
node Tools/generate-core-golden.cjs C:/path/to/web/gamejam
pwsh -File Tools/test-core-parity.ps1 -SourceGame C:/path/to/web/gamejam
```

The golden file belongs under `Tools`, outside Resources, so it is not included
in the shipped player. Do not regenerate expected values from the C# port.

## V14.1.1 intentional balance change

The shipped default oxygen multiplier is now **0.5**. Historical golden replays explicitly pass `oxygenDrainMultiplier = 1`, preserving the unmodified HTML baseline. `OxygenBalanceParity.Verify` separately gates every build on the new default: all 3 songs × 3 difficulties × 3 depths × 3 frame rates, intro grace, no surface drain, unchanged 12% refill, pause, and the doubled oxygen-exhaustion boundary. Full-song native QA uses the real default 0.5. These tests do not claim unchanged oxygen balance.
