## 2026-10-10 — Semantic Jev speech and near-target continuations

- **Implemented v21:** explicit intent/axes/magnitude/stage/prerequisites/interruption policy for the 300 catalog phrases and all 215 guidance texts. Jev receives structured current/proposed meaning, playback, event and compact gaze/geometry context. The existing study playback, voice identity, normalization and replay archive remain in use.
- **Implemented:** “A bit more.” / “Just a little more.” require actual same-direction progress (at least 2°), at most 10° remaining and at most 20% plane-height error when vertical; one continuation per unchanged direction. Stalling/reversal uses explicit directions.
- **Interruption tuning:** protect the final one second, unknown/finish-only phrases, ordinary progress and redundant same-wall arrival locally. Keep 650ms onset protection, 400ms advice deadline, one advice attempt per utterance and 150ms fade. These gates contain observed incorrect raw Jev approvals.
- **Live verification:** 43 synthetic interruption cases × 3 = 129 calls; 117 completed responses matched expected effective behavior after local gates, 12 deadline fallbacks safely finished (8 missed desired cuts). Completed-response median 203ms, p95 333ms. 16 navigation selections returned eligible nonrepeating phrases. Nine synthetic Eric audio clips generated for listening previews; no participant recordings sent.
- **Offline verification:** voice isolation/fade, gaze quality/history, transport/PCM, rotational layout and enrollment pass. Unity catalog and study integration pass, including JSON/asset semantics and all 2,888 angle/distance grid checks. See `docs/testing/2026-10-10-jev-speech/report.md` for evidence and limitations.
- **Review:** Code review: skipped (ce-code-review unavailable) — its independent reviewer/finish-agent dispatch conflicts with this session's inline-only AGENTS rule. Manual diff review and simplification completed; no independent receipt claimed. Unity's transient unrelated preload changes were not intentionally edited.
- **Device:** Android Build And Run succeeded in 94.9s; installed on Vive FA5AR3N00385 at 18:52:31, running PID 5896. No matching fatal/null-reference/missing-method/metadata exceptions in current-process startup logs.
- **Open validation:** headset listening and perceived naturalness, particularly neutral versus cloned prosody and fast gaze transitions. Synthetic audio previews are timing demonstrations, not headset recordings.

## 2026-10-10 — Checkpoint before primary checkout synchronization

- Saved the accumulated headset guidance, gaze diagnostics, spatial voice pipeline and desktop replay work as a local checkpoint for merging into the primary project folder.
- Preserved the primary checkout's existing modified/untracked files in an external backup before synchronization; its older run-sheet copy must not replace the newer tracked protocol. API credentials remain ignored and outside the checkpoint.
- This checkout synchronization does not constitute new headset validation; the v20 build and focused test evidence remain recorded below.

## 2026-10-10 — Bounded Jev interruption advice and 150ms fades

- **Implemented v20 pilot:** stable corrections can ask Jev in parallel while speech continues, once per utterance with a 400ms deadline. Fresh approval plus local gates is required; missing/error/late/stale advice finishes the phrase. Protects the first 650ms, last 600ms, confirmations and trigger reminders; 2.5s interruption cooldown. Inspector toggle allows reverting to finish-phrase scheduling.
- **Implemented:** linear unscaled-real-time 150ms fade reaches zero before Stop, restores playback gain afterward, and revalidates the replacement. Sustained tracking loss fades locally after the existing 250ms grace; lifecycle/setup cancellation remains immediate. Repeated invalid ticks cannot restart a fade. Unity frame scheduling limits exact timing to the first frame at/after 150ms.
- **Telemetry/replay:** advice request/result/discard and fade-start events; source replay renderer reconstructs the fade envelope and handles seeking/live refresh. Existing standalone viewer binaries are not rebuilt in this task.
- **Live Jev evidence:** four synthetic cases returned expected approve/reject choices in 247/243/199/175ms (arrival and overshoot approved; redundant direction and jitter rejected). No credentials or participant data printed. This does not validate perceived naturalness.
- **Verification:** production fake-audio regression covers half gain at 75ms, no early Stop, zero gain at Stop, prepared correction, stale/late advice, one request per utterance, protected endings/confirmations and lifecycle cancellation. Gaze-quality, voice-isolation and transport suites pass. Refreshed Unity study integration and replay fade reconstruction pass. Review also protected the clone's speech window rather than its effect tail and guarded replay refresh when a segment is inactive. Android Build And Run succeeded in 95.7s; installed on Vive FA5AR3N00385 at 18:26:06, running PID 31267. No matching fatal/runtime exception lines in current-process startup logs. Wearer listening and subjective naturalness remain unverified. Existing work preserved; no commit/push.

## 2026-10-10 — Independent replay walls and degrees

- **Implemented:** separate Walls and Degrees visibility controls. Angle labels remain available when outlines are hidden, for both live and saved replay. Updated eight-toggle layout and README.
- **Verification:** isolated macOS Trial Review 2.2 build and existing Unity replay checks pass, including P20 validation. Updated launchers and replaced the running 2.1 viewer with 2.2. No headset changes or commit/push.

## 2026-10-10 — Trial Review desktop interface redesign

- **Implemented locally:** charcoal/mint desktop interface with persistent first-person, third-person and overhead buttons; reset view; layout-radius-based orbit framing; large visibility chips; searchable/filterable trial sidebar; practice ordinals separate from study rounds; session/file and Wi-Fi controls. Preserved the shared replay geometry, pose, gaze correction, audio and live-follow pipelines.
- **Implemented:** selected-trial/whole-session timeline with saved speech segments, validity-aware gaze spans, event markers, exact-sequence seeking, stable-range scrubbing, sample/event steps, speed, volume and mute. Gaze spans show usable replay samples under the existing 100 ms display freshness rule, not fixations. Audio remains 1x only.
- **Implemented:** At cursor, Events and Final data panes; speech jump, tracking/sample age and expandable decoded observation. Per-trial export creates new samples/events/objects CSVs, supported decoded JSONL and source-hash manifest. Only a loaded identity/hash-matched summary is exported; unavailable questionnaires/results stay absent. This subset does not replace the full replay/audio or raw eye-acquisition package.
- **Verification:** 14 Python tests pass; isolated Unity build passes all interface/export, live, audio, renderer, correction, alignment and reconstruction checks, including P20's 98 sampled poses across nine trials. P20 has a pre-existing incomplete tail, omitted by the reader. CSV tests cover quoting/newlines, missing poses, trial isolation, unchanged source hash and rejection of changed recordings. `git diff --check` passes. Native layout and keyboard play/pause inspected on P20; automated mouse input did not activate Unity buttons, so normal mouse camera/toggle interaction remains a user confirmation, not a claimed pass.
- **Delivered:** Trial Review 2.1.app in the existing side-chat visualization output folder; closed the previous instance, launched only 2.1 on P20 and updated the three launchers to reuse the app rather than open duplicates. Updated tools/trial-review/README.md. No Android build, headset restart, commit or push; unrelated main-thread changes preserved.
- Code review: skipped (ce-code-review unavailable) — independent reviewer dispatch prohibited in this side conversation; manual scoped diff review performed. No matching replay task was found in the available board snapshot; no duplicate task created.

## 2026-10-10 — Signed-angle catalog and navigation speech adapters

- **Implemented:** 300 editable navigation entries (15 angle × 5 horizontal distance × 4 tone), JSON exchange/import, deterministic signed-angle matching and a Jev candidate-only adapter using Vercel's verified TypeSafe compatibility endpoint and `typesafe-ai/jev` model.
- **Implemented:** standalone ElevenLabs PCM WebSocket coordinator with cancellation, bounded fragmented-message handling, audio-thread ring buffer, configurable 60ms prebuffer and timing/underrun diagnostics. No zero-latency claim.
- **Implemented:** ordinary coarse study hints can select already-prepared catalog clips; existing valid-gaze, route lock, sentence pacing, calibrated loudness, voice identity and replay playback remain authoritative. Stateful vertical/diagonal, return, confirmation and milestone cues remain in the current policy.
- **Validation:** production transport/PCM tests pass, including gateway model/endpoint, exact choices, timeouts, socket fragmentation, cancellation and concurrent ring ordering. Refreshed Unity catalog/geometry/local-fallback checks and full study integration pass. Gaze-quality and voice-isolation regressions pass. Final Android Build And Run succeeded in 227.5 seconds; installed on Vive FA5AR3N00385 at 18:13:14, running PID 28795. Verified packaged gateway configuration matches the ignored local file without printing values; no matching fatal/navigation exceptions in current-process startup logs. `git diff --check` passes. No commit/push; side-chat replay work preserved.
- **Live verification:** user-authorized `jev_key` loaded from ignored local configuration. Three authenticated Vercel/Jev checks selected a valid candidate in 1,357/214/213ms; the study retains a 900ms decision deadline and deterministic fallback. Key values were not logged. One authenticated ElevenLabs socket smoke test returned 22,291 decoded 24kHz samples in 692ms (desktop transport, not headset audible latency).
- **Open:** Vive streaming listening/latency. Streaming is callable in a standalone preview; recorded study playback remains calibrated cached audio, because streaming normalization/effect/replay parity is not yet validated.

## 2026-10-10 — Finish guidance sentences and shorten cues

- **Evaluation:** confirmation bypassed the prior correction pacing, permitting “Go back — YEP”; request-time cadence allowed a fresh direction immediately after a longer acknowledgment finished. Invalid gaze also cancelled speech immediately, including brief gaps. User chose complete phrases with shorter wording instead of mid-sentence semantic interruption.
- **Implemented v19:** all automatic corrections and repeat requests wait for natural completion and a playback-end gap: 0.55 seconds ordinarily, one second after all gaze/controller acknowledgment variants. Removed stale-direction interruption. The 0.1-second gaze qualification remains, but waits its turn to speak; latest valid evidence is reevaluated rather than enqueuing obsolete cues. Pause/completion/session cancellation remain immediate. New decisions stop immediately on invalid gaze; already-playing speech gets 0.25 seconds before sustained tracking loss cancels it.
- Shortened large turns, area leads, diagonal refinements, target/wall return wording and the right-controller trigger reminder. Preserved distance tiers and shared policy across both voice conditions. Added the awkward-transition evaluation and current speech policy to `docs/guide/voice-guidance-states.md`.
- **Verification:** gaze/history and rotational-layout suites pass. Production synthesizer fake-audio regression now rejects interruption of held playback, enforces actual-end pauses, cancels obsolete pending cues without consuming acknowledgment, verifies all confirmation variants, and preserves cached/offline playback/decision linkage. Voice-isolation suite passes. Fresh Unity study integration passed. Build shortcut menu dispatch failed, so the same Build And Run was invoked through Unity UI; Android build succeeded in 69.4 seconds, installed at 17:30:05 on Vive FA5AR3N00385, running PID 13786.
- **Open validation:** wearer listening for return → confirmation → next-direction pacing, tracking-gap behavior and naturalness of the shorter phrases. No claim of automatic linguistic boundary detection or measured human naturalness. Unrelated work preserved; no commit/push.

## 2026-10-10 — Gaze-ring shine and right-controller hover rotation

- **Implemented:** valid gaze within the unique closest object's padded vicinity brightens its surface and adds a gold emissive edge. Applies equally to targets and distractors, regardless of dev-ring visibility. Uses the existing padding/overlap arbitration; ambiguous/invalid gaze does not highlight. Restores the previous base color on departure/reset and preserves the controller's blue rim when both point at the same object. No gaze selection was introduced.
- **Implemented:** a fresh tracked right-controller hit spins the object at 60 degrees/second around world up during active search. FixedUpdate applies rotation before physics, keeping visible geometry and the actual hitbox synchronized with automatic transform sync disabled. Stops in place on departure/tracking loss/pause; hidden objects and disabled hitboxes are excluded. Position, scale and identity stay fixed; existing replay transform changes capture rotation. Symmetric spheres may look unchanged; live shine is not a new replay material channel.
- **Verification:** Unity controller checks passed, including new material hue/alpha preservation, cleanup, simultaneous-controller rim priority, rotation/position/scale, hidden-object and disabled-hitbox tests. Final Android build with physics-timed rotation succeeded in 51.6 seconds; generated IL2CPP includes ControllerRaySelector.FixedUpdate. Installed on Vive FA5AR3N00385 at 17:23:15, running PID 3872. `git diff --check` passes.
- **Remaining wearer check:** shine visibility, overlapped-ring arbitration and hover stability near spinning corners. Existing voice/eye/replay work preserved; no commit/push.

## 2026-10-10 — Conversational turn progress and recent-target returns

- **Implemented locally:** guidance v18 chooses turns from actual object bearing (including lateral wall offset), preserves the opening route across the +/-180 seam, and releases that rear-seam preference below 150 degrees or on wall entry. Exact seated 0-to-180 and 180-to-0 ties resolve right and left respectively. Turns of at least 90 degrees remaining say “Turn way to your left/right.”
- Outside-wall encouragement now follows new 45-degree remaining-error milestones, with three rotating variants. Consumed milestones cannot repeat on backtracking; fast turns coalesce them. Ordinary cadence is two seconds; same-direction reminders without new milestones wait four seconds and stay explicit. Within-wall refinement retains its bounded continuation and plane-height tiers.
- A qualified recent target-vicinity visit followed by sustained valid departure can produce “You were just there. Go back down and left” (all eight directions prepared). Two-second departure-memory expiry, six-second spoken cooldown, invalid-gaze/lifecycle reset and post-fade relevance checks bound the cue. Wall-return speech now names the actual direction back. Added decision fields for remaining turn degrees and the latest qualified target visit's receipt time/observation sequence.
- Adding/removing diagonal detail no longer cancels a sentence. Busy ordinary reversal/arrival/return corrections require 650 ms settled evidence and at least 1.5 seconds since the preceding request. Fast 0.1-second gaze confirmation, controller hold and immediate invalid-gaze/lifecycle cancellation remain.
- **Verification:** gaze/history, rotational-layout and voice-isolation suites pass; refreshed Unity study integration passes, including runtime opening-route retention, milestone consumption and lifecycle memory reset. Android Build And Run succeeded in 103.4 seconds; verified Vive FA5AR3N00385 install at 17:16:08 and running PID 27382. No commit/push. Preserved side-chat replay and other WIP.
- **Open validation:** wearer pacing and spoken directions for opposite walls, milestone backtracking, high/low/diagonal search, recent target departure, wall overshoot and blinks. New pilot rules and both Mermaid state diagrams are documented in `docs/guide/voice-guidance-states.md`; no eye-event or accuracy validation is claimed.

## 2026-10-10 — Waiting message copy cleanup

- **Implemented locally:** removed the two supporting sentences below “Waiting for headset signal” and centered the heading alone. No live acquisition or recording behavior changes.

## 2026-10-10 — Replay walls and degree labels

- **Implemented locally:** renamed Study walls to Walls / degrees; it controls outlines and screen-facing 0°–315° markers together, matching the headset toggle. Labels use the saved layout origin/rotation and the headset's 0.43 m layout-relative label height; available in saved/live views, with 0° marked FORWARD. These are viewer overlays, not recorded participant guide visibility.
- **Verified:** desktop build 1.19 and environment checks pass, including rotated/translated marker placement and clearing on missing layouts. Visually verified all eight labels and the toggle in the synthetic demo overhead view. Native UI automation could not click the IMGUI control (noWindowsAvailable), so no manual click verification claimed. Updated README and local launchers; no headset deployment, commit or push.

## 2026-10-10 — Larger live waiting message

- **Implemented locally:** centered the live waiting message in the replay viewport, increased its heading from 14 px to a responsive 32–64 px, and enlarged supporting text to 18–24 px. Wrapped text uses measured heights to keep the block centered.
- **Verified:** isolated desktop build and existing replay checks pass; visually confirmed the large waiting message in the running Trial Review 1.18 app. Updated local replay launchers. Desktop-only change; no headset deployment, commit or push.

## 2026-10-10 — Live follow latency and inactive-headset waiting

- **Implemented locally:** desktop Trial Review 1.17 reduces its live buffer from 3 s to 200 ms, status/audio refresh to 100/200 ms, and receiver sleep between transfer passes from 500 to 100 ms. Cached eye-folder lookup avoids scanning the whole remote inventory for every replay row. Full-file hash verification yields while the newest replay is growing; archiving continues.
- **Inactive source behavior:** receiver exposes observed source-replay growth independently of connection/archive/audio activity. Follow requires current tracked head samples, no terminal/pause marker, caught-up transfer/parser, and source growth within 2 s. Otherwise the viewport displays “Waiting for headset signal” and stops speech/motion. Repeated Follow clicks no longer rewind a following view. Rejoin/catch-up uses the 200 ms edge; manual historical playback remains available.
- **Verification:** regression reproduced missing source-activity distinction; all 14 Python tests pass. Isolated Unity desktop build and follow-policy tests pass (historical first copy, stale source, disconnect, pause, tracking loss/recovery, completed run, backlog, low-lag cursor and no backward following), alongside existing replay/audio/P20 checks. Built Trial Review 1.17 and updated the local launch shortcuts. No study Unity/device restart, commit or push; main-thread WIP preserved.
- **Live follow-up:** accepted Documents access and restored Wi-Fi; visually confirmed stable waiting during offline state and archive catch-up. Real transfer exposed parser-chunk flicker, now fixed: ongoing following tolerates a partially parsed fresh chunk while all source/tracking/pause checks remain enforced. An initially offline headset now retries automatically instead of exiting; identity is checked again after each reconnection. Added regression checks.
- **Remaining manual check:** fresh wearer latency/audio and pause/resume. User accepted macOS Documents access; final 1.17 app and its bundled receiver are running over Wi-Fi. Visually verified the final waiting screen while no fresh samples arrive, with 100/100 files verified, about 803.1 MB archived and zero pending bytes. Fresh wearer latency/audio still needs a trial. End-to-end live latency is not measured or promised to be 200 ms; source writes/network transfer still contribute.
- **Review:** targeted manual review due to unrelated branch work; independent agents prohibited in this side chat. Simplification limited to this fix, preserving overlapping prior changes.

## 2026-10-10 — Wireless live replay and automatic run archive

- **Implemented locally:** Trial Review live Wi-Fi mode uses authorized ADB TCP/IP to copy existing headset replay/audio, linked raw eye observations and trial files, plus current/new preparation folders. It resumes byte offsets across disconnects/restarts, automatically discovers new runs, and keeps a SHA-256 manifest. Headset source recordings stay intact; no separate study/server build is needed.
- Added incremental live JSONL ingestion using the same validator as offline replay; dynamic meshes/trials, first-person/third-person rendering, rewind with ongoing archiving, buffered follow/catch-up, late WAV playback and audio cancellation. UI distinguishes disconnected/waiting/catching-up states. Root README now links the detailed live/replay instructions.
- **Verified:** 12 Python tool tests; Unity live checks (partial tails, exact records, mesh/trial append, rewind, late WAV, uninterrupted audio refresh, cancellation, malformed data), existing real P020 replay/correction/CSV join checks; standalone macOS build `Trial Review 1.14.app`; current study runtime/editor C# compilation (zero errors, existing runtime warnings). `git diff --check` passes.
- **Device evidence:** Wi-Fi endpoint `192.168.1.208:5555` identifies the Focus Vision `FA5AR3N00385`. Copied 148,250,314 bytes across 46 files into `~/Documents/Trial Review Live/data`; all 46 match source SHA-256 and a second local hash check. Explicit ADB disconnect produced connected → disconnected → connected with no pending bytes afterward. Viewer rendered the real received run and archive status. USB remained physically attached, but these transfers explicitly used the TCP/IP endpoint.
- **Limits / remaining wearer check:** run one fresh neutral demo to evaluate live delay, continuous audio and new-run transition under actual wearing. Existing completed/prefix recordings and synthetic incremental tests do not establish that human check. This is telemetry reconstruction, not passthrough video; external surveys/microphone/video are not manufactured. Hash equality verifies copied bytes, not study completion. Initial connection selects latest/current folders, then all new ones; does not bulk-import older history. Receiver must remain open/Mac awake, or catch up later before headset sources are deleted.
- Inline simplification/manual review fixed archive-path resolution on macOS, bulk-copy live lag, final-event draining and unnecessary audio-plan rebuilding. Code review: skipped (ce-code-review unavailable) — its required independent reviewer dispatch is prohibited in this side conversation; no independent review is claimed. No commit, push, Android deployment or study-session restart performed. Main-thread changes preserved.

## 2026-10-10 — UCF readiness charge colors

- **Implemented locally:** replaced the aqua readiness panel fill with opaque UCF yellow (#FFC904). The cross now fills left-to-right from gray to black using the same charge progress, keeping its original size and world position. Readiness duration and valid-gaze requirements are unchanged.
- Visual-only change; refreshed Unity readiness checks pass. Build shortcut failed to address Unity, so the same Build And Run command was dispatched through its menu. Android build succeeded in 54.0 seconds; verified install at 16:13:58 and running PID 8135 on Vive FA5AR3N00385. Headset visual check remains. Preserved unrelated edits; no commit/push.

## 2026-10-10 — Vertical guidance by wall-height distance

- **Implemented locally:** vertical phrasing now uses absolute target-minus-gaze-intersection height divided by the full configured wall height. At or below 20% uses “a little higher/lower”; above 20% through 50% uses “look higher/lower”; above 50% uses “look way higher/lower.” Each tier has three variants and applies to ordinary, diagonal, area-entry and repeat guidance; vertical overshoot return wording also respects the tier. Both voice libraries prepare all variants.
- The ray intersects the target's object plane using its actual radius and seated rotation; height includes the same top margin as the drawn frame. No clamping of off-wall gaze. Existing valid-gaze, horizontal-then-vertical and alignment rules remain. Band changes must settle and receive an explicit next cue rather than an ambiguous “keep going.” Pending area/return speech rechecks the tier after its audio fade.
- Added optional guidance-decision measurements (signed metres, plane height, signed fraction, availability), and advanced policy version to v17. Boundary/phrase-library tests pass with gaze/history, layout and voice-isolation suites. Fresh Unity study integration passes, including translated/rotated wall intersections, height normalization, parallel/backward rays and off-wall measurements. Android build succeeded in 59.7 seconds; verified install at 16:04:48 and running PID 4101 on Vive FA5AR3N00385. Wearer validation remains; no commit/push.

## 2026-10-10 — Faster gaze confirmation

- **Implemented:** reduced the gaze-only target-vicinity confirmation hold from 0.5 to 0.1 seconds, including reacquisition. Controller hold, closest-object arbitration, validity checks, 0.15-second exit rearm and selection reminder remain unchanged. Bumped the logged guidance-policy version and updated runtime documentation.
- Updated the boundary/reacquisition checks; the 0.1-second assertion failed before the threshold change and passed afterwards. Gaze/history script checks and freshly compiled Unity gaze checks passed. Android build succeeded in 44.5 seconds; verified install at 15:56:22 and running PID 31903 on Vive FA5AR3N00385. No commit/push.

## 2026-10-10 — Selection and cross reminders; 100-fragment success feedback

- **Implemented locally:** after target acknowledgment, three continuous seconds of valid target-winning gaze makes a right-controller/trigger reminder eligible once per objective. Existing closest-vicinity arbitration, invalid-gaze suspension, 0.5-second confirmation and 0.15-second reentry remain. Both voice libraries prepare the same new wording.
- **Implemented locally:** after five seconds away from the readiness cross following the setup announcement, one gaze-relative directional reminder asks the participant to find and hold gaze on the cross. The prompt stops when the cross is acquired, direction becomes obsolete, gaze is invalid, or readiness is paused/cancelled. Search timing still starts only after readiness and the opening direction.
- **Implemented locally:** a correct selection replaces the visible target with 100 independently animated faceted fragments in one mesh for 1.2 seconds. Timing stops before the effect; metadata remains for capture observers, the effect survives target cleanup, and no fragment adds a physics body or selectable collider. A local RNG preserves stimulus randomization.
- Verification: focused timer/wording tests first failed before implementation, then passed with existing gaze/history, layout and voice-isolation suites. Added Unity checks for gaze-relative cross geometry, phrase preparation, fragment count/motion, cleanup and RNG isolation. Refreshed Unity study integration passes, including fragment lifecycle checks (edit-mode fixture explicitly invokes runtime cleanup). Android Build And Run succeeded in 68.8 seconds; installed on Vive FA5AR3N00385 at 15:50:29 and running as PID 29803. Wearer validation of reminder timing/direction and the visual effect remains.
- Preserved existing eye-alignment, replay and other uncommitted changes. No commit or push.

## 2026-10-10 — Quick eye alignment (side conversation)

- **Replay follow-through:** additive replay fields preserve source/alignment provenance and both selected pre-alignment and final used rays in the fixed recording frame. New recordings use the measured eye origin without avatar recentering or double correction. Desktop controls now distinguish Recorded gaze / Before alignment, with alignment status/angles/ID at the cursor; Unity replay inspector shows the same metadata. Built `Trial Review 1.11.app`; end-to-end synthetic alignment/capture/JSON/render/seek/gap checks and all 8,134 tracked P020 legacy correction checks pass. Six desktop Python checks and 13 recording-link tests pass. Existing data retained.
- **Implemented locally:** Eye check now offers automatic three-dot quick alignment: one fit point and two independent checks. It estimates head-relative yaw/pitch bias from the actual usable EXT/native gaze source, requires stable valid delivery windows, and only enables a bounded correction after both checks pass. Gaps, insufficient data, unstable gaze, excessive offset, timeout, cancellation and worsening checks cannot accept a correction.
- Preserves raw EXT and native measurements plus the selected pre-alignment ray. Final used rays feed guidance, readiness, CSV/replay and the optional orange visual. Per-attempt JSON saves source, participant, input sequences, before/after errors and pilot settings; each eye row includes the alignment ID/status/applied angles. Save failures disable correction. Source/participant/device changes and app focus loss/pause clear it; no automatic reuse across restarts or mandatory trial gate.
- **Verification:** reproduced the missing quick-alignment implementation with an isolated Unity compile, then ran Unity synthetic fit/held-out, rotated-head, unchanged-raw, native fallback, source/participant change, duplicate, invalid-gap, timeout, noisy/large/zero-offset and JSON-result checks. Runtime and editor study assemblies compile using the current Unity compiler references; 22 eye-quality Python tests pass. No study/device restart, commit or push from this side chat; main work preserved.
- **Open validation:** wearer UI/collection behavior, full-layout accuracy and drift, pilot acceptance limits and actual installed build. This is offset alignment after HTC calibration, not a research-grade accuracy certification or proof of 120 Hz. See `docs/gaze-measurement-validation.md` for controls, fields and limits.

## 2026-10-10 — Wrist mixed-reality toggle

- **Implemented locally:** connected the existing wrist passthrough toggle to HTC's native underlay, labeled it Mixed reality, synchronized template environment fading, and applied transparent camera clear on / opaque off. Startup now explicitly reveals passthrough instead of relying on skipped template onboarding. Disable/destroy releases the native layer and restores camera settings; reenable reconnects the settings listener.
- Controls remain in the fixed panel left/below the initial heading. Right-ray Settings opens the former wrist menu; no left-hand gesture is required. Fresh runtime/editor assemblies passed study integration, including the new mixed-reality bridge tests. After resolving the qualified HTC handle type and waiting for concurrent eye-alignment source imports, Android Build And Run succeeded in 160.6 seconds. Verified install at 15:36:27, running PID 25705 and native underlay creation XR_SUCCESS on Vive FA5AR3N00385. Wearer on/off visibility check remains. No commit/push.

## 2026-10-10 — Closest-object gaze confirmation and prompt reentry

- **Implemented locally:** removed the gaze-confirmation six-second cooldown and shortened valid departure rearming from 0.5 to 0.15 seconds. Returning still requires a continuous 0.5-second hold; lingering, brief boundary jitter and blinks do not repeat acknowledgment. Wall and controller gate defaults are unchanged.
- Overlapping vicinities now compete by gaze-to-collider-center angular distance across active spawned objects with enabled colliders. Confirmation requires a unique target winner; near ties are withheld. Looking at a closer distractor counts as target departure even within the target ring. Both gaze-only and controller-plus-gaze cues use this winner, rechecked before playback. Dev ring green state matches eligibility.
- Added optional guidance audit fields for winner ID, candidate count, ambiguity and closest angular error. Raw geometric intersection remains separate from vicinity assignment and neither is a validated fixation.
- Reproduced reentry failure in script tests and overlap ambiguity in Unity before fixes. Added three-ring, ordering, inactive/disabled object, runtime gate, invalid-gaze and jitter regressions. All script suites and refreshed Unity study integration passed; gaze-only Android build succeeded in 94.4 seconds. Vive reconnected. Combined build with the subsequently requested mixed-reality toggle succeeded in 160.6 seconds and is running on the Vive (PID 25705). Earlier uncommitted changes preserved.

## 2026-10-09 — Bounded continuation and half-second vicinity confirmation

- **Implemented locally:** first-trial recording from development run 007 showed five consecutive “Keep going.” requests at roughly 1.25-second spacing. Each 2-degree improvement requalified because the policy lacked continuation history. Now only one generic continuation is permitted per uninterrupted direction; subsequent cues use specific direction variants.
- Changed gaze confirmation to a 0.5-second continuous valid hold inside the existing padded target vicinity, including its development wrist-adjustable radius. Added four prepared confirmation variants. Holding does not repeat; invalid gaze/region departure resets incomplete holds. Exact ray intersections remain separately logged and are not relabeled as fixations. Matching eye/controller evidence suppresses a redundant second acknowledgment.
- Reproduced the recorded-angle loop in an existing regression suite (failed before fix, passed after). Script suites for gaze/history, layout and voice isolation pass. Refreshed Unity study integration also passed, including actual runtime gate and continuation-history checks. Android Build And Run succeeded in 69.4 seconds; verified updated install at 12:27:50 and running PID 24741 on Vive FA5AR3N00385. Existing recordings retained. Wearer confirmation of pacing and vicinity behavior remains. Earlier uncommitted work preserved; no commit/push requested.

## 2026-10-09 — Pre-search opening, direct gaze feedback and diagonal refinement

- **Implemented locally:** cross readiness now precedes an audible opening direction while objects remain hidden and the search clock is stopped. Matching completed playback reveals objects and starts timing. Pausing cancels the opening and resets readiness; stale/cancelled audio cannot start search. Existing active-search instruction counts exclude this pre-search cue, which is retained in audio/readiness events with its decision ID.
- Added “You're looking right at it.” for stable direct target gaze without a target controller hit. Kept the one-second controller confirmation and larger gaze-vicinity circle distinct. Within-wall refinement now combines both axes into four diagonal directions, with three variants each and axis hysteresis.
- Tests first reproduced the premature onset and missing diagonal behavior. Gaze policy/history, layout and voice-isolation checks pass; refreshed Unity study integration passes, including direct-hit vs vicinity/occlusion and opening onset/pause/stale-trial guards. Final refreshed Unity integration and all three script suites passed. Android Build And Run succeeded in 215.1 seconds; verified install at 10:03:28 and running PID 27938 on Vive FA5AR3N00385. Existing device data preserved. Wearer validation of cue timing, diagonal usefulness and direct-gaze reliability remains. No commit/push.

## 2026-10-09 — One-second pointing confirmation

- **Implemented:** reduced the sustained right-controller target hold for “You're on it!” from 1.5 seconds to 1 second. Valid nearby gaze, tracking freshness and exit/rearm behavior remain unchanged. Updated guidance policy version so recordings identify the new timing.
- Updated boundary, reacquisition and tracking-recovery checks and runtime documentation. Gaze policy/history script and refreshed Unity gaze checks passed. Android Build And Run succeeded in 414.8 seconds; verified new install and running PID 21996 on Vive FA5AR3N00385. Existing device data preserved. Wearer timing check remains.

## 2026-10-08 — GitHub checkpoint

- Prepared all current recovered-study changes for GitHub in two commits: headset guidance/readiness/prompt/menu fixes, and trial analysis/desktop replay with supporting documentation. Retained the existing build and test evidence below.
- Checked changed files for credential patterns, oversized files and whitespace errors; ignored API configuration, build outputs and local participant recordings remain outside the commit. Target: `origin/codex/recovered-study-20261007`.

## 2026-10-08 — Stationary eye and settings controls

- **Implemented locally:** removed per-frame head following from the Eye Check launcher, diagnostic panel and left settings controls. Menus anchor in world space and stay upright while the head turns, tilts or translates. Opening/reopening places panels near the current view once; starting a validation check moves its panel below the target grid once, using the same fixed anchor as the dots.
- Pause/Continue checkpoints also stay world-fixed and reposition only when shown again after Settings closes. Right-ray input, hover haptics, pause behavior and diagnostic target measurements remain unchanged.
- Explicitly refreshed Unity scripts and passed the full study integration suite, including a new head-translation/yaw/pitch/roll regression for launchers, open panels and Continue, plus reopening placement. Android Build And Run succeeded in 63.7 seconds; verified fresh install and running PID 32016 on Vive FA5AR3N00385 with activity status ok. Existing device data retained. Wearer comfort/reach check remains. No commit/push.

## 2026-10-08 — Sustained target confirmation and continuing guidance

- **Implemented locally:** “You're on it!” after a 1.5-second tracked right-controller target hold, with valid nearby gaze at speech onset. Blinks do not erase controller evidence; confirmation finishes through a later pointer departure, while invalid gaze and lifecycle stops still interrupt.
- Added “Keep going.” for same-direction progress; retained explicit directions when stalled, moving away, changing direction or manually requesting a repeat. Added 0.35-second speech settling and within-wall hysteresis, and suppressed redundant same-direction overshoot interruptions.
- Inspected the latest development recording: pointing speech was cancelled about 0.06 seconds after commitment and a downward hint was immediately replaced by a downward return cue. Original recording retained on device; read-only local copy at `/tmp/gaze-guidance-followup/recent.jsonl`.
- Gaze-policy, rotational-layout and voice-isolation tests passed; first new tests failed against missing behavior before implementation. Unity integration passed including the runtime 1.5-second hold and controller evidence during a blink. Final foreground Unity integration passed. Android Build And Run succeeded in 86.1 seconds (`/tmp/gaze-prompt-editor-20261008.log`); verified fresh install, running PID 30761 and activity status ok on Vive FA5AR3N00385. Existing data retained. Scoped inline review per AGENTS (no subagents): reused the entry gate, consolidated direction/error logic and checked speech priority after controller departure. No commit/push. Wearer validation remains.

## 2026-10-08 — Auditable P020 gaze-coordinate correction

- Added an exact-recording correction profile keyed by SHA-256, recording ID and build GUID. It reverses the historical local-X origin reflection using each saved XR-origin pose; direction is unchanged and the actual eye–head offset is retained.
- Replay distinguishes Corrected gaze from original Raw gaze. Missing valid reference data suppresses correction, without substituting a head-anchored ray. No recording, hit ID, summary or trial metric is rewritten.
- Added a separate derived CSV/audit exporter: 9,703 sample rows, 8,134 tracked corrections, explicit invalid gaps, source hash and profile. Median gaze–head offset 1.655 m → 0.0241 m. Readiness cross angle median 20.66° → 8.40°; remaining error is not calibrated accuracy, and readiness membership is not independent ground truth.
- Added synthetic reflection/frame-transform tests and real-file regression checks for every tracked sample, original-data preservation and reference validity. Current study Assets remain untouched.

## 2026-10-08 — Larger replay display selectors

- Replaced tiny viewport checkboxes with 42-pixel-high clickable controls, 16-point labels and 22-pixel state boxes. Seven display options wrap into two to four columns based on available width.
- Reserved space below the viewport for all controls and the room-context note; display toggles retain existing behavior.

## 2026-10-08 — Seated replay chair clearance

- Moved the complete illustrative chair 14 cm backward relative to the avatar to reduce seat/knee clipping; body and recorded head/controllers are unchanged.
- Built Trial Review 1.8, passed the existing Unity verification suite and real P020 checks, and visually inspected seating at 30 s. Updated both P20 launchers. Researched alternative Kenney appearances; no avatar replacement selected or installed.

## 2026-10-08 — Remove irrelevant left-controller replay ray

- Desktop replay now omits the left-controller ray while retaining right-controller and eye-gaze rays. Historical left-hand data and controller/hand model remain unchanged.
- Change is confined to the standalone renderer adapter; added a both-controllers-tracked regression check. No main study Assets changes.

## 2026-10-08 — Gaze visible before cross charge completes

- Fixed the desktop replayer eye-origin ray being gated only by object visibility. It now also appears during fresh, active, unpaused readiness, including zero and partial cross charge.
- Valid head/gaze and freshness checks still apply; pause, transition and stale samples suppress the ray. Study recording data and main Unity project are unchanged.
- Added regression coverage for readiness visibility, recovery, toggles and rewind, and a real P020 charging-sample check (0.5474 s, 8.34% charge).

## 2026-10-08 — Replay wall and room toggles

- Implemented locally in Trial Review 1.5: independent Study walls and Room background toggles. Eight-wall rotational snapshots get inferred octagon outlines; the generic backdrop adds a neutral floor/grid and cutaway outer walls. Both default on and are labeled illustrative/inferred.
- Context uses snapshot origin/rotation and object extents; no study object, tracking, or measurement data changes. Unknown layouts do not get invented octagonal study walls.
- New Unity checks pass for independent toggles, radius/origin/rotation, trial switching/rewind, empty/unknown snapshots and data preservation. Existing replay checks plus 98 P020 camera samples and three Python tests pass. Built and visually checked the P020 room/walls; updated launchers to 1.5. Main study Assets/device untouched.

## 2026-10-08 — Replay hands and eye-origin presentation

- Implemented locally in the isolated Trial Review desktop viewer: both hands follow recorded controller grip anchors using deterministic two-bone IK; elbows/arm length adaptation remain illustrative. Controllers retain exact recorded transforms. Extreme reach is bounded and disclosed; invalid tracking resets the affected arm.
- Added default eye-origin gaze display and separate Raw gaze toggle. The display preserves recorded direction and discloses raw-origin distance; no recorded samples, hit IDs, or metrics change. P020 at 30 s has about 1.52 m raw-head origin separation, requiring a separate coordinate audit.
- Built and visually inspected Trial Review 1.4 on P020. New Unity checks pass for both hands, grip orientation/position, limits, rewind, gaps, invalid tracking, eye origin/direction and raw preservation. Existing replay/audio/CSV checks, 98 real camera samples and three Python preparation checks pass.
- Updated P20 launchers. Main study Assets, Unity session and headset build were not changed by this replay work. Finger gripping and participant-specific body proportions remain illustrative/untracked.

## 2026-10-08 — immediate vertical arrival and controller/gaze confirmation

- **Implemented locally:** area arrival includes immediate up/down refinement in one prepared clip; pointing confirmation requires an exact right-controller target hit plus valid gaze in an expanded circular angular region. Fresh controller telemetry and audio-fade revalidation prevent stale confirmations; stable-entry/latching avoid continuous repeats.
- Added development-only admin wrist controls for the target vicinity ring and 0–20° extra radius (1° steps, default 6°). Study sessions hide the ring and retain the default; selection colliders and raw gaze remain unchanged. Added angular/ray evidence and admin-setting events to guidance decision snapshots.
- **Verification in progress:** the new phrase-library regression first failed for the missing arrival clip, then passed. Rotational layout, gaze policy/history and voice isolation scripts pass. Unity integration passed for angular vicinity, correct/distractor target hits, stale controller data, and development-only settings. A scoped inline correctness/reuse/quality/efficiency review kept the radius separate from selection/fixation data, removed redundant opening state, and aligned follow-up guidance with vertical-first arrival. Final foreground Unity integration passed, including rendered-ring geometry verification; inspected `/tmp/gaze-vicinity-ring-preview.png`. Development ring previews remain live while settings pause the trial, without advancing speech or guidance gates. Final Android Build And Run succeeded in 407.7 seconds (`/tmp/gaze-prompt-editor-20261008.log`); verified install timestamp, running PID 28816 and activity launch status ok on Vive FA5AR3N00385. Existing app data retained. Hardware tolerance validation remains open.

## 2026-10-08 — human, Vive controllers and swivel chair in replay (side chat)

- **Implemented locally:** CC0 Kenney human in third-person/overhead, seated on an illustrative swivel chair; official Focus 3 controller meshes from the installed SDK follow independent recorded controller poses. First person hides the body; tracking gaps hide unavailable models.
- Chair seat follows estimated torso yaw while recorded head rotation remains independent. A labeled display-only neck allowance/turn-rate estimate supports deterministic scrubbing; actual torso/chair motion was not recorded. Static hands do not imply tracked full-body pose.
- Trial Review 1.3 Mac build and new actor/torso checks passed, as did P020 pose/CSV checks and three isolation tests. Opened and visually verified P020 third person. Vendor notices/provenance included. No study/device modifications, commit or push.

## 2026-10-08 — standalone Trial Review desktop app (side chat)

- **Implemented locally:** separate macOS Unity app with first-person recorded-head reconstruction, orbit/overhead, saved audio, trial/event/sample navigation, timeline and decoded sample inspector, plus exact-hash-matched final performance/subjective CSV context. Reuses shared replay code in an isolated generated project; no live-study Assets/settings changed.
- Universal app built successfully; three isolation tests and Unity state/audio/CSV checks passed. Verified 98 P020 head poses across nine recorded trials and matching actual CSV joins. App launched with copied P020 package; truncated ending is prominently labeled. Native UI mouse automation was unreliable; hands-on controls/audio validation remains.
- **Open:** MP4 export, passthrough footage and embodied third-person avatars are outside this version. No device deployment, commit or push. See `work_log/2026-10-08-trial-review-desktop-side-chat.md` and `tools/trial-review/README.md`.

## 2026-10-08 — shared coaching-style study prompts

- **Implemented locally:** one `StudyPromptCard` resource/component supplies the original coaching Inter font, dark surface, rounded template sprite and shared button/text styling across voice setup, recording/loading/audio checks, start/readiness instructions, trial status and block/completion screens. Rewrote participant instructions as a brief heading plus an action. Survey item wording, scoring, hidden-target rules and gaze-cross stimulus remain unchanged.
- Removed the redundant original Coaching UI scene instance and its wrist-menu relaunch button; kept the shared style source prefab. Clarified that surface selection is obsolete: removed surface-tap copy from study setup; current eight-wall trials start through seated centering, without a surface tap. Checkpoint cards now also use the shared component. Preserved background ray blocking in the shared card.
- Original CoachingCardRoot sprite reference was missing in this recovered project; the shared card uses the surviving rounded sprite from the same MR template. Resource generation validates both sprite and font. Reused original screen actions/layouts, right-ray trigger confirmation, haptic hover, six avatars and selection borders.
- **Verification:** first full Unity integration and ray-menu checks passed; inspected rendered voice menus and survey. Corrected underlying participant-status text showing through survey headers. Final ray-menu and full study-integration checks passed (`/tmp/gaze-prompt-final-ui-20261008.log`, `/tmp/gaze-prompt-final-integration-20261008.log`); rendered setup/voice/survey/completion cards inspected. Final ray-menu checks also passed in the foreground Editor after preserving card ray blocking. Final Android Build And Run succeeded (233.4 seconds; `/tmp/gaze-prompt-editor-20261008.log`), installed and launched on Vive FA5AR3N00385 (PID 20376; activity status ok). Existing data retained; wearer check of comfort/readability remains. Scoped inline reuse/quality/efficiency review removed obsolete color parameters; review stayed within prompt edits, preserving pre-existing research/runtime/analysis work.

## 2026-10-08 — clarified trial measurements and NASA-TLX scoring (side chat)

- **Implemented locally:** v2 offline export uses final correct selection timing, unique zones plus stable transitions/revisits, and planned target height relative to the recorded readiness-cross/plane midpoint. Valid gaze and source-aware continuity gate observed zone counts; debounce is a pilot export policy, not fixation validation. Removed the proposed incomplete-trial pause calculation.
- Added explicit six-dimension raw NASA-TLX mean (0–100), validated scale/direction metadata, corrected legacy totals and aligned subscale plots while preserving raw responses. Historical ambiguous surveys remain unscored.
- Full analysis suite passed (178 tests); independent reviews completed and synthetic workbook/CSV package refreshed. **Open:** headset validation, runtime survey endpoint labels and scale metadata, and any height-balancing quotas. No Unity/device/build, commit or push actions. Handoff: `work_log/2026-10-08-trial-demo-export-side-chat.md`.

## 2026-10-08 — readiness cross rejected valid HTC eye data

- **Diagnosed:** DEV_NEUTRAL run 002 never charged because EXT `isTracked` remained false; 4,264/4,999 inspected rows had at least one valid native eye, including 4,235 binocular rows. Native-binocular and EXT directions differed by a median 0.36 degrees; this agreement is not independent accuracy validation. The later headset-off rows correctly showed tracking loss.
- **Implemented locally:** require fresh, explicitly valid binocular HTC rays plus tracked head/focus for a labeled derived-ray fallback when EXT is untracked. Retain raw EXT flags/poses; no fabricated validity, head fallback, loosened charge threshold or rate claim. Added regression for the observed flag disagreement, blinks, monocular loss, stale sources, absent tracking parent/head, invalid rays and preservation of valid EXT.
- Red regression reproduced the bug in fresh Unity (`/tmp/gaze-native-red-20261008.log`). Final full Unity integration passed (`/tmp/gaze-native-final-check-20261008.log`) and focused gaze policy/history checks passed. Also separated offline raw EXT spatial/event/offset analysis from valid native-derived rows, with explicit source labeling/counts; preserved CSV/replay and used-ray availability. Native derived-ray accuracy and eye-event validation remain open. Targeted manual review kept unrelated branch/side-chat edits out of scope. All 179 analysis tests passed, including the source-separation regression. APK build succeeded (207.9 seconds; `/tmp/gaze-native-build-20261008.log`), installed with `adb install -r`, and cold-launched successfully on Vive FA5AR3N00385. Unity reopened, existing data retained. Physical cross charging and independent known-target accuracy still require the wearer.

## 2026-10-08 — trial demo sheets and separate motion export (side chat)

- **Implemented locally:** added optional per-trial/combined motion CSV export, explicit synthetic/development exclusion labels, and pre-read/final source fingerprint checks. Added reproducible three-case synthetic demo sheets, block survey examples and a full trial-column dictionary. Raw replay remains necessary for object/event/audio reconstruction.
- Two independent reviewers checked joins, missingness, source provenance and all 156 example motion records. Fixed source-change and symlinked-survey regressions. Full analysis suite passed (162 tests); workbook inspected and rendered successfully.
- **Open:** fresh headset export validation, final-zone/zone-count definitions, signed-height quota reference, and downstream filtering of development data. No Unity/device/build, commit or push actions here. See `work_log/2026-10-08-trial-demo-export-side-chat.md` for files, artifacts and main-chat handoff.

## 2026-10-08 — quick-test first-trial readiness fix

- Device run `DEV_NEUTRAL/run_001` stopped with `readiness_start_wall_mismatch` before search (0 seconds): the skipped practice sequence left the actual start at wall 0 while the saved schedule expected wall 3. Preserved the stopped run.
- **Implemented:** quick-test schedules now anchor to the centered forward wall 0, with a distinct `quick-origin0-v1` schedule suffix and mode-aware cache invalidation. Normal study schedules still continue from the final practice target. All planned signed thetas and subsequent previous-target transitions remain unchanged.
- Reproduced the defect with a failing regression, then passed the fixed schedule/voice tests, including same-ID mode switches. Added a Unity regression exercising the real readiness coroutine for all 40 quick-test transitions. Fresh Unity integration passed, including the actual readiness coroutine for all 40 quick-test transitions and a forward first cross. APK build succeeded (706.5 seconds), `adb install -r` succeeded, and cold launch returned `Status: ok` on Vive FA5AR3N00385. Existing data/audio retained. Verification/build logs: `/tmp/gaze-quick-origin-check-20261008.log` and `/tmp/gaze-quick-origin-build-20261008.log`. Cleared only unused LMDB lock files (preserved under `/tmp/gaze-unity-stale-locks-20261008-1791505598`) to recover a stalled Editor startup; asset databases remained intact.

## 2026-10-08 — restore neutral voice configuration and relaunch

- Diagnosed device failure: the recovered checkout had no ignored `Assets/StreamingAssets/api_keys.json`, leaving neutral synthesis without a provider credential. Linked the original project's existing local file into this checkout; the source remains unchanged, and the link is Git-ignored. No credential values were printed or committed.
- Verified actual Eric speech synthesis for “The object is to the left.” with HTTP 200. Rebuilt and ran on the connected Vive successfully (113.5-second incremental build). Fresh device process 14532 reports `[VoiceAssist] Loaded API keys` and `Ready. Key=present`. Existing app data remains intact; Unity stays open.

## 2026-10-08 — deploy neutral quick test to Vive

- Corrected the Android build helper to set both the selected build-target group and BuildPlayer target group to Android. The installed OpenXR validator reads the selected group; stale desktop selection incorrectly rejected the already-enabled Android Composition Layers feature. No XR feature settings were changed.
- Vive Focus Vision `FA5AR3N00385` is connected and authorized. Android APK built successfully in 572 seconds; `adb install -r` succeeded and Android launch returned `Status: ok` with the app process running (PID 12770). Existing app data was preserved. The headset was asleep at launch; sent a wake event and reissued launch successfully to the existing top-most app. Headset interaction still needs wearing/waking the Vive. Build log: `/tmp/gaze-neutral-quick-build-20261008.log`.

## 2026-10-08 — neutral quick-test shortcut

- **Implemented:** added a right-controller ray button on the first setup screen to enter a session-only neutral development mode. Eric's neutral library is prepared without clone enrollment; study intro, audio acceptance and practice are skipped, while centering, trial readiness, guidance and loggers remain active.
- Runs contain one 40-trial block (or the existing shorter debug override), with matching schedule flags. `DEV_NEUTRAL`, the run-folder prefix, session-mode sidecar and trial-summary mode distinguish test data. New-participant reset restores normal study behavior. Audio preparation failures stay blocked with a neutral retry/cancel path.
- Verification: focused voice checks pass for neutral-only preparation, missing clone, development identity, schedule and reset. Unity offline study integration passed, including the start-screen ray button, unarmed/no-audio guard and blocked quick-test failure path. Required Build And Run was dispatched but failed the existing OpenXR Composition Layers Support validation; no APK was deployed and no Vive is connected. Unity remains open. No paid/provider calls were made during development.

## 2026-10-08 — initial target direction and gaze-history return cues

- Implemented the first directional cue as “The object is to the left/right,” with truthful above/below/ahead cases when already horizontally aligned. Practice/new trials reset the opening; same-trial resume does not. Both voices prepare all 53 guidance phrases.
- Added a bounded three-second target-relative gaze history with source IDs/timestamps and same-wall horizontal/vertical passage detection. Invalid/stale gaps, duplicate deliveries, device/recording changes and pauses cannot manufacture a crossing. Direction-specific return cues revalidate before interruption; frozen history is retained in guidance-decision telemetry. Thresholds are pilot settings, not validated fixation or perception evidence.
- Focused gaze-history, rotational-layout and voice-isolation checks pass; all 156 Python analysis tests pass. Unity 6000.3.10f1 offline study integration passed, including opening-cue trial/resume lifecycle and history serialization (`/tmp/gaze-target-history-unity-verified-20261008.log`). No provider audio was requested during development.
- Opened the recovered project in Unity and dispatched the required Build And Run; Android preprocessing failed OpenXR validation because Composition Layers Support is required by the installed Composition Layers package (`/tmp/gaze-target-history-editor-final-20261008.log`). No APK was deployed. No Vive is connected; headset behavior remains unverified. Unity remains open.

## 2026-10-07 — recover the saved study implementation

- Restored tracked source at `8da2eea` into the managed `gaze-recovered` worktree on `codex/recovered-study-20261007`; original edits remain untouched. The two older missing protocol/material checkpoints are already included, and no extra staged changes remain in their missing worktree indexes.
- Re-ran all 156 analysis tests successfully and verified shared Python environment helpers. Preserved surviving P020 preview HTML and embedded nine-trial data with hashes as derived, incomplete evidence. Original raw P020 files, generated results and APK were not found in checked local locations; headset copies remain unchecked because no device is connected.
- Documented voice hint preparation, local geometry/phrase selection, cache playback, interruption and timing. Identified the stale self-similar `FirstPerson` audio metadata label for follow-up; no runtime logic changed. No build, deployment, paid audio calls or push. See `docs/overnight/2026-10-07-recovery.md`.

## 2026-10-01 — overnight continuation

- 06:00 EDT: overnight work window complete. Final handoff reconciled: approved three-script restore is complete; #11/#23/#24 issue bodies read back exactly with remaining device/research gates and In Progress status. Corrected phrase inventory to 44 guidance variants plus four success responses. Current APK fingerprint remains verified; Build And Run was dispatched, but no terminal result or connected Vive is present. No push or headset acceptance claim.

- Re-exported the original incomplete P020 package under the new motion method: 22 stable measured/practice IDs, one row per file, eight changed motion rows and matching source hashes; local ignored comparison artifacts retain warnings and leave originals unchanged. Added explicit three-directory (`GazeData`, `EyeMeasurements`, `GazeReplays`) transfer instructions because the older analysis pull fetches only trial data. No device copy was performed.

- Reproduced omitted trigger-time motion in trial CSVs (synthetic path 1.0 m exported instead of the observed 1.4 m). Motion method v2 now includes tracked correct/incorrect selection poses, preserves invalid/gap/pause breaks, excludes post-capture movement and identifies a selection-only geometry reference. Frame gaze-quality denominators remain unchanged. Four regressions and all 156 Python tests pass; new method/count fields preserve export provenance.

- Extended strict import to shared replay parsing after reproducing a textual `complete: "false"` footer accepted without warning. Reject non-finite JSON and malformed declared schema/sequence/time, booleans and tracking states before downstream metrics. All 152 Python tests pass; P020 retains exactly 10,119 records / 9,703 samples with its existing incomplete-prefix warnings. Corrected #20 documentation: repeat request/delivery counting is implemented; zone-arrival/revisit definitions remain open.

- Reproduced a raw-eye import false pass: JSON `valid: "false"` was treated as true and yielded 100% valid time; a complete receipt without a row count was also accepted. Added strict declared-boolean/numeric identity checks and typed clean-close row counts, preserving missing-count receipts as incomplete. All 151 Python tests pass, including the combined audit. Original raw data remains unchanged.

- Added `analysis/audit_recording.py` to run session, exact raw/replay/audio links, guidance and eye-quality validators from explicit local inputs. It retains failures and incomplete evidence, source hashes/change checks, command logs and declared short/full-session context; seven real-command regressions pass and the full Python suite now passes 149 tests. No hardware, survey or acoustic acceptance is inferred. Corrected the Unity refocus helper's misleading blanket permission advice for a locked/unavailable menu.

- Audited HTC requested-pose time and Khronos EXT expressed-pose time against the primary specifications and installed SDK. Documented the proposed native diagnostic route, its limits and connected-device evidence matrix; corrected the measurement plan's stale pre-implementation/current-state wording. Native capture time/sequence and true 120 Hz remain unknown, not unlocked by a timestamp field.

- Added an original editable setup/session schematic (SVG, vector PDF, PNG, caption/alt text and builder) for #30, with 40+40, separate practice, hidden target and right-controller selection. Replaced stale table/dwell/old-count runtime and octagonal guides; historical generated artifacts are explicitly labeled stale and preserved. Removed the unsupported blanket accent claim from the run sheet; native compilation passes. Figure rendered and inspected; final venue sizing, protocol ordering and hardware/research acceptance remain open.

- Verified the actual incomplete P020 source in both replay viewers: all 44 sampled/event cursor comparisons agree on original sample fields, source lines, freshness and 168 object positions. Original bytes remain unchanged; desktop/mobile pages were inspected. Fixed browser event jumps to preserve sequence boundaries, including multiple events at the same timestamp; both browser suites pass. P020 lacks the current gaze-link contract and matching video, so this is historical reconstruction evidence only.

- Native full-session replay now seeks from saved trial snapshots while retaining absolute source lines. Full-prefix equivalence passed across 80 trials/exact boundaries/random rewinds. In the 652 MB synthetic fixture, 240 late/mid/early seeks improved from p95 111.6 ms to 1.35 ms; no GUI/headset performance claim. Reproduced and fixed missing selection-time object movements in both capture and reconstruction; full Unity integration passed. Saved reproducible benchmark evidence.

- Reconciled the existing survey references with 40+40 trials, separate practice, six choices per gender and hidden-target shared guidance. Added separately labeled draft reliance/use-percentage items and clarified neutral debrief probes, timing and response provenance. Both native LaTeX compilations pass; final layout, anchors/burden and capture/coding choices remain researcher review. Updated/read back #26; no live survey or import restoration.

- Reworked native replay audio to preserve overlapping success chimes and speech, per-event volume for reused clips, natural/recorded stop limits and seek offsets. Playback owns disposable preview-scene sources; malformed WAV lengths/rates and invalid playback volumes are rejected. Full Unity integration and the silent real-engine check pass, including independent sources, seeking, cleanup and unchanged study-scene state. Acoustic listening remains a morning check.

- Native Unity replay now gates poses/highlights on freshness and known tracking, clears prior poses at phase boundaries, labels recorded-head fallback, steps exact equal-time samples and exports source-linked cursor snapshots. Full Unity integration and a graphical 168-object render passed; browser phase-boundary regression passed. Source records remain inspectable through gaps. Real headset comparison and interactive rehearsal remain open.

- Full-session timeline stress checks reproduced a JSON parsing failure in the old 570 MB single-payload page. Split raw-import HTML into per-trial payloads, parse only the selected trial, preserve all source observations, publish atomically and label headset/editor/synthetic/unknown origin. An 80 × 60-second synthetic run (432,000 samples, 168 objects/trial, 758 MB HTML) passed all trial-identity checks; observed load 2.68 s, p95 switch 34 ms and scrub 2.4 ms on this Mac. All 142 Python tests and both browser suites pass. Saved reproducible scripts and measurement evidence; hardware/video comparison remains open.

- Added direct replay JSONL import to the browser timeline (#37), exact source/sample inspection and stepping, recorded object transformations/visibility, pause-aware search lanes, stale/invalid pose handling, event/object inspection and cursor JSON export. Optional trial-summary joins require exact replay hash and identity and keep final/survey context separate. All 139 Python tests and both Chromium suites pass, covering synthetic 80-trial context, duplicate timestamps, backwards seeking and existing local-video controls; full-session browser performance and actual headset/video comparison remain morning checks. No runtime/APK changes in this unit.

- Extended recording-links v2 with referenced PCM16 audio validation, missing-clip/recorder-failure findings, bounded local asset reads, traversal/symlink rejection, source hashes and reusable-clip event inventories. All 130 Python tests pass, including corrupt header/length/rate cases, path protections, duplicate references and CLI mixed incomplete-report output. No participant audio was uploaded or regenerated; listening, actual headset playback and video validation remain open.

- Advanced #16 with a reproducible, explicitly hypothetical 40+40 voice sensitivity package: participant-level order-adjusted log-time contrasts, trial correlation/geometry, heterogeneous voice effects, random/informative loss, paired benchmarks and recruitment assurance. Saved 120 scenarios × 2,000 simulations, tables/figure/configuration/hashes. All 123 Python tests pass. Final primary outcome, effect threshold, model/missingness rules and enrollment remain researcher decisions; no participant data or protocol approval used.

- Added exact replay-to-raw gaze recording identity on every sampled/selected observation and a versioned link contract. New recording-links audit checks run/trial/voice/object/selection consistency, timestamps and world/local coordinate transforms; preserves invalid/legacy/incomplete evidence and hashes explicitly supplied inputs. Real production-header/reopened-stream Unity tests and all 115 Python tests pass. Fresh Android APK packaged successfully in 84.3 s (71,125,376 bytes); device installation/measurement validation remain pending. Updated replay documentation to the current 40+40/shared-command/right-only flow.

- 02:08 EDT: Android APK rebuilt successfully with the completed-trial preservation fix (269.7 s; 71,124,916 bytes; fingerprint in morning report). Updated current system overview/study hub to remove obsolete dwell/shelf guidance; current overview/QA links pass. No device deployment.

- 02:03 EDT: saved validator/QA fixes in `d9a8aa1` (103 Python tests). Reproduced stop-during-success corruption in actual Unity summary output, then preserved completed trial outcome/duration while retaining the session technical stop. Regression covers rounds 0, 39 and 79 plus unfinished controls; full Unity integration passed (`/tmp/gaze-unity-stop-integrity.log`). New APK rebuild running; previous package predates this fix.

- 01:59 EDT: reproduced and fixed session-validator false passes for unfinished/duplicate trials, contradictory outcomes, NaN/missing/negative timing and impossible pause/resume sequences. Readiness pauses no longer distort active duration. Interrupted trials retain timing/audio checks. Full current 80-trial packages pass in both voice orders; all 103 Python tests pass (`/tmp/gaze-python-session-integrity-final.log`). Found a separate runtime stop-after-capture outcome edge for follow-up.

- 01:53 EDT: launched the correct graphical Unity project. Refocus/Build And Run helper was attempted and exited 1 because no Unity menu bar was available while the Mac was locked; ADB lists no devices. Offline APK remains valid, but no device deployment is claimed. Replaced stale QA instructions (14 trials, shortcuts, countdown, distinct wording) with the current 40+40/right-ray/shared-guidance and progressive audio workflow.

- 01:50 EDT: saved the reviewed runtime, controller, voice, replay and validator changes in local commit `77f85f6`; the Git compile hook completed a fresh successful Unity compile. No push. Scoped commit excludes all unrelated deletions and Unity-generated ProjectSettings change. Task #27 update read back successfully.

- 01:50 EDT: reconciled the open run sheet with 40+40 measured trials, two separate practices, hidden target, shared guidance, right-controller controls and six voices per gender. Native LaTeX compilation succeeded. Added an explicit current/historical boundary to the procedure register; height balancing, hardware validation, final survey/interview/consent details and live publication remain pending.

- GitHub #34 moved to **Ready for headset QA** and read back in Project 2; only the completed software checklist items were checked. The issue remains open/unverified. Test the complete right-controller-only flow with the left controller off; exact steps are in the updated issue.

- 01:38 EDT: saved schedule-aware descriptive voice comparisons and unavailable-blink preservation in local commit `932f516`. The old hardcoded 14-trial pairing now supports the saved 40+40 schedule, rejects short/debug/restarted/malformed pairs and retains historical 7+7 support. All 95 Python tests passed. Fresh Android APK with the diagnostic panel fix built in 216.3 s (71,122,616 bytes); current SHA-256 is in the morning report. No device install/launch.

- 01:30 EDT: saved the seven analysis/report files in local commit `64d8325` (91 tests passing). Other index entries and unrelated deletions were excluded. Diagnostic panel clearance and full Unity integration passed (`/tmp/gaze-unity-diagnostic-clearance.log`); rebuilding Android with that fix. Native rate and headset checks remain open.

- 01:28 EDT: added per-phase delivery/capability/clock tables and timing figures; inspected a labeled synthetic outage plot. All 91 Python tests pass. HTC query counters deduplicate copied fields by host query time, and native-rate/loss conclusions remain unknown. Found and repaired diagnostic panel occlusion of right-side fit dots; full Unity geometry/integration verification is running. This small runtime change requires another APK before handoff.

- 01:18 EDT: Android packaging succeeded (305.2 s; current APK 71,124,672 bytes, SHA-256 in the morning report), no device install. Added offline exact-observation guidance audit and ten regressions for joins, two clock origins, invalid/stale claims, revalidation, cancellation, phrase mismatches, legacy/incomplete recordings and CLI safety. GitHub #15/#22/#24 reflect the implementation and remaining hardware checks.

- 01:07 EDT: added guidance decision/source-observation audit linkage, correction commitment/abandonment and correlated software playback events. Immutable snapshot, replay round-trip and voice lifecycle tests passed; full Unity integration passed (`/tmp/gaze-unity-guidance-provenance-final.log`). Python quality CLI regression brings the suite to 72 passing tests. New APK packaging is running; no headset validation claimed.

- 00:57 EDT: completed sequential self-review artifact (no independent peer credit), retaining two concrete measurement-plan gaps. Implemented per-trial search-only quality CSV and per-target direction bias/angular spread/time missingness. Invalid heartbeats split precision segments. All 71 Python tests passed; guidance decision provenance is the next remaining review item. Hardware acceptance is unchanged.

- Persistent Android APK packaged successfully (280.4 s, 71,123,184-byte APK); no device installed. Subsequent settings lifecycle repair prevents readiness/search running behind already-open Settings; ordered pause flush preserves the last eye/replay rows without blocking on storage. File-level stalled-writer and setup→readiness/search→Continue regressions passed in the full Unity suite (`/tmp/gaze-unity-pause-durability.log`). Rebuild required after review. Marked old beta-menu documentation explicitly historical and linked current behavior.


- Android packaging succeeded in 676.4 seconds (1145.9 MB reported), no device deployment. Found Temp output disappears at editor exit; switched helper to ignored persistent `Builds/CodexAndroid/` for next build. Expanded catalog to six per gender by appending Liam/Laura, preserving previous indices; all twelve cards now have fictional portraits, eight generated with built-in image tool and saved with prompt provenance. Catalog inventory snapshots are versioned per participant/run.
- Fixed shared eye/CSV trial linkage across capture/feedback and 39→40/79→80: a presented-trial context stays stable until the next actual transition. Practice remains separate; replay's voice routing is independent of listener order. Canonical Unity integration including new context fixture passed, pure voice/cache/saved-choice tests passed; both six-card graphical checks passed and were visually inspected. Latest full Unity integration passed (`/tmp/gaze-unity-six-final.log`), and all 66 Python tests passed after fixing empty-context annotation matching and cross-device event/precision continuity. Rewrote the authoritative gaze guide to match the new pipeline and guaranteed distinct layout seeds with collision retry (schedule suite passed). Persistent APK packaging is running.


- Full 40+40 schedule and right-controller startup/modal-input regression passed canonical Unity checks; all 63 Python tests passed. Graphical voice-menu checks passed and preview inspected. Updated #10/#13/#28 and project overview with actual local schedule progress; height quotas and headset acceptance remain open.
- Added offline APK build entry point with explicit batch failure propagation. Android packaging started without AutoRunPlayer (`/tmp/gaze-android-overnight.log`); no headset attached. No participant data deleted, external audio requested, or study approval claimed.

## 2026-09-30 — overnight run (ends October 1 at 06:00)

- Late-evening continuation: implemented 40 measured trials per voice (80 total), eight per absolute angle, generalized stratified randomization, same target-identity counts across voices with no adjacent repeats, and explicit pending-height-policy metadata. Voice/schedule checks passed across 50 participant IDs and both voice orders. Updated replay fixture for the actual new block boundary; full Unity integration passed. Python suite now 63 tests, including versioned manifest geometry/quota/identity and summary consistency. GitHub evidence refreshed on 13 tasks; 12 relevant cards In Progress, #11 no longer deferred. Right-ray policy now recovers from hand-mode startup; latest integration rerun passed (`/tmp/gaze-unity-40-trials-final.log`).


- User authorized restoring the three required scripts and autonomous in-scope task work until 06:00. Restored the dependencies; migrated the gaze CSV logger to SharedGazeStream, preserved invalid gaps and unavailable eye events, and made re-enable append-safe. Added an actual Unity CSV integration fixture and downstream missing-blink regressions.
- Fresh canonical Unity batch compilation and gaze checks passed, replacing the earlier temporary dependency-fixture evidence. Full offline study integration checks passed (gaze/CSV, real physics ray filtering, trigger gates, readiness, boundaries, voice choices and replay). Python analysis: 54 tests passed. Device accuracy/rate, physical haptics and listening remain pending.
- Added right-ray-only settings/pause/repeat controls, disabled left/hand/gaze UI activation, and retained existing settings content via front docking. Added versioned requested/delivered repeat counts and non-repeat hint counts, final-success event retention, and raw-eye interrupted-prefix/identity/row-count validation. Latest analysis suite: 60 tests; updated Unity right-only integration suite passed. Physical UI/voice/haptics remain in the morning verification list.
- Fixed stale-lock handling in the compile helper and shared-imperative voice metadata. Working report and verification list: `docs/overnight/2026-10-01-morning-verification.md`. Continuing with right-controller-only access and other unblocked project tasks; no issue is closed on the strength of implementation alone.

## 2026-09-30

- Implemented a shared validity-aware EXT gaze adapter with independently flagged HTC eye/pupil/openness probes, raw/native/world coordinate records, input delivery timestamps, invalid/stale heartbeats and bounded continuous JSONL recording. Removed late visual X flipping; replay/readiness/guidance/trial intersection summaries now consume the shared ray.
- Added right-ray researcher eye diagnostics and separate 54-point spatial validation, nine-point offset-fit, independent held-out and five-point drift checks. Added raw CSV/spatial quality reports, held-out offset evaluation and explicitly unvalidated fixation/saccade/blink candidates with annotation comparison. Native 120 Hz and hardware accuracy remain unestablished.
- Implemented shared imperative conversational guidance, within-wall vertical cues, validity cancellation, hidden-target trial/readiness wording, `Loading...`, and chime/varied success speech with transition ordering. Preserved existing five-per-gender catalog and clone audio treatment.
- Verification: runtime C# compilation passed in an external fixture using Git versions of three pre-existing deleted legacy scripts; this is conditional compilation, not a successful build of the current checkout. Focused gaze, layout, voice isolation and enrollment checks and 7 eye-quality / 22 trial-CSV tests passed. All 50 analysis tests passed using the repository's shared Python environment after system Python lacked pandas; runtime and editor assemblies compiled in the temporary dependency fixture. See `docs/gaze-measurement-validation.md` for operation and hardware gates.
- Updated and read back GitHub #14/#35/#22/#15/#11/#23/#24/#12/#20 with local implementation evidence and explicit hardware/validation blockers; no issue closed. Full independent/code-review delivery gate and actual Unity Build And Run remain pending the canonical dependency repair.
- Pending: response to restoring GazeDataLogger/GazeHighlightManager/ShapeObjectFactory; the deleted scripts remain untouched. No ADB headset was connected. No hardware validity/rate claims, task completion, commit or push.

## 2026-09-30

- Saved researcher decisions in `docs/meeting-reconciliation-2026-09-30.md`: shared participant-directed imperative wording, more conversational/less silent contextual guidance, 6–8 voices per gender, `Loading...`, varied success chime/reinforcement, descriptive filenames and separate trial-performance/subjective versus motion/reconstruction data. Retained hidden-target, right-controller-only and 40+40 decisions.
- Updated the study hub and added precedence notes to the September 28 reconciliation, voice-choice baseline and measures register. Extra cloned-voice prosody/personality remains an open pilot proposal; natural recording instructions remain, and possible voice-identity/style confounding is explicit.
- Updated and read back 15 existing GitHub issue bodies (#7, #8, #11, #12, #16, #19, #20, #21, #22, #23, #24, #25, #27, #29, #33), preserving older history and task status. No new duplicate tasks or implementation/completion claims. Local link and whitespace checks passed. Runtime/LaTeX sources were not changed for this notes update; no build, commit or push.

## 2026-09-28

- Researched ten primary/methodological gaze references and saved `docs/plans/2026-09-28-vive-eye-gaze-measurement-plan.md`, with direct DOI/full-text links, method-transfer limits, current source audit and implementation/test gates mapped to #14/#35/#22/#8/#15/#20/#16.
- Confirmed HTC documents standalone Focus Vision per-eye support and nominal binocular 120 Hz; installed wrapper's predicted-display-time usage does not establish native sampling. Found active X-position flip in the study scene with Update/LateUpdate measurement-order risk; requires known-target validation, not an untested removal.
- Proposed shared validity/freshness-aware acquisition, independent spatial validation/drift checks, head-aware event analysis, horizontal-then-vertical assistance and separately validated workload measures. No gaze runtime, protocol thresholds, GitHub status or participant data changed. Existing voice/guidance edits preserved.

## 2026-09-28

- Replaced runtime Voxtral cloning/TTS with ElevenLabs instant voice cloning and `eleven_turbo_v2_5`; both conditions share the model, retaining natural recording instructions and clone-only treatment. New cache namespace prevents old Voxtral audio reuse; manifests explicitly record voice condition.
- Expanded selection to five male and five female voices, preserving original saved option positions. Authenticated catalog lookup verified the supplied local key and voice IDs (including the Janet alias); no participant sample was uploaded.
- Added production-client transport checks for multipart WAV/authentication, verification and malformed responses, access/quota errors and non-audio rejection. Expanded selection/isolation checks across ten voices.
- Live TTS returned HTTP 200/audio for all ten choices. Unity voice-choice and ray-menu checks passed, including five cards per gender. Enrollment/client, voice-isolation and layout tests pass; Unity and LaTeX compilation pass. Build And Run could not install because no Android device is connected.
- Updated session validation to use explicit voice condition and the saved neutral ID with legacy-manifest compatibility; recorded round/object counts replace outdated defaults where present.
- Updated runtime documentation and the run-sheet choices. Open: fresh headset enrollment/auditions, microphone clone quality, and matching cloud-provider consent/retention details. No approval or live cloning success implied.

## 2026-09-28

- Applied researcher clarification: retain the headset timing/progress/status panel while removing its target name/color content. Readiness status now refers to the agent, and wrong-selection timeout restores only generic status rather than a target label. Preserved survey controls, timers and trial progress; added assertions to existing ray-menu Editor checks. Browser timeline unchanged; full hidden-target conversion (including separate readiness labels and spoken identity) remains pending. Changes remain local. Unity live-editor compilation passed; Android Build And Run was attempted but no headset was connected. Editor UI regression assertions were added; their menu execution was unavailable in this session.

## 2026-09-28

- Began #24 follow-up after v3/main integration: expanded guidance to 32 source phrases with explicit self-similar first-person mappings; normal directional cue eligibility shortened from four to two seconds without overlapping speech. Added varied area/return phrasing without changing the existing relevance/debounce/cooldown rules. Expanded regression coverage for phrase inventories, cycling, direction preservation and first-person coverage. Layout/gaze and voice-isolation tests pass; hardware listening remains pending.
- Identified the headset bottom goal/status/timer panel in FindObjectUI and requested clarification against the browser timeline's bottom data/cue panel. Panel removal is pending the user's answer. These follow-up changes remain local and uncommitted.

## 2026-09-28

- Prepared all current Unity interaction/guidance, protocol/meeting/survey, per-trial CSV and timeline-video changes for the requested v3 then main integration. Both remote target histories are ancestors of the current work, so fast-forward integration is available without discarding commits. Participant recordings, credentials, generated analysis outputs and Unity performance-test build metadata remain local/ignored.
- Pre-integration checks passed: rotational layout/gaze entry-return regression suite; voice isolation suite; 32 CSV tests; 2 timeline packaging tests; prior real-browser timeline integration tests; Unity live-editor compile check. Android Build And Run was blocked by the missing headset; dismissed the device notice and verified Unity restored temporarily removed simulation settings. Device validation remains pending.

## 2026-09-28

- Added local headset-video support to the browser run timeline (#37): reusable page packager, run-scoped local video selection, manual recording-clock alignment, linked play/pause/speed/scrub/trial/event controls, missing coverage, media errors and downloadable/restorable alignment metadata. Preserved the original P020 preview and raw data; generated a separate updated local page. No recording or media upload started.
- Validation: two Python packaging tests and real Chromium integration checks passed with synthetic video, including trial switching, pause/speed, coverage edges, offset restore/wrong-run rejection and corrupt media recovery. Desktop/mobile screenshots inspected. Real P020 page loaded all 9 recorded trial snapshots and retained telemetry playback; actual P020 video remains unavailable/unverified. Manual offset is approximate and requires later-event drift checking; raw footage cannot be reconstructed from CSV alone. Changes remain local and uncommitted.
- Ran the requested Unity refocus/Build And Run shortcut; menu dispatch succeeded and the editor began building. Completion/device launch is not confirmed (latest log remained in Android prerequisite checks). The timeline feature itself runs in the browser; build-generated Unity files were left untouched.

## 2026-09-28

- Implemented the local post-session portion of GitHub #20 in `analysis/trial_csv.py`: one performance/subjective CSV per logical trial, combined table and source-hash manifest; strict replay/survey identity joins, scoped questionnaire enrichment, pause-aware timing, tracked contact snapshots, actual audio instruction counts, motion paths and realized geometry. Raw sources remain unchanged, missing measures remain blank, and practice/incomplete records remain explicit. Added usage/method documentation and regression tests; changes remain local and uncommitted.
- Validation: 32 CSV tests pass and Ruff checks pass. P020 exported 22 rows/files (20 planned measured + 2 practice; 6 measured completed), with each individual file verified against the combined table. Original incomplete recording/tail and missing surveys are flagged; participant artifacts remain under ignored analysis/results. No claims of validated fixation, native eye sampling frequency, finalized zone/repeat metrics or acceptance of the new 40+40 design. #20 stays In Progress pending definitions, finalized survey joins and fresh-session/device integration.
- Requested Unity refocus/Build And Run script was attempted; menu dispatch failed because System Events could not access Unity's menu bar (invalid index). No headset build or runtime validation completed for this Python export change.

## 2026-09-28

- Incorporated the researcher's whiteboard transcription: Euclidean distance, number of instructions, first eye/right-controller contact timing and final selection timing. Mapped these to separate distance/count/active-search latency fields, with actual-playback counting, partial/missing flags and final correct-trigger completion; detector details remain specified separately. Updated meeting/measures notes and GitHub #8/#19/#20/#21. Removed the small photographed To-do section from pending transcription, as requested. Planning/schema clarification only; no runtime, headset or survey change.

## 2026-09-28

- Incorporated researcher clarification: right controller only throughout setup/menus/wrist/pause/repeat and trigger selection; the agent alone knows target identity/location; guide horizontally to the correct vertical wall, then up/down within it. Updated the meeting record, procedure/measures/decision pointers and related GitHub requirements/project overview. Target identity and controller allocation are no longer open questions. Runtime implementation remains pending; target announcements/repeats/error speech and readiness dependencies need a coordinated change, not simply silencing the introductory clip.
- Cropped three unredacted whiteboard regions for researcher transcription: count-column heading after distance, timing headings between search/controller, and small To-do notes. Preserved original image/redactions and kept crops local. No headset build, runtime or live survey/Overleaf change for this clarification/task update.

## 2026-09-28

- Processed the supplied whiteboard and typed meeting notes into `docs/meeting-reconciliation-2026-09-28.md`, separating decisions, candidate schema/items, unreadable headings and unresolved wording. Updated 21 existing GitHub issues and project #2 overview without duplicate issues; activated #11 coarse-to-fine/vertical guidance (Ready), renamed #10 for 40-per-voice scheduling and #20 for one performance/subjective CSV per trial. No tasks marked Verified or closed.
- Captured 40+40 trials, five balanced absolute-theta strata, 20 positive/20 negative height deltas per block, separate reconstructable motion/scene streams, block-survey provenance, short varied guidance and success speech, exact loading copy, reliance/chair instruction and later passthrough introduction. Linked draft willingness/reliance/independent-motivation/use-percentage items to #26 and preserved the unfinished semantic-differential and controller notes as open. Target identity availability, vertical reference and turn/look threshold still require researcher clarification.
- Propagated current planning pointers to procedure, measures, angle schedule and decision register; preserved runtime evidence and older requirements as history. Runtime, headset build, live Qualtrics/Overleaf and generated IRB sources unchanged; this session reconciled planning/tasks only. Raw P020 recordings and the meeting screenshot were not uploaded. Current-design power remains unestablished (#16).

## 2026-09-27

- Recorded the researcher’s new forty-trial-per-voice decision (80 measured total; eight assignments per absolute angle per block) and guidance without a spoken target description. Checked GitHub #16 and legacy protocol: current-design power remains unfinished; the older 48-participant sensitivity statement concerns a superseded design. Flagged target identification within a wall as unresolved before removing announcements. No runtime, open run-sheet, or headset change in this decision audit.

## 2026-09-27

- Split P020’s mega CSV into separate gaze, replay-gaze, head/XR motion, controller, trial, event, selection, object, session and voice files. Added 29 exclusive record-type partitions retaining geometry and all original populated fields; verified exact reconstruction of every original row and column. Kept source exports intact, retained incomplete-session warnings and documented overlapping convenience views. Added an index/manifest and ZIP under ignored analysis/results/p20-20260927/separate-data; no Unity runtime change or device rebuild needed.

## 2026-09-27

- Generated a local interactive timeline from actual P020 run 001 data, with recorded layout, gaze/controller rays, trigger/hit snapshots, cue text and authoritative selections. Recovered two practice and six completed measured trials; trial 7 never reached search and the replay ends without a footer. Preserved original headset copies and documented the one truncated CSV row omitted only in a derived copy. Exported 21,089-row / 387-column combined CSV, including 7,376 exact frame joins. Unity accepted the original replay prefix; browser playback, event jumping, scrubbing and incomplete-trial display verified. Artifacts and participant recordings stay under ignored analysis/results/p20-20260927; no runtime code or headset state changed. Full 3D/audio replay remains available in the existing Unity viewer; inline overview is reduced for display.

## 2026-09-27

- Changed the spoken processing announcement to “Give us a moment while we process your voice.” Voice-isolation checks passed. Android Build And Run succeeded in 49.8 seconds and launched on Vive FA5AR3N00385; listening verification remains on device.

## 2026-09-27

- Added the standalone survey questions-by-block companion to Overleaf (`survey-questions-by-block.tex`, revision 0.1). Includes every baseline, post-block and final draft question/response option, all 42 Guo candidate ratings plus Q43, requested accent/voice preferences and similarity/liking items, and the proposed exploratory debrief. Maps administration to two counterbalanced ten-trial voice blocks, distinguishes existing drafts from proposals, and flags stale wording, applicability, duplicate items and scoring/scale decisions. Verified all source question text and IDs; built-in LaTeX compilation succeeded. Published only the new companion to Overleaf at `476eed1` and verified the remote bytes match. Live Qualtrics forms, run sheet, Unity code and headset workflow unchanged.

## 2026-09-27

- Replaced the audio-level check with the requested Accept Audio/controller/trigger instruction in both voices. Added a preloaded “Go back. That was the correct area.” cue and its first-person counterpart after a stable horizontal visit/exit, with a two-second relevance window and six-second cooldown. Canceled correction fades no longer consume cooldown; stable arrival stops stale directional hints even during area-cue cooldown. Setup and target announcements remain protected.
- Fixed the reported upward-scan reannouncement path: guidance area membership now ignores wall-height bounds and uses the horizontal wall sector. Nearly vertical gaze abstains rather than producing an exit. Object interaction and telemetry unchanged. Pure geometry/entry/return tests, voice-isolation/interruption checks, Unity compilation and Editor checks across all eight walls with +/-89-degree pitch passed. Android Build And Run succeeded in 73.9 s and launched on Vive FA5AR3N00385 (PID 11641); the initial Unity/Android error query was empty. Physical listening and edge-jitter acceptance remain open.

## 2026-09-27

- Side-conversation change: wrong controller selections now speak the actual target again in the active condition: neutral “Locate the blue cube.” / self-similar “I'm looking for the blue cube.” Existing wrong-selection visual/tone feedback and ongoing trial timing remain unchanged. Correction utterances have their own source wording and `wrong_selection` audio context so normal announcements retain their current wording and directional guidance cannot interrupt the correction. Included corrections in practice starter, full phrase inventory and ordered background preparation. Voice-isolation checks passed for exact wording, both provider paths and preserved condition. Headset playback remains unverified here; kept the main thread’s device workflow undisturbed.

## 2026-09-27

- Side-conversation change: removed the two-second initial directional-hint delay. OnNewObjective now makes the first hint eligible on the first valid search frame; target announcement/readiness, gaze validity, and speech-busy gates remain in place. Later directional hints retain four-second minimum spacing; target-area entry still uses its existing 0.1-second debounce. Updated telemetry guide and passed the rotational/directional policy checks. Device timing remains unverified; no build was launched from this side conversation to avoid disrupting the main thread’s headset workflow.

## 2026-09-27

- Side-conversation change: the study introduction now explicitly uses the selected neutral voice and external, participant-directed wording, regardless of the first block’s assigned condition. Pedro confirmed this applies only to the introduction; trial target announcements and hints still follow their voice condition. Added an optional per-utterance voice argument without changing session assignment. Voice-isolation regression passed for all four neutral identities, external wording, and preserved self-similar/enrollment state. Left the main thread’s active headset build/testing workflow undisturbed; installation of this small follow-up has not been verified here.

## 2026-09-27

- Added stronger “Look way left/right” hints (first person for self-similar) when the current horizontal gaze-to-object angle is strictly over 90°, interpreting two thetas as two 45° wall steps. Uses actual target bearing; balanced trial theta quotas are unchanged. Included both lines in audio preparation, updated wording/policy versions, and passed threshold, wraparound, invalid gaze, near/far phrasing and voice-isolation checks.
- Removed “I’ll try looking right” from the self-similar hint rotation at Pedro’s request. Self-similar right hints now cycle three options; neutral/right and left options remain unchanged. Legacy source wording resolves to an allowed first-person line. Updated wording/policy versions and passed the rotational and voice-isolation checks.
- Changed the avatar picker to controller-hover voice audition, trigger selection with a persistent gold border, and a Back-to-Confirm button on the same screen. Hover does not mutate the selected or committed voice. Previews capture their own provider identity, cancel prior audio, and enable confirmation only after a successful completed sample of the chosen voice. Stable cards avoid hover restart loops; reentry retries, gender changes reset selection, and focus loss cancels previews. The updated ray-menu regression first failed against the old separate-preview screen, then passed after implementation. Voice-isolation checks also passed for all four explicit audition identities, unchanged session voice, completion timing and failed samples. Unity voice-choice and ray-menu checks passed; inspected the rendered picker with a persistent gold border and disabled Confirm for unavailable audio. Review ran inline per the project’s no-subagent instruction; no remaining in-scope correctness findings. Final combined Android Build And Run succeeded in 75.3 s and launched on Vive FA5AR3N00385 (PID 8872). Initial Unity/Android error query was empty. Physical hover/listening, selection confirmation and far-turn hint acceptance remain for headset testing.

## 2026-09-27

- Renamed the existing wrist Settings degree-guide control to **Guidance boundary**, retaining one toggle for all eight wall outlines and degree labels. Added shared ray hover/haptics and synchronized the toggle with public visibility changes and menu reopening. Visibility continues across trials/recreated frames within the session; objects, search zones and gaze guidance remain active when guides are hidden. Moved the control to the top of Settings. Unity compiled and GuidanceBoundaryChecks passed (eight walls/labels, pre-spawn preference, on/off, reopened/API synchronization, ray feedback attachment and unchanged zone geometry). Android Build And Run succeeded in 60.0 s and launched on Vive FA5AR3N00385 (PID 4163). Startup logs reported unsupported lip tracking, a null plane subsystem, and VIVE plane detection timeout; no boundary-toggle exception was observed. Physical wrist targeting, label readability and haptic feel remain for headset acceptance.

## 2026-09-27

- Added Zimmerer, Bartels and Latoschik (published IEEE ISMAR 2025) as the rationale for IMI in the survey inventory and measures register. Preserved the proposed four-subscale battery and distinguished methodological precedent from a predicted voice effect. Survey revision 0.2 compiled successfully and synced to Overleaf at `abaace7`; updated the shared sync baseline.

- Fixed the approved Unity compile-hook environment leak: only the Unity subprocess clears inherited repository/index/worktree/object-store/config Git variables; parent Git context is retained. New isolated fake-Unity regression tests first reproduced the leak, then passed clean-environment and nonzero-exit propagation checks; Bash syntax validation passed.
- Imported approved `eb477b0` soft-close v2 from `origin/v3`, rebasing the saved mega-CSV and ray-menu/voice-choice/stratified-schedule work on top. Resolved the prior DSP-removal conflicts by retaining the upstream treatment unchanged, stereo clip replacement, speech-window RMS, manifest fields and DSP tests while preserving all newer setup and trial features. Pedro confirmed natural-voice enrollment instructions will remain unchanged and approved the hook environment fix.
- Combined voice-isolation checks passed, including new exact manifest window checks (full neutral clip / clone words excluding its 0.4-second tail). Unity compile and Editor ray-menu, voice-choice and replay checks passed. Android Build And Run succeeded in 66.2 s and launched on Vive FA5AR3N00385 (PID 30510); initial Unity/Android error check was empty. Hardware listening, interruption-tail acceptance and preparation timing require a real setup run. The latest of 14 device voice manifests predates this install (15:02:05 UTC) and still contains 61 dry clips without speech_samples; fresh on-device manifest validation remains pending, not passed.

## 2026-09-27

- Replaced the self-similar inner-thought treatment with `inner-thought-v2-soft-close`, chosen by listening from six prototypes built on Voxtral clones of the neutral voice. It applies close-mic EQ, gentle 2.5:1 levelling and a -24 dB dark stereo room with a 0.4 s tail. Words start at sample 0; neutral bypass stays exact. VoiceSynthesizer now swaps in the stereo clip and level-matches over the words only (`speech_samples` in the manifest). Voice-isolation checks pass. The C# port matches the prototype within 0.5 dB. Open protocol decision: the selected sound assumed a soft enrollment reading, but the run sheet still says "natural voice"; instructions are unchanged. Headset listening and on-device timing are pending. See docs/inner-thought-voice.md.

- Updated `agents.md` here and in the side-conversation worktree to always include direct links for reported research papers, plus accessible full-text/PDF links when available.

- Uploaded the survey inventory to Overleaf and verified its rendered preview. Confirmed the researcher-created dedicated Overleaf Git clone is clean and its survey matches the local file byte-for-byte at `f397ee4`. Recorded mapping/common baseline and conflict-aware sync steps in `docs/protocol/overleaf/SURVEY-SYNC.md`. Subsequent noninteractive Git fetch requires authentication; token persistence remains a user-local step. No runtime or run-sheet files changed.

- Implemented controller-ray buttons across voice setup, audio checks/recovery, centering, checkpoints, survey submission and completion; shared blue hover feedback and one pulse on the hovering controller, including existing wrist controls. Removed global menu shortcuts and clone-page keyboard suffix. Added four generated fictional avatar cards, retained both neutral options per gender and dry self-similar voice.
- Kept beta at 10 measured trials per voice (20 total, two separate practices). Schedule v4 stratifies each block into shuffled five-trial halves, each covering all absolute theta categories; preserves mirrored pairs, random target heights, seeds and full schedule logs. Updated run sheet and angle documentation.
- Added three left/right alternatives per original phrase with first-person equivalents and startup preloading; retained correct-area interruption and stale-direction cancellation. Voice/layout/enrollment suites and Editor ray-menu/voice/readiness/controller checks passed; run sheet compiled. Android Build And Run succeeded (92.5 s); installed and launched on Focus Vision FA5AR3N00385, PID 29279; initial error-log check was empty. Physical hover/haptic, held-trigger transitions and full 10+10 beta validation remain open.

## 2026-09-27

- Created standalone `docs/protocol/overleaf/survey-instruments.tex` in the survey side conversation: current survey inventory, source-paper bibliography, administration/scoring notes, custom voice/debrief questions, unresolved adaptations and burden, and a manual Overleaf round-trip workflow. The desktop LaTeX compiler successfully compiled the initial document. This is a planning reference; live Overleaf/Qualtrics contents and participant forms were not changed.

- Added the requested post-session mega-CSV exporter while retaining separate recordings: exact trial/frame gaze+replay joins, all source columns, trial/schedule/target context and scoped survey repetition, object/mesh/event rows, voice manifests, explicit unmatched/missing data and a provenance/count manifest. Ten synthetic regression tests pass. No saved measured-run summary was present on the connected headset, so a real session export remains pending. Added GitHub project Backlog #36 (reconstruct a saved trial and rerun it interactively) and #37 (timeline playback with synchronized data snapshots); linked #28 and the two follow-ups. Updated #20 with exporter progress and remaining real-session validation. Generated a clearly labeled synthetic CSV example; final Build And Run succeeded in 21.3 seconds and the Vive process was verified. Requested a complete two-practice plus 10+10 beta session for real export/replay reconciliation.

- Replaced the rejected inner-thought DSP with a gender-first neutral voice choice: Eric/Roger or Janet/Sarah, followed by a sample and explicit confirmation; switch voices or return to gender before enrollment. Verified provider metadata (the legacy Rachel endpoint currently resolves to Janet). Both conditions now use dry audio. Saved exact choices with legacy gender-only migration, preserve voice-block order on re-selection, clear old preparation/prefetch state on change, and record voice name/ID/option in readiness and summary output. Voice isolation/routing/cache/persistence checks and the Unity voice-choice UI-state check pass; the updated open run sheet compiles. Android Build And Run succeeded in 76.3 seconds, installed and launched on the connected Vive (process verified); physical audition and end-to-end enrollment acceptance remain pending.

- Committed all accumulated study, replay and protocol changes as `d9868a7` and pushed the checkpoint to `origin/v3`. Added the requested self-similar-only inner-thought voice pilot: softened high frequencies and quiet 24/52 ms reflections, applied to PCM before existing level matching; neutral bypass is exact. Raw caches remain untouched, replay saves the treated clip, and manifests/playback events record the effect version. Generated a local dry/treated preview from a cached clone phrase; both are 2.2204 seconds and RMS 0.1000, with safe peaks. Voice-isolation/DSP and level-matching checks pass. Android Build And Run succeeded in 47.9 seconds; installed and launched on the Vive (process verified). Physical listening acceptance remains pending. See docs/inner-thought-voice.md; the treatment is a pilot manipulation awaiting perceptual/protocol review.

- Implemented the selective trial replay port from `origin/v2-app-demo`: current stimulus snapshots, head/gaze and both controller poses/rays/triggers, authoritative controller-selection events, voice playback assets, and isolated Unity playback. Removed dependencies on object `DwellTarget`/`DwellProgress`; new schema keeps legacy object dwell at zero while reading old recordings. Added bounded background writing, incomplete-file recovery and failure isolation. Video export remains deferred; telemetry cadence is render-frame based, not native 120 Hz.

- Integrated the requested world-fixed, previous-target-wall readiness cross: configurable one-second continuous valid gaze, generous whole-cross bounds and left-to-right fill; gaze loss, tracking loss and pauses reset charge. Both charge and target announcement must finish before objects/search timing begin. Changed theta to signed shortest turn from the previous wall, retaining two each of absolute 0/45/90/135/180 per ten-trial block, mirrored relative directions and independent seeded order, including practice and block boundaries. Added separate readiness replay/events and start-wall CSV metadata; updated schedule documentation and the open run sheet (compilation passed).

- Verification: Unity replay serialization/selection integration/failure isolation, bounded-queue overflow, render smoke, readiness/audio gating, and both-controller selection checks pass. Voice/schedule (50 participant schedules) and randomized-layout shell checks pass. Fixed an introduction/target-announcement coroutine cancellation race found during integration. Final Android Build And Run succeeded in 71.9 seconds; installed and launched on the connected Vive Focus Vision (process verified). Physical gaze-gate behavior, replay comparison, audio timing and headset performance remain acceptance checks.

- Cleared old recordings from the connected Vive at the researcher's request: nine run folders, eighteen participant preparation folders, twenty-three global preparation logs and the legacy root gaze CSV (85 files total). Verified all targeted files were removed while preserving voice setup metadata and audio caches; relaunched the installed app and verified its running process. Reviewed current trial/gaze exports; native 120 Hz acquisition, controller/head motion streams and dedicated first/final gaze/raycast latency fields remain outstanding.

- Added plain-language explanations to every ChallengeSet function and computed property, including participant seeds, signed/absolute angles, mirrored walls, shuffling, distractor counts, target placement and schedule exports. Verified that executable code is unchanged; this is documentation only.

- Updated the measured schedule to ten trials per voice block (twenty total, practice separate), with two occurrences each of absolute wall angles 0/45/90/135/180 degrees. Bumped the schedule version, moved the voice boundary to trial index 10, and aligned the open run sheet, decision register and angle-schedule documentation. Regression verifies all twenty manifest rows and quotas across 50 participant schedules; run-sheet compilation passes. Android Build And Run succeeded in 41.4 seconds and launched on the Vive Focus Vision. Physical session acceptance remains pending.

- Removed the enrollment 3–2–1 countdown while retaining the recording instructions and tone. Implemented participant-seeded, mirrored and independently shuffled target-wall angle schedules with identical absolute-angle quotas per seven-trial voice block. Added full planned-trial CSV and summary angle/seed metadata; retained randomized heights and object coordinates. See docs/balanced-angle-schedule.md for quotas, angle interpretation and the open 30-trial schedule decision. Regression tests pass for 50 participant schedules, randomized layout and enrollment. Android Build And Run succeeded in 90.0 seconds and launched on the connected Vive Focus Vision; physical listening/gaze and trial-start-facing acceptance remain pending.

- Saved all remaining protocol/run-sheet edits and Unity recovery/crash artifacts at the researcher's request for the v3 checkpoint. The saved run sheet compiles successfully; legacy IRB packet reconciliation and headset acceptance remain open.

- Recovered the temporary Unity checkout Git link and missing tracked files from its existing HEAD, preserving local protocol edits. Implemented first-person-singular clone speech and external neutral speech with versioned per-voice formatting through preparation, background synthesis, playback and correction-cache lookup. Replaced warmer/colder hints with gaze-relative left/right target-wall guidance and the existing debounced interrupting area cue; preload all three hints, cancel stale direction cues and suppress invalid gaze. Voice-isolation and rotational-layout suites passed, covering provider payloads, offline cache reuse, directional handedness and entry gates. Unity restarted and compiled without C# errors. Required Build And Run was attempted but failed during prerequisites with `No Android devices connected`; headset installation and listening/gaze acceptance remain pending. Preserved Unity scene backups in Assets/_Recovery/.

## 2026-09-27

- Revised participant-facing run-sheet explanations into future tense for the upcoming task, voice blocks, practice and data collection. Preserved direct instructions, current comfort questions and post-task completion/debrief wording.

## 2026-09-26

- Updated the open run sheet from the researcher-supplied revision: two counterbalanced voice blocks, trial/block definitions, between-trial rest and immediate-pause timing, consistent planned 30-trial wording, and removal of obsolete timing-condition references. Recorded the design in the IRB decision register and flagged legacy protocol/consent sources as unreconciled; no Huron submission or generated-packet update.

## 2026-09-25

- Prepared the complete pilot checkpoint for v2: randomized 168-object layout, floor-relative heights, wrist degree-guide toggle, immediate gaze-area speech interruption, finalized run-sheet source/PDF and prior controller/audio fixes. Layout/gaze-gate and voice-isolation regression suites passed again; prior Android Build And Run succeeded. Physical headset acceptance remains pending.

## 2026-09-24

- Made gaze-area corrections more immediate at researcher request: 100 ms stable entry and 40 ms fade, down from 300/150 ms. Search speech is cut off mid-sentence; instructions remain protected. Added regression proving unfinished audio is stopped rather than awaited. Geometry/gate and voice tests passed. Android Build And Run succeeded (296.4 s), installed and launched on the connected Vive; physical timing/listening QA remains pending.

## 2026-09-24

- Implemented 168-object scatter (21 per octagon wall) with trial seeds, random heights/width positions, 20 cm minimum spacing and recorded layout_seed. Exactly one target and scaled conjunction distractors retained. Added eye-gaze target-wall entry correction: 300 ms dwell, 150 ms speech fade, 500 ms exit rearm and 6 s cooldown, with tracking/search revalidation and protected instructions. Preload correction in both voices (ten startup clips). Scatter/gate and voice tests pass, including stale correction cancellation, progressive volume reduction and network-free playback. Android Build And Run succeeded (141.8 s); updated package and running headset process verified. Physical gaze/audio/performance QA still required.

## 2026-09-24

- Expanded octagonal object centers to 0.10–3.048 m above the tracked floor with configurable bounds and a new layout tag. Scene requests floor tracking; wrist Settings now includes Degree guides to toggle outlines and labels together across rounds. Geometry checks pass for bounds, determinism, spacing and connected corners. Android Build And Run succeeded (56.4 s); verified updated package and running headset process. Physical floor alignment and wrist-toggle QA remain pending.

- Fixed trial aborts from later voice clips exceeding accepted-sample RMS headroom: retain progressive loading, clamp gain to 4 / peak to 0.9, and record requested/achieved RMS, limitation flag and audio_level_limited telemetry. Starter matching remains strict; missing/corrupt audio and voice-isolation checks remain enforced. Peak-limited and quiet-clip regressions failed before the fix and pass afterward; targeted manual review (unrelated branch work) and inline reuse/quality/efficiency review found no additional changes needed. Committed fix as 83785d0. Android Build And Run passed after restarting Unity to clear stale editor assemblies; verified package update and running headset process. Human playback/trial acceptance remains pending.

- Added controller-ray object-entry haptics to ControllerRaySelector: per-controller 40 ms / 0.2 amplitude pulses, no sustained-hover repeats, reset on inactive search/tracking loss/selection reset. Documented behavior and updated #34 device QA. Unity batch compilation and ControllerSelectionChecks passed (exit 0). Required refocus/build script attempted but macOS could not connect to Unity (-609); no device build or physical haptic validation completed.

## 2026-09-23

- Updated GitHub #26 with a draft exploratory own-voice debrief interview and finalization criteria: self-recognition, perceived performance, liking/eeriness, use cases and context preferences. Linked operator delivery in #27. Verified both issue bodies; live survey and finalized run sheet remain unchanged pending question-set finalization.

## 2026-09-23

- Removed the metadata cover page from the live Overleaf run sheet at the researcher’s request and synced the local source, preserving current live edits. Verified the remaining source exactly and compiled six pages with zero errors or warnings.

## 2026-09-23

- Synced the local run-sheet source to the researcher-finalized attachment verbatim. Verified the live Overleaf experimenter-run-sheet.tex already matches all 13,391 characters exactly; recompiled successfully to seven pages with zero reported errors or warnings. This finalized edition supersedes the earlier ten-page local draft. No protocol wording changed during sync.

## 2026-09-23

- Verified the run-sheet meeting checklist against GitHub tasks. Updated #33/#26 with explicit participant-chosen agent gender/accent (superseding automatic gender matching), 100-point individual ratings including self-similar, and separate requested 1–7 voice/accent similarity items. Updated #27 with the local ten-page run-sheet revision and publication limits. Existing #20/#22/#14/#16/#18 already own timing, motion/replay, literature/quality and disclosure requirements. Read back and verified the updated issue bodies.

## 2026-09-23

- Revised the researcher-supplied Overleaf run-sheet source: initial Qualtrics records own perceived accent separately from preferred agent gender presentation and accent, including no-preference/missing handling. Added preference-aware previews/ratings, explicit participant voice choice and mismatch handling, early sample collection, readiness/identity checks and separate voice/accent-similarity items. Replaced superseded gaze-dwell instructions with the confirmed controller-ray/trigger method. Survey/application implementation and unresolved scale/allocation decisions remain explicit. Compiled a 10-page review PDF; changes are local, not a live Qualtrics or Overleaf publication.

## 2026-09-22

- Created GitHub #35: eye-measurement validity gaps and true 120 Hz acquisition evidence gate; added to Participant Readiness as Ready. Defined source-route feasibility, native-versus-predicted timing, render-independent acquisition, buffered persistence, quality/calibration/detector validation and hardware proof. Linked tailored responsibilities in #8/#14/#15/#16/#17/#19/#20/#22/#26/#28. No acquisition capability or workload validity claimed from advertised frequency; implementation remains pending. Verified issue bodies and project membership.

## 2026-09-22

- Replaced gaze dwell capture with controller ray + fresh trigger press. Blue highlight follows controller intersection; gaze remains observational for telemetry and hints. Search objects cannot be grabbed. Pause/round/tracking resets require trigger release; UI hits take precedence. Added controller selection verification and selection-method metadata. Updated spoken instructions in both perspectives and enrollment wording. Unity compilation, controller checks and octagon regression pass. Required Build And Run attempted: existing OpenXR Composition Layers Support validation blocks Android build; no adb device connected. Headset QA remains pending.

# Project Progress Log

Daily progress on the Gaze Contingency Project ("Follow My Voice: Gaze-Contingent
XR Search with a Self-Similar Agent"). Newest entries on top.

## 2026-09-22

### Implement the connected octagon and wider object coverage
- **Confirmed design:** retain eight faces around the seated user and spread objects across the connected faces; the researcher clarified that solid backing panels are not requested. Kept 56 objects, stable plane/slot IDs, existing vertical placement and shape/target composition.
- **Implemented locally:** derive outline width from the actual outline radius so all eight seams close; expand horizontal object placement with 15 cm of corner clearance. Added `rotational_octagon_v2` manifest provenance and updated the geometry guide to describe wider spacing and changed radial distances.
- Verification: observed the expected failing old-layout coverage test, then passed extended geometry checks at 1/1.5/3 m, Unity batch compilation, and a temporary integration harness invoking actual frame creation at a translated origin and 137° heading. Runtime corners join, frames add no colliders, and existing through-wall gaze physics checks pass. Code review found no actionable issues.
- Build And Run was triggered in Unity for this checkout. Android preprocessing failed because OpenXR Composition Layers Support is required by the installed package but not enabled; geometry changes do not modify XR settings. ADB also reports no connected headset. Device rendering/targeting and build configuration remain unverified; no successful APK/device deployment is claimed.

### Scope connected search walls under existing geometry task
- Confirmed current remote v2 uses eight separate outlined frames (0.84 m by 0.72 m), with seven objects clustered on each tangent plane and no solid backing. Added the requested connected-corner extension and geometric/interaction verification criteria to #9, preserving its project status.
- **Open decisions:** eight joined faces versus six, and solid panels versus expanded object coverage versus both. Requested clarification before changing the study geometry; no code or device build yet. Existing local user changes remain untouched.

### Lock controller ray as the selection method
- **Confirmed design decision:** researcher explicitly locked controller-ray object selection, with trigger confirmation, replacing gaze-dwell completion in the intended study. Gaze remains independently logged and supplies gaze-contingent guidance.
- Recorded the confirmation in #7 and #34 and verified the saved issue bodies. Press-versus-hold timing and other input details remain separate open decisions; no runtime implementation or headset test is claimed, and task statuses are unchanged.

### Map PeriSpike screenshot measures and citations into existing GitHub tasks
- **Protocol proposal:** reviewed the supplied measures/reference screenshots from anonymous submission 1752 and extended #26 (susceptibility/SSQ/IPQ selection), #27 (exposure and stop procedure), #20 (exposure events), #14 (gaze validity and projection), #15 (optional BCEA/transitions/SSQ-per-minute analysis), and #16 (source verification and interpretation). Preserved existing scope, issue history and project statuses; advanced analytics remain deferred.
- Checked the Golding MSSQ bibliographic/abstract source, Golding/Rafiq/Keshavarz VIMSSQ full text, and Castet/Crossland eye-stability bibliography/abstract. SSQ/IPQ exact form/scoring checks remain explicit tasks. The public PeriSpike manuscript, Zhao reference [49], mapping of [9], and two unseen gaze metrics remain unresolved; no invented citations or publication status.
- Specified transfer limits for seated passthrough MR: source-study 90 Hz, 20-minute exposure and normalized viewport radius are not local validated parameters. Preserve exposure and symptom outcomes separately, avoid bridging gaze gaps, and do not interpret free-search dispersion as fixation stability or cognitive load. Planning/GitHub changes only; no runtime, live survey, Overleaf or device changes.

### Add paper-informed eye-gaze implementation requirements to GitHub
- Verified the screenshot reference as Zagermann, Pfeil and Reiterer (2016), *Measuring Cognitive Load using Eye Tracking Technology in Visual Computing*, BELIV, pp. 78–85, DOI `10.1145/2993901.2993908`, using the open full text at <https://d-nb.info/1128594803/34>. It is a position paper; its proposed ocular/workload relationships do not validate this headset or task.
- **Protocol proposal / implementation backlog:** expanded and renamed existing #14 to own eye-gaze acquisition, calibration and quality validation, with concrete sensor-capability, timing, validity, coordinate-frame and device-test requirements. Added conditional fixation/saccade/blink and pupil criteria plus confound controls; linked telemetry #22 and outcome interpretation #16. Preserved existing issue bodies and project statuses; #14 remains Ready.
- **Implemented evidence:** inspected `GazeDataLogger.cs` at remote v2 `3cd250b`; the current render-frame logger and heuristic blink output are not sufficient evidence of validated physiological measures. GitHub bodies and project membership were read back and verified. No runtime changes or headset tests in this task.

### Format the annotated run sheet for Overleaf
- Created a standalone LaTeX review edition with UCF-inspired black/gold styling, distinct Say/Ask speech, italic researcher actions, highlighted meeting questions, session fields, page numbers and a seated 360-degree schematic. Preserved the supplied script and annotations with a cover note identifying the newer preferred-voice/controller requirements.
- Added the source to the requested Overleaf project alongside the manuscript and verified its eight-page PDF after a clean Overleaf build. Saved the reproducible generator and review PDF; local Tectonic compilation, text-coverage checks and page-by-page visual review passed without overfull boxes. Protocol choices remain open under #27; no runtime or participant-readiness claim.

### Reconcile meeting requirements with v2 and the GitHub project
- Audited latest `v2` at `6457c1f`, all 29 GitHub issues/project cards and the older local task lists. The supplied annotated run sheet exactly matched the committed source; preserved its notes and saved the meeting prompt verbatim.
- **Protocol proposal:** updated the planning entry points to preferred versus self-similar voice, controller selection with independent gaze, and pilot-first delivery. Added a complete 27-section owner map, evidence table, next-step sequence and open method decisions in `docs/meeting-reconciliation-2026-09-22.md`.
- **Implemented evidence:** the beta remains 14 measured gaze-dwell trials with neutral profiles; shared script modes, progressive preparation, practice/blocks and survey drafts exist. Replay work on `codex/trial-demo-replay` is separate and unmerged. Planning updates do not complete any pilot acceptance gate.
- Reused existing issues, preserving acceptance criteria/history; added concrete progress/next actions and run-sheet details for survey-to-trial joins, final-zone timing definitions, measured sampling rates and distinct operator actions. No duplicate issues or issue closures.
- Documentation/project update only: no runtime, live Qualtrics, generated PDF/Word or device-build changes. Existing unsaved workspace work was preserved by preparing this update in a clean checkout from `origin/v2`.

## 2026-09-20

### Checkpoint all workspace changes
- Merged the complete workspace snapshot, including the latest inline protocol questions, calibration and telemetry notes, survey wording and break updates. Existing generated PDFs remain their earlier review editions; they have not been regenerated for these latest source edits.

### Save remaining study materials
- Checkpointed survey sources, generators and review PDFs, the UCF protocol review edition, current run-sheet edits and the provisional 60-trial planning decision. Preserved the existing published protocol and study-flow figure.


### Publish formatted experimenter protocol
- Added the current run sheet, verified UCF-themed PDF and self-contained LaTeX sources. Added prominent README links to the protocol and editable sources. The cover identifies trial-count and voice-order inconsistencies for review.

### Add provisional study procedure figure
- Added the UCF-inspired study-flow image and reproducible ImageGen prompt under `output/imagegen/`.
- Depicts two counterbalanced 30-trial voice blocks, two practice trials, survey timing and seated eight-plane gaze search. The figure labels the procedure provisional; it does not change runtime trial counts.

### Link the experimenter protocol from the README
- Added a prominent PDF link and editable source links, retaining the review/rehearsal status. Copied the complete formatted protocol and Markdown into the local v2 checkout so its README links resolve.
- Opened the three existing Qualtrics survey builders for review.


### Format current experimenter run sheet as PDF
- Converted the current Markdown run sheet to a UCF-themed PDF with editable LaTeX under `docs/protocol/formatted-run-sheet/`. Preserved source wording and flagged the trial-count and voice-order inconsistencies in a cover note.
- Compiled using Tectonic and checked extracted sections and rendered pages.

### Set provisional study trial count
- Subsequent user revision: increased the provisional plan to 60 experimental trials, 30 per voice condition, plus two practice trials (62 total). Updated both planning documents; runtime remains unchanged. Thirty trials per block cannot divide equally across eight planes, so the location-balancing schedule remains open.
- Recorded the researcher’s provisional choice of 48 experimental trials: 24 per voice condition, plus two practice trials (50 including practice), in the protocol decision register and alignment document.
- Runtime and dated protocol exports remain unchanged; angle allocation, pilot feasibility and power justification remain open.

### Save reusable UCF academic PDF styling and render protocol
- Created the personal `ucf-academic-pdf` skill with a reusable LaTeX theme, local compilation fallback and PDF/text/visual verification instructions, based on the requested Hermes references.
- Produced a UCF-inspired protocol review edition under `docs/protocol/ucf-themed/` with editable LaTeX, a charcoal/gold cover, session overview, and explicit review notes for wording, TLX duplication, survey linkage and open decisions. The original run sheet remains the dated source; this is a styled review edition, not a protocol freeze.
- Validated the skill and compiled with Tectonic because pdflatex is unavailable; checked extracted content and rendered pages.

## 2026-09-19

### Create Qualtrics draft survey battery
- Built repeatable Qualtrics Advanced TXT imports from the questionnaire and measures register: baseline/pre-task, post-voice-block and post-session/safety.
- Imported the three drafts into the UCF Qualtrics workspace, preserved repository item IDs as question export tags, and removed each blank starter block. Draft IDs and builder links are recorded in `resources/surveys/qualtrics/README.md`.
- Included the full proposed 24-item IMI bank, NASA-TLX, baseline/post SSQ, voice manipulation and technical checks, and the Guo Table A1 source bank. IPQ remains on hold for the passthrough MR design; final wording, burden, scoring, requiredness, IRB reconciliation and publication remain open.

### Print the Qualtrics drafts for protocol review
- Rendered one print-ready PDF per draft under `output/pdf/`, with survey ID, administration timing, export tags, response choices, writing space and page numbering.
- Visually checked representative first, middle and final pages for clipping, readable text, matrix response scales and interview writing areas. The PDFs are static review copies; response collection remains in Qualtrics.

### Add source annotations to the survey PDFs
- Rendered annotated companion PDFs with a yellow provenance note after each item group and a references page covering NASA-TLX, SSQ, IMI, Kao et al. (2021), Guo et al. (2024), and the original Guo Table A1 attributions.
- Marked demographics, technical checks, preference questions and interview prompts as study-created instead of implying they came from a validated paper. Visually checked representative annotated pages and reference pages.

## 2026-09-16

### Archive submission-video reference and checkpoint all work for v2
- Preserved the original TimeFlow MP4 under `resources/videos/` with provenance, technical metadata and checksum; configured Git LFS for the 177 MB file.
- Created #32 for reference review, original storyboard, headset/setup capture, narration/captions, evidence-backed claims and final submission export/review; linked epic #19 and the Study Hub.
- Checkpointed the voice-script implementation and accumulated protocol, measures, survey/paper/figure/video resources for the user-requested v2 push. Prior voice/enrollment tests and Unity compilation passed; headset QA remains pending.

### Track master's thesis and committee document readiness
- Created #31 for official template verification, canonical thesis source/build, chapter-level readiness, citation/format checks and a versioned committee-review package; linked epic #19.
- Recorded dependencies on design/power, instruments/protocol, setup figure and actual analysis/results. Committee milestone, recipients, review lead time and dates require confirmation; task creation does not claim approval or authorize sending/submission.

### Archive motor-control paper and track the study setup figure
- Saved the supplied motor-control submission and flow-image reference under `resources/papers/` and `resources/figures/`, with provenance and SHA-256 checksums. Linked the reference shelf; publication status remains unverified.
- Created #30 for an original two-panel seated eight-plane setup/session-flow figure, including editable vector and PDF/PNG exports, caption and alt text. Linked epic #19 and dependencies for final counts, wording and surveys; did not import the reference paper's training design into this study.

### Make power analysis an explicit GitHub work package
- Expanded and renamed #16 to own primary-outcome definition and reproducible power/sensitivity analysis for participant and trial counts. Added assumptions, repeated-measures structure, practical effect, pilot uncertainty, attrition, reporting artifacts and final sample-size decision tasks.
- Linked #10 candidate schedules, #17 pilot inputs and #19 epic readiness to that work. No calculation was performed or final sample/trial count selected; initial sensitivity work starts now and finalization follows pilot evidence.

### Implement issue #24 script pilot modes
- Added Collaborative and External wording selection after Start setup, with a shared versioned phrase set across both voice conditions. Perspective locks before enrollment and persists through preparation, retries and both blocks; a new session resets the choice.
- Scoped audio caches and manifests by perspective/version while preserving eight starter clips and background loading. Playback telemetry retains exact spoken text, and run metadata records the selected script.
- Added the script comparison guide and pilot rubric. Final listening assessment and study-script selection remain open; issue #24 is not yet fully complete.
- Used Luna workers for phrase modes, setup/session changes, regression coverage and review. Unity compilation, voice-isolation tests and enrollment tests passed. Build And Run was triggered, but Unity stopped with “No Android devices connected”; hardware playback/UI QA remains pending.

### Create the experimenter protocol from the supplied run-sheet example
- Added a six-page Word run sheet and Markdown companion under `docs/protocol/`, preserving the supplied example's typography, headings, Say/Ask/Action structure and page footer. Linked both from the Study Hub and procedure document.
- Checked setup, voice recording/readiness, practice, blocks, pause controls and saved records against the Unity scripts. Included participant scripts, survey handoffs, debrief and final data checks; distinguished implemented beta behavior from the fixed-front proposal and other open decisions.
- Rendered and visually checked all six pages. Documentation only; no device build or submission-packet regeneration.

### Add a single study-materials hub
- Added `docs/STUDY-HUB.md` with the current protocol summary, survey shelf, reference shelf, decisions/gaps and delivery-board link. Marked older PDFs and IRB sources as drafts needing reconciliation.
- Added the hub to the repository README and survey/measures/IRB indexes; corrected the README’s superseded gaze-aware versus unaware question to the current voice comparison. Documentation only; no device build.

### Create GitHub participant-readiness project
- Created private GitHub Project #2, “Gaze Study — Participant Readiness,” with the 24 existing repository issues and an automatic repository import workflow: https://github.com/users/PedroPovedaQ/projects/2.
- Configured Backlog, Ready, In Progress, Ready for headset QA and Verified statuses. Added stage and priority issue labels, separated deferred/historical scope, and documented the participant journey, work order and dependency gates in the project README. Linked epic #19 and updated #26 with IMI/Guo survey decisions.
- Used the signed-in browser because the CLI token lacks Projects scopes; no permission expansion was required. No runtime changes or device build.

### Add Guo et al. agent-perception survey
- Saved the 23-page author-hosted paper (DOI 10.1145/3651288); visually checked Table A1 against the user's screenshot. Added an unchanged-wording 43-item Markdown/CSV source bank with original item IDs, anchors and all five source references.
- Extended the measures register with S16–S24; linked the questionnaire source and added missing bibliography entries. Flagged appearance-dependent items, pending scoring/adaptations, overlap with legacy eeriness and the proposed combined survey burden. No runtime or generated submission form changes.

### Add IMI and establish the measures register
- Saved Kao et al. (2021), DOI 10.1145/3474665, from the author's university site and the official IMI complete packet under `resources/surveys/`, with provenance and SHA-256 checksums.
- Added `docs/measures-register.md` to track subjective, behavioral and quality measures, operational definitions, timing, scoring, implementation support, analysis joins and unresolved decisions. Recorded confirmed IMI inclusion and proposed the paper's four subscales; distinguished this from the unapproved final item selection.
- Linked protocol, alignment and analysis docs; removed the questionnaire source's stale frozen status, added IMI planning, corrected two-block administration and retired directional guidance item. IPQ remains on hold; legacy forms still require reconciliation. No runtime changes or generated submission-packet rebuild.

### Clarify the seated rotating-search thesis and documentation conflicts
- Recorded the user-directed seated rotation task and audited the protocol, beta documentation and older IRB decision register. Added `docs/thesis-study-alignment.md` with document-by-document reconciliation work and explicitly pending decisions.
- Corrected stale beta branch/implementation descriptions and marked mixed historical protocol content as needing reconciliation. The user confirmed return to a fixed front direction before each trial, warmer/colder-only guidance and cloud voice processing, and reported that the IRB application has not been submitted. Recorded these as target-protocol decisions; the beta still needs its fixed-front gate. Pilot trial count remains unanswered. Runtime behavior and generated submission artifacts are unchanged.

### Checkpoint the beta and recording workflow for v2
- User requested promotion of the current beta work to `v2`, including the surrounding layout, through-wall gaze selection, progressive/background voice preparation, recording package commands and meeting backlog notes.
- Voice isolation, enrollment, rotational geometry and synthetic recording/merge checks pass. The runtime changes were built and launched successfully on September 12; this checkpoint adds no new runtime behavior.
- Different collaborative/external voice scripts are tracked by [issue #24](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/24), not implemented by this checkpoint. The future voice-by-perspective factor is deferred in #29.

## 2026-09-15

### Translate the latest meeting into a thesis epic and executable backlog
- Created [epic #19](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/19), nine native child issues #20–#28, and a separately deferred ISMAR issue #29. Each includes scope, dependencies and acceptance criteria. Added an ordered delivery sequence and updated existing issues #6–#18 instead of duplicating their calibration, schedule, analysis, pilot and material/lifecycle work.
- **Meeting decision:** the thesis compares generic versus self-similar voice within participants, always gaze-contingent. **Open decision:** choose one shared wording/perspective after piloting alternatives; current condition-specific phrasing is a confound to resolve. Preserve eight-starter/background preparation rather than the older full-library startup requirement.
- **Protocol proposal / open decisions:** enrollment around 15–20, repeated geometric difficulty, instrument timing, headset breaks and reported thesis dates require finalization/verification. Future perspective factors and unvalidated physiological interpretations are not thesis launch requirements.
- Verified all 11 newly created issues and nine native child links, resolved dependency references, and confirmed meeting updates on 13 older issues. This was planning only: no participant data collection, external messages, implementation changes or Unity/device build.

## 2026-09-12

### Reusable headset and Yeti recording commands
- Added `scripts/record-vr.py` and dependency-free package commands for start/status/stop/devices/merge. Silent scrcpy video and a separately captured Yeti WAV finalize on disconnect or stop; raw files remain available. The microphone is resolved from current device names, with no silent fallback.
- Created the personal `record-vr-session` skill with the same helper and package workflow. Audio timing uses approximate launch offsets and supports manual refinement; a static final video frame is extended to the recording endpoint when necessary.
- Live validation: stopped the previous session, preserved its earlier Mac microphone and Yeti files, and started a new verified video/Yeti session through `npm run record:start`. Synthetic FFmpeg checks pass for audio delay/advance, final duration, stream presence and preservation of originals; skill frontmatter validation passes.

### Install the latest beta on the headset
- Built and launched the current workspace on the connected VIVE Focus Vision, including the eight starter clips followed by silent background loading and the existing through-wall gaze fix. Android Build And Run succeeded in 99.4 seconds; the replacement app process is running.
- Sampled startup logs contain no fatal exception, null-reference, missing-method or missing-library markers. Full headset listening and interaction QA remains pending.

## 2026-09-11

### Continue loading audio after the eight starter clips
- **Implemented:** background loading begins during the voice checks, queues upcoming trial instructions before other phrases, and silently prepares both voice variants. It does not change the active voice condition, block controls or play audio. Foreground playback interrupts the loader and the interrupted item stays queued for resumption.
- **Implemented:** background errors do not change foreground audio-check errors or stop an active trial. A needed missing clip still uses foreground generation and normal validation/failure handling. The manifest identifies `starter_then_background`.
- Verification: production voice harness passes background silence, voice isolation, foreground preemption, resumption, instant cached playback and background-versus-foreground failure handling. Unity compiled the changes; Build And Run stopped at “No Android devices connected.” Installation and on-headset QA remain pending.

### Prepare voice prompts as the session progresses
- **Implemented:** setup prepares four starter phrases per voice instead of the full phrase library. Remaining authorized trial and hint audio is generated on first use and cached independently per voice. Both audio checks are still required.
- **Implemented:** new clips retain first-person self-similar wording, must match the accepted starter RMS/peak constraints, and update the participant and active-run manifests with preparation mode and actual spoken text. Round preparation displays a waiting message; search timing and capture remain gated until the instruction finishes. First-use hint synthesis may add latency during search.
- Verification: production voice isolation harness covers lazy generation, cache reuse, condition separation, matching levels, active-run manifest writes, unknown phrase rejection and failure handling. Voice enrollment tests also pass. Unity compiled the changes without C# errors; the requested Build And Run stopped at “No Android devices connected.” Deployment and headset timing/listening QA remain pending.

### Through-wall gaze selection in the rotational beta
- User reported the eight-plane beta works well and requested a checkpoint; pre-fix beta is saved at `751097a`.
- **Implemented:** beta start configures the eye-gaze ray to hit searchable layer 8 only, ignore UI graphics, and select the nearest searchable object. Room colliders no longer block gaze targets; controller menus retain their existing interaction.
- Verified the mechanism in XRI's ray target selection and added an Editor regression check with actual wall/target colliders. The all-layer ray hits the wall; the configured ray hits the target behind it. Check passes. Android Build And Run succeeded in 52.7 seconds and launched the replacement process; user headset QA remains pending.

### Eight-plane surrounding-search beta
- **Implemented:** separate `codex/eight-plane-beta` branch from the stable `v2` checkpoint. Explicit seated centering captures headset position/forward direction once; eight vertical planes surround that fixed origin at 45-degree intervals and a default 1.5 m radius, seven objects per plane. Surface taps no longer start the beta.
- **Implemented:** noninteractive plane outlines/degree labels, a goal display below the turning view, practice and existing voice setup/announcements, 14 measured rounds, and gaze dwell. Coverage identifies eight planes by stable object ID; hints use within-plane distance. The object manifest labels beta runs and records plane/slot/azimuth and origin/heading.
- Verified geometry determinism, counts, handedness, spacing and invalid-radius behavior (test initially failed before the implementation existed). Both voice harnesses and all 9 analysis tests pass. Review/simplification ran inline per the project instruction; no independent peer review was used. Resolved duplicate centering and retry behavior. Final Android Build And Run succeeded in 37.8 seconds; the headset process launched with no fatal/managed startup exception markers in the sampled log.
- **Open validation:** full seated headset QA, radius/readability/comfort and side/rear gaze acquisition. The beta does not implement the larger proposed schedule, plane-balanced targets, forced forward alignment, theta calibration or coarse directional audio. `docs/eight-plane-beta.md` records the boundary and QA procedure.

### Checkpoint voice setup and practice updates on v2
- **Implemented:** neutral recording introduction uses the requested wording: "We will now connect a short sample of your voice, after the tone please read the script." Permission checks precede the countdown and tone; capture opens only after the cue finishes. Failed instruction playback offers an enrollment retry.
- Checkpoint includes explicit practice labels and announcements, command-based enrollment, processing declarations, first-person plural self-similar prompts, and the updated yellow star instruction.
- Verification: both voice harnesses pass, including neutral introduction before enrollment and cue-before-capture ordering. Android Build And Run succeeded in 50.4 seconds and launched the updated app. User requested this checkpoint be pushed to `v2`; headset listening QA remains pending.

### Spoken processing status and first-person self-similar wording
- **Implemented:** announce "Processing voice" before preparing the voice libraries and declare both voices ready after successful preparation, before the audio checks. Setup status uses the selected neutral voice and restores the assigned condition afterward.
- **Implemented:** self-similar targets, practice, hints, audio checks, introduction, congratulations and closing use first-person plural wording. The same conversion runs before synthesis/cache lookup and is recorded as actual spoken text in the manifest. Neutral wording is retained.
- Updated procedure and QA notes: wording and voice identity now vary together. Verification covers all live hint strings, target/practice synthesis, prepared playback without network requests, and setup-status condition restoration. Both voice harnesses pass; Android Build And Run succeeded in 49.1 seconds and launched the app. End-to-end listening QA remains pending.

### Clarify practice and use command-based enrollment
- **Implemented:** practice announcements explicitly say the round is practice and does not count toward the study. Transition and search displays label practice; progress shows practice 1 / 2 or 2 / 2 as not counted. Both voice libraries include the practice-specific announcements.
- **Implemented:** enrollment now uses short visual-search and gaze commands, with an instruction to read naturally and finish recording when done.
- Verification: voice isolation and enrollment harnesses pass; final Android Build And Run succeeded in 44.3 seconds and launched the updated process. Visual and spoken headset QA remains pending.

### Update yellow star spoken instruction
- **Implemented:** the yellow star target now uses "Locate the star, yellow color." in both voice libraries and round announcements. Audio caching already includes the exact phrase text.
- Verification: voice isolation checks pass; Android Build And Run succeeded in 48.1 seconds and launched the updated app on the headset. In-headset audio QA remains pending.

### Pull upstream task wording and relaunch headset
- **Implemented:** fast-forwarded to `v2` commit `a5fe654`, including project-wide Locate wording and the updated audio content version.
- Verification: both voice test harnesses pass. Android Build And Run succeeded in 57.0 seconds; the updated process launched on the connected headset and passthrough initialized successfully. Full session QA remains with the researcher.

### Publish accumulated project work to v2
- User requested all current project changes be committed and pushed to `v2`. Remote `v2` matches the working branch base; no force push or merge is needed.
- Pre-push validation: both C# voice harnesses and all 9 analysis unit tests pass; diff whitespace checks pass. Local API credentials remain ignored and excluded.

### Cached setup pacing and audio diagnostics
- **Performance:** skip the 100 ms pacing delay for local cache hits, retaining it for newly generated clips. Removes 5.3 seconds of deliberate delay for 53 cached neutral clips, or 10.6 seconds when both libraries are cached; these are eliminated waits, not measured end-to-end speedups.
- **Implemented:** preserve synthesis error details, separate final matching and manifest-saving progress stages, and persist preparation diagnostics.
- Verification: cached preparation makes zero provider requests and adds no pacing waits; voice isolation and enrollment tests pass. Android build/install succeeded and launched the updated app.

### Explicit setup start gate
- **Implemented:** startup waits at a pointed-at Start setup button before any voice selection or recording. Button arms after a brief released-input interval while focused; focus loss/pause disarms it. A global trigger press does not activate this gate. A post-start cooldown and fresh input edge prevent that click selecting a voice underneath.
- Existing optional automatic defaults now apply only after Start setup. Public voice-selection methods cannot bypass the waiting phase; `StartSetup()` provides the programmatic equivalent.
- Enrollment and voice-isolation checks pass; full headset overlay/click-through behavior remains user QA. Updated consolidated checklist. Android build/install succeeded in 131.2 seconds and launched the updated app.

### Early completion of voice enrollment
- **Implemented:** clickable Finish recording button below the reading passage, with Trigger / 1 and public method equivalents. Recording ends on confirmation or its existing deadline, and encoding uses the captured frame count. One-second guard rejects accidental empty submissions; microphone is released on component disable.
- Verification: production enrollment coroutine tests cover early finish, automatic deadline, duplicate/idle input, captured frame count and interrupted microphone; voice isolation harness passes. Unity recompiled without C# errors; Android build/install succeeded in 57.7 seconds and launched the replacement process. Physical button/reading layout QA remains pending.
- **Observed:** user reached the reading passage after the clean rebuild, so startup progressed beyond the earlier scene-loading crash. Full study QA remains pending.
- **Open platform question:** no eye-tracking notice UI was found in app code/assets. The notice appears to be HTC runtime/system-owned; no supported app-side suppression setting was confirmed. System prompt behavior remains unchanged.

### Startup crash recovery — verification pending
- Added `Tools > Codex > Clean Build And Run Android` using Unity's `BuildOptions.CleanBuildCache`, preserving normal incremental builds. Clean rebuild/install completed in 443.6 seconds (1141.0 MB).
- **Observed:** clean rebuild produced identical level0 scene bytes. This does not establish a fix. Native symbolication shows script-component deserialization; the crash register's 251104 offset matches the packaged FindObjectGameManager record, making it the leading component to investigate if the crash persists.
- **Blocked device check:** the replacement process starts but the headset reports Asleep and pauses/stops before scene loading. Asked user to wake/wear it and retry; no response yet. No claim that the startup crash or full study QA has passed. Sanitized fresh-process evidence: `/tmp/gaze-clean-build-startup.log`.

### Headset startup crash diagnosis
- **Observed:** three native SIGTRAP crashes on `Loading.Preload` at 10:55:18, 10:55:35 and 10:55:46. Unity reports packaged `assets/bin/Data/level0` as corrupted with `Position out of bounds!`, before study startup.
- Installed APK SHA256 matches the local build exactly; the packaged level0 ZIP CRC passes. Evidence points to serialized scene/build data, not damage during USB installation. The eye-tracking prompt precedes the symptom but is not established as the cause.
- **Open investigation:** exact serialization/build-cache cause and fix remain unverified. Next diagnostic step is a clean scene/player-data rebuild followed by device startup verification. Sanitized process evidence saved locally at `/tmp/gaze-headset-crash-20260911.log`; no runtime changes made during this log check.

### Connected headset deployment
- **Implemented / deployment verified:** built the current local branch for Android ARM64, installed it on the connected VIVE Focus Vision, and verified the running Unity process and launch activity. The headset is asleep and immediately stops the activity; the user must wake/wear it to continue. Unity reported build success in 968.7 seconds (1164.9 MB).
- Reused the main checkout's existing voice configuration in the ignored worktree StreamingAssets file; verified inclusion in the APK without exposing credential values. No research audio was recorded by the agent.
- **Open validation:** participant-facing voice setup, full two-block flow and telemetry QA remain for the user on the headset. Device package is `com.DefaultCompany.MixedRealityTemplate`.

## 2026-09-10

### Four-step voice study implementation — ready for consolidated headset QA
- **Implemented:** mandatory two-voice readiness, full cached phrase libraries, common achieved RMS across both libraries, sample acceptance/replay/restart, bounded preparation retries and explicit enrollment cancellation. Self-similar failure never substitutes neutral speech; both voices are required for this study.
- **Implemented:** two separate practice trials, then two counterbalanced blocks of seven measured trials. Participant files persist voice order and neutral profile. Added per-block NASA-TLX, break checkpoints, pause/withdrawal/technical stops; timeout remains disabled pending a protocol decision. Fixed the scene's existing ten-round debug override.
- **Implemented:** hide stimuli until announcement completion, start a pause-aware search clock at reveal, and gate dwell to active search. Added schema-2 events, stable object IDs/manifests, voice clip identities, separate preparation gaze logs, per-run workload data and a session validator. Analysis reports descriptive block differences and explicit pairing exclusions.
- Verification: production C# voice/schedule/clock harness and all nine Python tests pass. Final production Assembly-CSharp compilation using Unity's generated response file and compiler passes (deprecation warnings only). Earlier Unity Editor reload passed; final interactive inspection was unavailable because the Mac was locked. Required Build And Run was dispatched; no connected-device QA was completed.
- Review: resolved the independent peer's actionable defects, including RMS mismatch, truncated rounds, lost workload rows, ambiguous cancellation, profile persistence and boundary IDs. Changes remain local and uncommitted on `codex/always-gaze-contingent`.
- **Open validation:** user will perform one full headset QA using `docs/voice-study-qa.md`; perceived similarity/neutrality, acoustic onset, memory use and real provider/device failure behavior require that run. Directional/theta mechanics, full spatial measures, power and IRB reconciliation remain separate backlog work. Historical entries below describe earlier states.

### Voice contamination fixes
- Unity Editor compilation passed. Required Build And Run was dispatched; Unity reported **No Android devices connected**, so headset audio verification remains pending.
- **Implemented:** self-similar synthesis never falls back to neutral audio, including when clone IDs, credentials or providers are missing. Cached self-similar audio remains usable offline only under its own clone ID; pending enrollment stays silent.
- **Implemented:** removed automatic reuse/persistence of device-wide clone IDs. Self-similar launch now enrolls the current participant; failure keeps selection incomplete with Retry or explicit neutral selection. Added a missing-provider enrollment guard and corrected immediate-failure speech busy-state cleanup.
- Verification: `scripts/test-voice-isolation.sh` executes the production synthesizer with deterministic providers and populated competing caches, covering missing/pending clones, missing credentials/provider, failed/short audio, successful and offline clone playback, male/female routing and busy-state cleanup.

### Neutral versus self-similar voice
- **User-directed decision / implemented:** voice identity is neutral versus self-similar. Added explicit male neutral (ElevenLabs Eric) and retained female neutral (Rachel); launch selection, synthesis and pre-cache use the selected profile. Voice IDs scope caches to prevent cross-voice playback, and new run labels preserve neutral-male / neutral-female.
- **Implemented:** intro and task start wait for voice selection; removed the fixed Ava introduction. Enrollment failures return to explicit neutral voice selection. Historical generic labels remain readable.
- Verification: Unity Editor compiled/reloaded the scripts without compiler errors; executable C# checks cover male/female neutral IDs, run labels and unchanged stimuli; all six Python tests pass, now including both neutral profiles. Build And Run was invoked and reported no Android device connected.
- **Open validation:** listening checks for perceived neutrality and matched audio, headset build/run, and full protocol reconciliation remain pending.

### Always gaze-contingent agent
- **Protocol proposal — user-directed decision:** gaze awareness is no longer an experimental factor. The agent always uses gaze-contingent guidance; generic versus self-similar voice remains the planned comparison.
- **Implemented:** removed the unaware hint toggle/control phrases; every round uses the existing gaze-responsive proximity policy, with its 2-second initial delay and 4-second cadence. New runs have one guidance block and `always_gaze_aware` summary metadata.
- **Implemented:** preserved all 14 seeded targets and 784 object configurations; normalized voice-suffixed labels in analysis while preserving legacy alternating data. Updated runtime guides, procedure and historical protocol notices; updated GitHub issues #6, #7, #13, #16 and #18.
- Verification: Unity 6000.3.10f1 batch compilation passed; executable C# checks covered all rounds, five participant IDs, both voice labels and unchanged stimuli; six analysis compatibility tests and worktree-environment checks passed; `git diff --check` passed.
- Review fixes: event logs resolve the new run folders correctly, raw voice labels remain available, and awareness comparisons reject datasets containing the new schedule. Updated remaining agent instructions and guide snippets that referenced the removed toggle.
- Used the shared Python environment and an isolated APFS Library clone from the closed primary v2 checkout. No credentials copied.
- Ran `scripts/refocus-unity-and-build-device.sh`; Unity accepted the menu command but reported **No Android devices connected**. Headset build/run verification remains blocked by device connection. Changes remain local on `codex/always-gaze-contingent`.
- **Open work:** directional coaching, spatial/theta mechanics, two-voice block orchestration, revised power/analysis and full IRB packet reconciliation remain separate issues. Historical audit from September 9 is preserved as the baseline before this decision.

## 2026-09-09

### Meeting gap audit and ordered backlog
- **Implemented evidence:** audited v2 at `3822269`; confirmed existing bookshelf runtime and voice enrollment/cache instead of treating all meeting tasks as new work.
- **Protocol proposal:** recorded the two-voice-block, balanced-theta, directional-coaching direction without replacing historical protocol decisions.
- Created GitHub roadmap [#6](https://github.com/PedroPovedaQ/Gaze-Contingency-Project/issues/6) and 12 linked issues (#7–#18), with evidence, dependencies and verification criteria.
- Found timing contamination from transitions, generic fallback in self-similar runs, provider mismatch, persistent clone identity concerns, missing spatial telemetry and outdated analysis assumptions.
- Checked primary literature and corrected the appearance-versus-voice interpretation of the doppelganger study. Full audit: `docs/meeting-gap-audit-2026-09-09.md`.
- **Open decisions:** advisor confirmation of factors/theta/posture, provider and institutional handling, pilot-derived counts/power, final protocol and actual Friday date. No runtime changes, hardware validation or approval claims.

## 2026-08-18

### CITI / IRB training — PASSED
Completed the following UCF CITI **Stage 1 – Basic Courses**, all passed **18-Aug-2026**:
- **IRB Protocol Review**
- **Research and HIPAA Privacy Protections**
- **Researchers – Information Privacy & Security (IPS)**

### IRB packet (v2 branch)
- Filled the administrative fields across the IRB source docs and rebuilt the 8-document
  submission packet:
  - PI: **Dr. Roshan Venkatakrishnan** — roshan.venkatakrishnan@ucf.edu — **407-823-3957** —
    Computer Science, College of Engineering and Computer Science
  - Student investigator: **Pedro Poveda** — pe011248@ucf.edu
  - Lab rooms: **HEC 208, HEC 308, BYC 119** (UCF Main Campus)
  - Submission date: **2026-08-18**; compensation from **approved laboratory funds**
- Reused institutional facts (department, rooms, funding, minimal-risk precedent) from the
  **Study 9491** lab reference packet (Mehrab Islam, same PI).
- Removed the "DRAFT — NOT FOR SUBMISSION" banner from all 8 generated documents
  (updated `build-irb-packet.py` + `verify-irb-packet.py`).
- Huron study created: **STUDY00009581**.

### Still open
- **UCF determinations** (their call): risk level, injury-language wording, clinical-trial
  classification, conflict-of-interest disclosure, Certificate of Confidentiality.
- **Post-approval fills**: IRB study number on recruitment materials, exact calendar dates.
- **Protocol vs. implementation gap**: the Unity build is still the 14-round bookshelf
  prototype and does not yet implement the planned 8-plane / 4-block / Williams design.
