# Combined session CSV

**Implemented:** `analysis/mega_csv.py` creates one wide CSV for a saved measured run, combining existing gaze/trial/object/survey files with its matching replay packages. Original recorders and source files remain intact. This is a post-session export on the computer, not an extra per-frame headset writer or a replacement for the raw recording.

```bash
python3 analysis/mega_csv.py /path/to/GazeData/P001/run_001_... \
  --replays /path/to/GazeReplays \
  --output /path/to/exports/P001_run001_mega.csv
```

Use completed/copied files rather than a directory still being written. The run must contain `trial_summary.json`. Without matching replay data, CSV-only exports still work and report missing motion/replay coverage. The optional `--replays` path can be a package or the parent containing packages. Unsupported/corrupt interior replay records fail validation; a truncated last fragment yields an explicitly incomplete export. The output and adjacent `.manifest.json` are never overwritten. The CSV is fully staged before publication.

## Rows and columns

`row_type` distinguishes records within the **same file**:

- `trial`: one row for every summarized/planned trial, including unstarted ones.
- `sample`: a gaze CSV sample joined to a replay sample with the exact same measured trial ID and Unity frame. Joins also require the same coded participant, run number and source run folder. Update and LateUpdate remain distinct observations within a frame, not proof of identical hardware sample time.
- `gaze_sample` / `replay_sample`: unmatched samples are retained separately, with `join_status`; no nearest-time interpolation.
- `event` / `replay_*`: original event rows and replay events are both retained with provenance. Do not add counts across these two representations of the same action.
- `object` / `replay_object` / `replay_object_change`: layouts and changes, one object per row. This avoids multiplying every gaze row by all 168 objects.
- `mesh_vertices`, `mesh_normals`, `mesh_triangles`: geometry elements, with mesh ID, index and recording linkage, for later reconstruction.
- `survey`, `voice_manifest`, `voice_clip`, `session`: original contextual records and voice metadata.

All source fields are kept in separately named column groups (`gaze__`, `replay__`, `object__`, `event__`, etc.). Nested vector/controller fields become columns such as `replay__leftController__position__x`. Variable-length lists in summaries remain JSON cells; object/mesh arrays get dedicated rows. JSON nulls/missing CSV measurements stay blank; recorded zero, false and unknown sentinel values are preserved.

Every relevant measured-trial row repeats `session__`, `trial__` and `schedule__` values and the uniquely identified target's manifest fields (`target__`). This includes existing trial timing, outcome, gaze-proxy summaries, theta, voice and target information without computing new unvalidated measurements. Practice rows are flagged and do not inherit measured-trial summaries or questionnaires. Final trial outcomes repeated along the timeline are retrospective context, not values necessarily known at that time.

`nasa_tlx.csv` is included automatically from the run directory. Unambiguous block responses repeat under `survey__nasa_tlx__block__...` on that block's trial/sample/event rows. Original survey rows remain separate, so repeated values must not be counted as independent responses. Duplicate responses are preserved as rows and flagged rather than arbitrarily selected for repetition.

Additional questionnaires can be imported with repeated `--survey /path/to/name.csv` arguments. External CSVs must explicitly contain `participant_id` and `run_number`; use optional `block` or `trial_id` for narrower scope. Trial scope takes precedence over block; absent both means run scope. Survey filenames must have distinct stems. Unmatched participant/run responses are not imported. Uncollected surveys are not invented.

## Timing, coordinates and assets

The export preserves Unity gaze/event time and replay-relative monotonic time in their original fields. It does not claim they share a zero point. Rows are grouped by source, not globally sorted across clocks. Gaze/object CSV world coordinates and replay coordinates relative to its saved origin remain separately labeled; the replay header supplies the transform.

Spoken audio remains in the replay package, with clip references in CSV. Retain the original JSONL/WAV packages alongside the CSV. This export is not a complete binary-media container, a replay importer, native 120 Hz acquisition, or validation of fields historically called fixations/saccades.

The manifest reports source paths/sizes, row counts by type, column count, join/coordinate policies and incomplete/missing-source warnings. The exporter spools samples to temporary SQLite and rows to disk to avoid loading a whole session into memory.

## Verification and follow-up

Run `python3 -m unittest discover -s analysis -p test_mega_csv.py -v`. Tests cover exact joins, missing/duplicate frames, wrong-run isolation, practice separation, repeated/duplicate surveys, external trial questionnaires, missing replay, geometry, voice metadata, quoted/multiline CSV values, source field preservation, truncation/corruption and overwrite refusal.

**Open hardware validation:** export a fresh headset run and reconcile selected sample/event rows against the viewer and questionnaires. No saved measured-run summaries were present on the connected headset at the implementation check, so current export evidence is synthetic, not participant data.

**Requested backlog:** [#36 trial reconstruction and interactive runner](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/36), [#37 timeline playback and synchronized data inspector](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/37), linked to [#28 data validation](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/28).
