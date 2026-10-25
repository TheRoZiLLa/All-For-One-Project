# Phase Recorder: playable loop

PROLOGUE is the enabled build entry. Its existing Player, Dev GameTest and the Player prefab have PhaseRecorder and PhaseRecorderHUD.

- F: enter the hologram planning world; F again saves the take and returns to the entry pose.
- G: play the saved hologram in the main world. Playback can be repeated after it finishes. Starting a new recording removes a currently playing hologram.
- Backspace: cancel this take and return. The previous saved take survives cancellation and sanity overrun.
- At full sanity, recording lasts about 12 seconds. The last 25% warns in red. Zero sanity takes control for two seconds, discards the unfinished take, then forces a return. Sanity recovers in the main world; re-entry requires at least 25%.

Recording stores movement, camera pitch, shot origins/directions/ammo colors, and the security kiosk's E interaction. Main-world AI, bodies, door movement, RGB timers and delayed enemy/color effects pause while planning/returning. The player uses temporary ammo; the original chambers and selected index restore on return. Replayed shots affect the current main world and do not spend the player's ammunition. Walls still stop them.

The preview uses opaque cyan scan surfaces so walls remain readable. It shows shot feedback but deliberately defers target color/physics and kiosk consequences until playback; it is a plan, not a second physics simulation. Recorded references and the single saved take live only in the current scene/session. A new puzzle interaction must explicitly join recording/playback before using it in a hologram puzzle. Hologram appearance is still a placeholder body rather than a full animated character.

Verification: PhaseRecorderVerificationTests.VerifyInPlayMode() checks actual gun recording/replay, world materials and body isolation/restoration, manual playback, ammo/index restoration, kiosk timing, duplicate playback prevention, cancellation, disable recovery, sanity exhaustion and re-entry lock. Existing movement, RGB and held-revolver checks cover integration.
