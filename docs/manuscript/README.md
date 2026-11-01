# Historical planned-study manuscript

**Implemented — November 1, 2026:** the current schedule is 96 measured trials (48 per voice), plus one practice: six per signed step, absolute-angle counts 6/12/12/12/6, and 24 above/24 below with three of each per signed step. Each of the 24 identities occurs exactly twice per block. See [current schedule](../balanced-angle-schedule.md). Earlier dated counts and allocation descriptions below are historical; they do not describe v9.

**Superseded draft — October 20, 2026.** The 2×2 / 64-trial / N=48 design below is historical. Current working sources are in `../protocol/overleaf/`: two voice conditions, 48 trials each, one practice in the first assigned voice and final retrospective questionnaires. Participant N requires new simulation under the planned trial-level model. The shared `references.bib` is synchronized to the 62-entry working library; that does not update this old manuscript’s design claims.

`gaze-guidance-planned-study.tex` is the IEEE-style planned-study manuscript for the proposed eight-plane, 360-degree experiment. It intentionally reports no empirical results.

Build from this directory with:

```sh
tectonic gaze-guidance-planned-study.tex --outdir ../../output/pdf
```

The design currently targets 48 complete participant datasets and schedules up to 64 experimental trials per participant (a standard 16-trial manifest per 2 × 2 cell; two scheduled targets on each of eight planes per full cell). A complete dataset requires at least 12 technically valid trials per cell. Pilot-dependent thresholds and prespecified early-stop rules are identified in the manuscript and must be frozen before preregistration.
