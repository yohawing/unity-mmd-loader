# Humanoid Playback sample

This sample is a complete, redistributable Unity Humanoid retarget-playback example. It is separate from `Basic Playback`: use Basic Playback for native PMX/VMD Timeline playback, and this sample when a standard Unity Humanoid `AnimationClip` or FBX motion is required.

## Open and play

1. Import **Humanoid Playback** from Package Manager.
2. Open `Assets/HumanoidPlayback.unity` from the imported sample folder.
3. Enter Play Mode. `Humanoid Sample Timeline` plays the 39.7-second `TaisouMocap.fbx` clip through `MmdHumanoidAnimationTrack`.

The scene contains a Humanoid-imported PMX hierarchy, a valid Avatar/Animator, a playback controller, a Timeline binding, camera, and directional light.

## Included source and generated assets

- `HumanoidSampleModel.pmx`: redistributable model source copied from the Basic Playback fixture and imported as Humanoid.
- `TaisouMocap.fbx`: redistributable, already-captured Humanoid exercise motion used by the ready-to-play Timeline.
- `HumanoidSampleTimeline.playable`: `MmdHumanoidAnimationTrack` with the FBX motion clip.
- `HumanoidPlayback.unity`: ready-to-play scene.
- `ASSET_PROVENANCE.md`: redistribution provenance for the bundled FBX motion.

## Boundaries

The FBX clip demonstrates ordinary Unity Humanoid retarget playback. It does not reproduce native MMD IK/helper behavior, facial morphs, or Live physics. Use native PMX/VMD playback when those behaviors are required. VMD-to-Humanoid bake remains an explicit authoring workflow documented in [`docs/HOW_TO_USE.md`](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md#8-optional-bake-a-humanoid-animationclip); its setup and generated assets are intentionally not bundled in this playback sample.

The PMX originates from the self-generated redistributable `maya_mmd_tools` test fixture already used by Basic Playback. No third-party licensed MMD asset is included.
