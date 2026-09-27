# Controller-ray menus and directional hints

**Implemented — beta, 2026-09-27.** Point at a control, see its blue hover feedback and feel one 0.04-second pulse on that controller, then pull the trigger. Hover alone never activates a button. A/B, keyboard digits, and global trigger shortcuts no longer drive setup, checkpoints or surveys. Wrist/tutorial controls use their existing XRI pointer actions with shared hover feedback. Global EventSystem navigation/Submit is disabled; gaze UI interaction is disabled. Physical UI rays use their existing left/right Trigger bindings.

Visible controls cover Start setup; gender; the two avatar voice cards; Use this voice / Hear the other voice / Back; Finish recording; preparation retry / re-record / cancel; audio Accept / Replay / Restart; Center and begin; practice/block/pause checkpoints; NASA-TLX sliders and Submit; Finish and Reset to Start. Disabled audio acceptance cannot bypass failed or unfinished playback. The clone page says **Finish recording**, without a keyboard suffix.

The survey now uses `TrackedDeviceGraphicRaycaster` from XRI rather than a bespoke global-input/ray fallback. Six slider change events update their recorded scores; Submit emits one result per displayed survey. The existing first-screen focus and button-release guard remains. XRI pointer-down/up/click owns confirmation: a press started on an old control cannot activate a newly created control.

## Voice phrasing

**Implemented:** the original phrases plus three alternatives in each direction. Each direction cycles through all four before repeating, starting at a reproducible participant/trial offset. The semantic direction remains separate from wording, so a new wording is not mistaken for a gaze reversal. All variants are included in voice preparation for both conditions. Actual audio text and voice remain recorded by the existing audio telemetry.

| External voice | Self-similar voice |
| --- | --- |
| Look left. | I need to look left. |
| Look to your left. | I need to look to my left. |
| Try looking left. | I'll try looking left. |
| Search to your left. | I need to search to my left. |
| Look right. | I need to look right. |
| Look to your right. | I need to look to my right. |
| Try looking right. | I'll try looking right. |
| Search to your right. | I need to search to my right. |

The target-zone interruption remains “You're in the correct area. Keep looking.” / “I'm in the correct area. I need to keep looking.” Reversing direction or losing tracking still cancels a stale hint. Entering the correct zone still interrupts current speech.

**Implemented:** two ten-trial measured blocks, plus two separate practice trials. Each consecutive five-trial half within a block contains each absolute angle once, in seeded random order. Directions mirror across paired assignments in the other block. See [angle schedule](balanced-angle-schedule.md). Small turns may still occur early; the first five cannot omit the larger-angle categories.

## Avatars

Generated with the built-in imagegen tool, one request per fictional character. Source assets: `Assets/Resources/VoiceAvatars/{Eric,Roger,Janet,Sarah}.png`. Unity imports each at maximum 256 px with alpha transparency. These are decorative fictional illustrations, not representations of the actual speakers. All four use the same size and card layout; selecting a card plays the voice sample.

Prompt set: “Create one tiny UI voice-selector avatar for a fictional [gender] voice named [name]. Cute low-fidelity flat 2D cartoon head and shoulders, rounded geometric shapes, dark charcoal clean outline, very minimal shading, friendly closed smile, [appearance]. Front view centered bust fills 80% square. Restrained palette teal cream muted coral navy, highly legible at 96 pixels. Transparent background. No text, no letters, no logos, no accessories, no sound waves. Fictional character, not a likeness of a real speaker.”

- Eric: male; warm medium skin, short dark wavy hair, teal crewneck (prompt also described vector-like styling).
- Roger: male; fair peach skin, short side-parted sandy brown hair, simple short brown beard, muted coral crewneck.
- Janet: female; warm brown skin, short rounded dark curly bob, teal crewneck.
- Sarah: female; fair peach skin, straight chestnut shoulder-length hair with side part, muted coral crewneck.

## Verification and headset check

Editor checks exercise actual Button pointer callbacks for gender/voice/back, hover and pointer-down without activation, all four loaded avatars, no-audio acceptance gate, six distinct survey values saved once, and visible Finish. The voice and rotational shell suites cover first-person variants, phrase cycling, angle quotas/halves/mirrors/reproducibility across 50 IDs, target positions and height/spacing constraints. The run sheet compiles in the native editor.

**Open verification:** with each physical controller, point at every setup control, move off/re-enter, hold the ray steady, and confirm one entry pulse with a blue highlight and no repeated pulses. Test A/B away from controls, held-trigger screen transitions, clone Finish recording, disabled Accept while audio plays, both voices, six survey sliders, Submit, and the block-break Continue. Run both full ten-trial blocks; compare saved schedule, actual targets, hint interruptions and recorded survey values. Hardware haptic feel and end-to-end headset UI usability require this beta test.
