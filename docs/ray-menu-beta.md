# Controller-ray menus and directional hints

**Implemented — beta, 2026-09-27.** Point at a control, see its blue hover feedback and feel one 0.04-second pulse on that controller, then pull the trigger. Hover alone never activates a button. A/B, keyboard digits, and global trigger shortcuts no longer drive setup, checkpoints or surveys. Wrist/tutorial controls use their existing XRI pointer actions with shared hover feedback. Global EventSystem navigation/Submit is disabled; gaze UI interaction is disabled. Physical UI rays use their existing left/right Trigger bindings.

Visible controls cover Start setup; gender; the two avatar voice cards with hover previews and Back / Confirm; Finish recording; preparation retry / re-record / cancel; audio Accept / Replay / Restart; Center and begin; practice/block/pause checkpoints; NASA-TLX sliders and Submit; Finish and Reset to Start. Disabled audio acceptance cannot bypass failed or unfinished playback. The clone page says **Finish recording**, without a keyboard suffix.

The survey now uses `TrackedDeviceGraphicRaycaster` from XRI rather than a bespoke global-input/ray fallback. Six slider change events update their recorded scores; Submit emits one result per displayed survey. The existing first-screen focus and button-release guard remains. XRI pointer-down/up/click owns confirmation: a press started on an old control cannot activate a newly created control.

## Voice phrasing

**Implemented:** four ordinary alternatives per direction for the neutral voice and self-similar left hints; self-similar right hints have three after removing “I’ll try looking right.” Each direction cycles through its allowed variants before repeating, starting at a reproducible participant/trial offset. The semantic direction remains separate from wording, so a new wording is not mistaken for a gaze reversal. All variants are included in voice preparation for both conditions. Actual audio text and voice remain recorded by the existing audio telemetry.

| External voice | Self-similar voice |
| --- | --- |
| Look left. | I need to look left. |
| Look to your left. | I need to look to my left. |
| Try looking left. | I'll try looking left. |
| Search to your left. | I need to search to my left. |
| Look right. | I need to look right. |
| Look to your right. | I need to look to my right. |
| Try looking right. | Removed from the self-similar rotation. |
| Search to your right. | I need to search to my right. |

**Implemented:** when the shortest horizontal angle from the current eye-gaze direction to the actual target object exceeds 90° (two 45° wall steps), the hint is “Look way left/right.” / “I need to look way left/right.” At or below 90°, ordinary variants apply. This live gaze-relative angle is independent of the balanced trial theta schedule. Both stronger cues are included in startup audio preparation and normal audio telemetry. Direction changes and tracking loss still cancel stale hints; crossing the near/far threshold updates the next hint without treating it as a reversal.

The target-zone interruption remains “You're in the correct area. Keep looking.” / “I'm in the correct area. I need to keep looking.” Reversing direction or losing tracking still cancels a stale hint. Entering the correct zone still interrupts current speech.

**Implemented — guidance interruptions:** area entry uses the target wall's horizontal sector rather than the wall's height bounds. Scanning up/down does not count as exit/reentry; near-vertical gaze without a reliable horizontal bearing abstains and does not rearm the area announcement. A 100 ms confirmed visit followed by a 250 ms horizontal exit enables “Go back. That was the correct area.” / “I need to go back. That was the correct area.” The return cue expires two seconds after exit, requires another confirmed visit before repeating, and has a six-second cooldown. Tracking loss clears the return history. These are beta tuning values, not validated fixation thresholds.

Prepared area/return cues can replace ordinary hints using the existing 40 ms fade; target announcements, intros and audio checks remain protected. Canceled fades no longer consume cue cooldowns. Arrival also cancels stale directional speech during the area-announcement cooldown. Regaining the area cancels a stale return cue. Return interruptions use the existing `audio_area_correction` event with reason `gaze_overshoot_return`.

**Implemented — audio checks:** both neutral and self-similar voices say “Hover your controller over Accept Audio and press the trigger to confirm this is an acceptable audio level.” The Accept audio button remains unavailable until playback completes successfully.

**Implemented:** two ten-trial measured blocks, plus two separate practice trials. Each consecutive five-trial half within a block contains each absolute angle once, in seeded random order. Directions mirror across paired assignments in the other block. See [angle schedule](balanced-angle-schedule.md). Small turns may still occur early; the first five cannot omit the larger-angle categories.

## Avatars

Generated with the built-in imagegen tool, one request per fictional character. Source assets: `Assets/Resources/VoiceAvatars/{Eric,Roger,Janet,Sarah}.png`. Unity imports each at maximum 256 px with alpha transparency. These are decorative fictional illustrations, not representations of the actual speakers. All four use the same size and card layout; pointing a controller at a card plays its voice sample once per entry. Moving to the other card interrupts and previews that voice; holding still does not loop. Pulling the trigger selects a card with a persistent gold border while both avatars stay visible. The bottom Back button becomes Confirm. Hovering another card never changes the pending selection. Confirm becomes available after the selected voice has completed a successful audition and playback is idle; only confirmation commits the choice to the study. A failed sample can be retried by pointing away and back.

Prompt set: “Create one tiny UI voice-selector avatar for a fictional [gender] voice named [name]. Cute low-fidelity flat 2D cartoon head and shoulders, rounded geometric shapes, dark charcoal clean outline, very minimal shading, friendly closed smile, [appearance]. Front view centered bust fills 80% square. Restrained palette teal cream muted coral navy, highly legible at 96 pixels. Transparent background. No text, no letters, no logos, no accessories, no sound waves. Fictional character, not a likeness of a real speaker.”

- Eric: male; warm medium skin, short dark wavy hair, teal crewneck (prompt also described vector-like styling).
- Roger: male; fair peach skin, short side-parted sandy brown hair, simple short brown beard, muted coral crewneck.
- Janet: female; warm brown skin, short rounded dark curly bob, teal crewneck.
- Sarah: female; fair peach skin, straight chestnut shoulder-length hair with side part, muted coral crewneck.

## Verification and headset check

Editor checks exercise actual Button pointer callbacks for gender/voice/back, hover and pointer-down without activation, all four loaded avatars, no-audio acceptance gate, six distinct survey values saved once, and visible Finish. The voice and rotational shell suites cover first-person variants, phrase cycling, angle quotas/halves/mirrors/reproducibility across 50 IDs, target positions and height/spacing constraints. The run sheet compiles in the native editor.

**Open verification:** with each physical controller, point at every setup control, move off/re-enter, hold the ray steady, and confirm one entry pulse with a blue highlight and no repeated pulses. Test A/B away from controls, held-trigger screen transitions, clone Finish recording, disabled Accept while audio plays, both voices, six survey sliders, Submit, and the block-break Continue. Run both full ten-trial blocks; compare saved schedule, actual targets, hint interruptions and recorded survey values. Hardware haptic feel and end-to-end headset UI usability require this beta test.

## Guidance boundary wrist option

**Implemented:** Wrist menu → Settings → **Guidance boundary** shows/hides all eight wall outlines and degree labels together (formerly labeled Degree guides). Point at the switch and pull the trigger; the shared ray highlight and entry pulse apply. The choice remains in effect for later trials and newly created frames during the same session. Reopening the menu reflects the current value. This controls the guide visuals only: objects, search-zone geometry, gaze hints and readiness remain active. The public `FindObjectGameManager.SetDegreeGuidesVisible(bool)` API provides the same action. The existing default is visible; this change does not add a device-wide saved preference.
