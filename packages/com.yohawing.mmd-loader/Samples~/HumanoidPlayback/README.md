# Humanoid Playback sample

This sample is a complete, redistributable Humanoid bake example. It is separate from `Basic Playback`: use Basic Playback for native PMX/VMD Timeline playback, and this sample when a standard Unity Humanoid `AnimationClip` is required.

## Open and play

1. Import **Humanoid Playback** from Package Manager.
2. Open `Assets/HumanoidPlayback.unity` from the imported sample folder.
3. Enter Play Mode. `Humanoid Sample Timeline` loops the baked clip through `MmdHumanoidAnimationTrack`.

The scene contains a Humanoid-imported PMX hierarchy, a valid Avatar/Animator, a playback controller, a Timeline binding, camera, and directional light.

## Included source and generated assets

- `HumanoidSampleModel.pmx`: redistributable model source copied from the Basic Playback fixture and imported as Humanoid.
- `HumanoidSampleMotion.vmd`: redistributable VMD source.
- `HumanoidSampleSetup.asset`: setup metadata and mapping diagnostics.
- `HumanoidSampleMotion.anim`: explicit 30 fps, frames 0–5 Humanoid muscle-curve bake.
- `HumanoidSampleTimeline.playable`: `MmdHumanoidAnimationTrack` with the baked clip.
- `HumanoidPlayback.unity`: ready-to-play scene.

To repeat the workflow yourself, follow [`docs/HOW_TO_USE.md`](https://github.com/yohawing/unity-mmd-loader/blob/main/docs/HOW_TO_USE.md#8-optional-bake-a-humanoid-animationclip): set the PMX Rig to Humanoid, create a Setup Asset, assign the VMD and frame range, then click **Create Humanoid AnimationClip Asset**.

## Boundaries

The baked clip contains Humanoid muscle rotations. Root translation, Live physics, facial morphs, and exact native MMD IK/helper behavior are not baked. Use native PMX/VMD playback when those behaviors are required.

The PMX/VMD pair originates from the self-generated redistributable `maya_mmd_tools` test fixture already used by Basic Playback. No third-party licensed MMD asset is included.
