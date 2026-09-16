# Record headset video with Yeti audio

From the project root:

```sh
npm run record:start
npm run record:status
npm run record:stop
```

No npm install is needed. Requires Python 3, scrcpy, FFmpeg/ffprobe, ADB, an authorized USB headset and macOS microphone permission. `record:start` selects the currently enumerated **Yeti Stereo Microphone** by name and prints `RECORDING` once both output files contain data. It refuses to silently switch microphones or start over an active session.

`record:stop` requests finalization. Check status until `complete`; disconnecting the headset also stops and merges automatically. Originals and the MP4 are saved under `~/Movies/GazeRecordings/`. `record:status` shows the session folder, state, microphone and current file sizes. If start reports `starting`, video may still be waiting for an awake headset; check status before beginning the demonstration.

List microphones with `npm run record:devices`. Override explicitly with `npm run record:start -- --mic 'Microphone name' --serial DEVICE_SERIAL`.

The video is silent and the separate WAV contains microphone audio. Headset app sound remains enabled. The tested Android 12 headset does not support scrcpy playback duplication; microphone pickup of headset speakers is not a clean digital app-audio track.

Audio alignment uses capture launch times and is approximate. Say “mark” with a visible controller gesture for more precise alignment. To advance audio 300 ms in a new merged file:

```sh
npm run record:merge -- --session /absolute/session/folder --audio-offset -0.3
```

Positive offsets delay audio. Existing files remain untouched. A final static video frame is extended to the capture endpoint if no further frames arrived, preserving later commentary. Validate sync with the visible/spoken cue. Run the synthetic-media merge checks with `npm run record:test`.
