# Copy a closed Vive recording for verification

**Implemented workflow; device execution pending.** The current Android package is `com.DefaultCompany.MixedRealityTemplate`, as configured in this checkout. Confirm the installed build before copying if its package ID differs. Finish the app's normal thank-you/exit path first; pressing Home can leave its eye writer active. Preserve interrupted recordings as incomplete evidence.

Copy these three sibling directories from the app's `files` directory:

| Directory | Needed for |
|---|---|
| `GazeData` | Participant/run summary, schedule, events, object manifest, compatibility gaze CSV, voice manifest and in-app ratings |
| `EyeMeasurements` | Continuous raw eye observations, capability metadata and clean-close receipts |
| `GazeReplays` | Reconstruction JSONL and the referenced synthesized speech/chime files |

The older `scripts/run-analysis-pipeline.sh --pull` copies **only `GazeData`**. It is not a complete raw-eye/replay transfer. Keep each replay's audio beside its JSONL. Video is separate: retain the matching run footage and its alignment sidecar; do not associate a video just because its filename looks similar.

From the project root, list devices using Unity's bundled adb:

```sh
adb_bin=/Applications/Unity/Hub/Editor/6000.3.10f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
"$adb_bin" devices -l
```

Choose the authorized Vive serial shown there. `unauthorized` needs confirmation on the headset; multiple devices require an explicit serial. The commands below are a manual workflow, not a claim that a headset was connected overnight.

```sh
device_serial=REPLACE_WITH_THE_VIVE_SERIAL
package_id=com.DefaultCompany.MixedRealityTemplate
"$adb_bin" -s "$device_serial" shell getprop ro.product.model
"$adb_bin" -s "$device_serial" shell pidof "$package_id"
```

Check that the model is the intended headset and that `pidof` prints no running app process. Do not force-stop a live recording just to obtain a complete receipt. If the session was interrupted, copy it as-is and use the incomplete audit path.

Make a fresh destination, then pull each directory. `mktemp` prevents merging this copy into an earlier one; these commands do not remove anything from the headset.

```sh
mkdir -p analysis/results
if copy_root=$(mktemp -d "$PWD/analysis/results/headset-copy-XXXXXXXX"); then
  "$adb_bin" -s "$device_serial" pull "/sdcard/Android/data/$package_id/files/GazeData" "$copy_root/GazeData"
  "$adb_bin" -s "$device_serial" pull "/sdcard/Android/data/$package_id/files/EyeMeasurements" "$copy_root/EyeMeasurements"
  "$adb_bin" -s "$device_serial" pull "/sdcard/Android/data/$package_id/files/GazeReplays" "$copy_root/GazeReplays"
  printf 'Transfer destination: %s\n' "$copy_root"
fi
```

Check **all three** command results. A missing directory or failed pull is missing evidence; do not substitute files from another run. A successful pull alone does not establish a cleanly closed recording or exact joins. Run the [combined recording audit](../analysis/README.md#combined-recording-audit-before-analysis) with the chosen run and all matching eye/replay files. Header paths describe the device location and may be stale after a recording restart; exact recording IDs and observation sequences drive the audit.

The destination is local and ignored by Git. It is a working copy, not the study's durable archive: retain the originals and move the verified package to approved study storage before retiring this checkout. Raw gaze/motion and participant-specific synthetic voice clips remain participant data. This procedure does not upload files or request app credentials, enrollment folders or unrelated device folders.
