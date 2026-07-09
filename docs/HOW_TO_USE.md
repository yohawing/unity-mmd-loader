# How to use MMD Loader

This guide is for users who have added `com.yohawing.mmd-loader` to a Unity project as a UPM package.

## 1. Add the package

![howtouse1](./assets/howtouse1.png)

Open **Window > Package Manager** in Unity, and enter the following into **Add package from git URL**.

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

The supported Unity version is 6000.4 or newer.

## 2. Optional: import the Basic Playback sample

In Package Manager, open the MMD Loader package and import the **Basic Playback** sample from the **Samples** section.

The sample includes a small redistributable PMX/VMD pair that is useful for checking the release golden path before using third-party MMD assets.

## 3. Import a PMX

![howtouse2](./assets/howtouse2.png)

Add a `.pmx` file, along with its texture files, under your Unity project's `Assets/` folder.

A PMX is imported as a model file, just like an FBX. You can adjust the import settings in the Inspector.

## 4. Place it in the Scene

![howtouse3](./assets/howtouse3.png)

Drag the PMX asset from the Project window into the Scene or Hierarchy.

This creates a playback object in the scene. Even when only a PMX is placed, the playback controller is kept, so you can add a VMD to the Timeline later.

## 5. Import a VMD

![howtouse4](./assets/howtouse4.png)

Add a `.vmd` file under your Unity project's `Assets/` folder.

A VMD asset is referenced by Timeline clips and by the runtime playback source. It is not designed so that you create a separate, duplicated asset from the original VMD data through the normal workflow.

Bind the scene's MMD playback object to the Timeline and create a VMD Timeline clip.

The available editor actions may change between package versions, but the basic idea is as follows.

- A PMX asset creates the scene's playback controller.
- A VMD asset is referenced from a Timeline clip.
- A Timeline clip does not bake the VMD into an AnimationClip right away; it passes the playback time to MMD's runtime evaluation.

## 6. Set up rendering in URP

MMD Loader expects a URP project. If your project uses multiple URP assets or quality levels, check the Renderer Data that is actually used by the Game View or build target.

![howtouse5](./assets/howtouse5.png)

1. Open **Project Settings > Graphics** and confirm the active URP Asset.
2. Open the Renderer Data asset referenced by that URP Asset.
3. Add **MmdSelfShadowRendererFeature** to the Renderer Features list.
4. Keep the feature enabled. The default shadow map size and bias are intended to be usable as a first setup.
5. If you use multiple Renderer Data assets, add the feature to each renderer that can render the MMD scene.

The PMX importer generates materials for the `MMD Basic URP Toon` shader. Self-shadow rendering requires those generated materials, because they include the dedicated MMD self-shadow caster pass. If you replace materials manually, make sure the replacement shader is intended for this package's MMD rendering path.

### Self-shadow troubleshooting

If the model renders but MMD self-shadow does not appear, check these items in order:

1. Confirm that **MmdSelfShadowRendererFeature** is added to the Renderer Data that is actually used by the current URP Asset and Quality level. If multiple Renderer Data assets exist, the feature must be on the one rendering the MMD scene.
2. Confirm that the placed PMX still uses generated `MMD Basic URP Toon` materials. Replaced materials need a `MmdSelfShadowCaster` pass, otherwise the render pass reports `NoCasterPass`.
3. Confirm that the scene contains one active `MmdSceneEnvironmentBinding`. Multiple active bindings can make the hidden self-shadow target report `AmbiguousEnvironment`.
4. Confirm that **Self Shadow Enabled** is on in `MmdSceneEnvironmentBinding`. The binding records VMD self-shadow state as MMD render state; it does not change Unity `Light.shadows`, `RenderSettings`, or `QualitySettings.shadowDistance`.
5. Treat binding-local `Active` as "self-shadow state was recorded", not as proof that the shadow map rendered. Target readiness, RendererFeature setup, bounds, and caster passes must also be valid.
6. If there is no VMD self-shadow track, the binding uses a default MMD self-shadow state. If the VMD self-shadow mode disables shadows, the diagnostic state is `ModeDisabled`.
7. If the character has no visible renderer bounds, the render pass reports `NoBounds`. Check that the placed PMX hierarchy is active and visible.

For the diagnostic layer definitions, see `docs/MMD_SELF_SHADOW.md`.

## 7. Set up the Scene environment

The scene needs the playback object from the PMX placement step and a scene environment binding for camera, light, and self-shadow state.


![howtouse6](./assets/howtouse6.png)

1. Place the PMX in the Scene so the MMD playback object and `MmdUnityPlaybackController` exist.
2. Add `MmdSceneEnvironmentBinding` to a scene GameObject. A small empty GameObject such as `MMD Scene Environment` is fine.
3. Assign **Target Camera** to the Camera that VMD camera motion should drive.
4. Assign **Target Light** to a Directional Light if the VMD light track should drive scene light color and direction.

## Credits

- Model: [Sour](https://bowlroll.net/file/146103) 
- Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784)
