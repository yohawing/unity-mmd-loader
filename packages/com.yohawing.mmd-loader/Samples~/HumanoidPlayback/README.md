# Humanoid Playback sample

This sample is a complete, redistributable Unity Humanoid retarget-playback
example. It is separate from **Basic Playback**: use Basic Playback for native
PMX/VMD Timeline playback, and this sample when a standard Unity Humanoid
`AnimationClip` or FBX motion is required.

## Open and play

1. Import **Humanoid Playback** from Package Manager.
2. Open `Assets/HumanoidPlayback.unity` from the imported sample folder.
3. Enter Play Mode. The `Humanoid Timeline` PlayableDirector automatically plays
   the 39.7-second `TaisouMocap.fbx` clip through
   `MmdHumanoidAnimationTrack`.

The scene contains a Humanoid-imported PMX hierarchy, a valid Avatar/Animator, a
playback controller, a Timeline binding, camera, and directional light.

## Included assets

- `HumanoidSampleModel.pmx`: redistributable model source copied from the Basic
  Playback fixture and imported as Humanoid.
- `TaisouMocap.fbx`: redistributable, already-captured Humanoid exercise motion
  used by the ready-to-play Timeline and by this sample's motion-quality check.
- `HumanoidSampleTimeline.playable`: `MmdHumanoidAnimationTrack` with the FBX
  motion clip.
- `HumanoidPlayback.unity`: ready-to-play scene.
- `ASSET_PROVENANCE.md`: redistribution provenance for the bundled assets.

## Boundaries

The FBX clip demonstrates ordinary Unity Humanoid retarget playback. It does not
reproduce native MMD IK/helper behavior, facial morphs, or Live physics. Use
native PMX/VMD playback when those behaviors are required. VMD-to-Humanoid bake
remains an explicit authoring workflow; its setup and generated assets are
intentionally not bundled in this playback sample.

The short `HumanoidSampleMotion.vmd` used by contract fixtures is only for
conversion-contract checks. It is not bundled in this sample and is not a
natural-motion quality example. `TaisouMocap.fbx` is the practical quality
sample.
