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

1. Open **Project Settings > Graphics** and confirm the active URP Asset.
2. Open the Renderer Data asset referenced by that URP Asset.
3. Add **MmdSelfShadowRendererFeature** to the Renderer Features list.
4. Keep the feature enabled. The default shadow map size and bias are intended to be usable as a first setup.
5. If you use multiple Renderer Data assets, add the feature to each renderer that can render the MMD scene.

The PMX importer generates materials for the `MMD Basic URP Toon` shader. Self-shadow rendering requires those generated materials, because they include the dedicated MMD self-shadow caster pass. If you replace materials manually, make sure the replacement shader is intended for this package's MMD rendering path.

## 7. Set up the Scene environment

The scene needs the playback object from the PMX placement step and a scene environment binding for camera, light, and self-shadow state.

1. Place the PMX in the Scene so the MMD playback object and `MmdUnityPlaybackController` exist.
2. Add `MmdSceneEnvironmentBinding` to a scene GameObject. A small empty GameObject such as `MMD Scene Environment` is fine.
3. Assign **Target Camera** to the Camera that VMD camera motion should drive.
4. Assign **Target Light** to a Directional Light if the VMD light track should drive scene light color and direction.
5. Leave **Self Shadow Enabled** on when you want MMD self-shadow rendering. A VMD with self-shadow keys can drive this state; when the VMD has no self-shadow keys, the binding's enabled default state is used instead.
6. Optional: assign **Self Shadow Direction Light** when you want a specific Directional Light to provide the self-shadow direction without making it the VMD light target.
7. Bind the same scene environment object to the Timeline track or clip that drives VMD camera/light scene motion, then bind the MMD playback object for model playback.

You do not need to add hidden self-shadow helper components by hand. The PMX placement/factory path creates the required internal hookup for the model root.

If self-shadow does not appear, check the setup in this order:

- The Game View is using a URP Renderer Data asset that contains **MmdSelfShadowRendererFeature**.
- The PMX object is in the Scene and still has its generated `MMD Basic URP Toon` materials.
- `MmdSceneEnvironmentBinding` exists in the Scene and **Self Shadow Enabled** is on.
- The Timeline binding points at the scene environment object when VMD scene motion is used.
- The VMD self-shadow state is not explicitly off.
- Frame Debugger shows **MMD Self Shadow Pass** during rendering.

For lower-level implementation details, see
[MMD_SELF_SHADOW.md](./MMD_SELF_SHADOW.md).

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784)
