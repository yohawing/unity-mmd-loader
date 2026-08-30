# How to use MMD Loader

This guide is for users who have added `com.yohawing.mmd-loader` to a Unity project as a UPM package.

## Contents

- [How to install](#how-to-install)
- [Import a PMX](#import-a-pmx)
- [Place it in the Scene](#place-it-in-the-scene)
- [Import a VMD](#import-a-vmd)
- [Play models with automatic worker playback](#play-models-with-automatic-worker-playback)
- [Set up Humanoid](#set-up-humanoid)
- [Set up rendering in URP](#set-up-rendering-in-urp)
- [Set up camera and light motion](#set-up-camera-and-light-motion)
- [Credits](#credits)

## How to install

![howtouse1](./assets/howtouse1.png)

In Unity, open **Window > Package Manager** and enter the following URL under **Add package from git URL**.

```text
https://github.com/yohawing/unity-mmd-loader.git?path=packages/com.yohawing.mmd-loader
```

The release target is Unity 6000.0 LTS or newer with URP 17 on Windows x86_64.

## Import a PMX

![howtouse2](./assets/howtouse2.png)

Add a `.pmx` file, along with its texture files, under your Unity project's `Assets/` folder.

A PMX is imported as a model file, just like an FBX. You can adjust the import settings in the Inspector.

## Place it in the Scene

![howtouse3](./assets/howtouse3.png)

Drag the PMX asset from the Project window into the Scene or Hierarchy.

This creates a playback object in the scene. Even when only a PMX is placed, the playback controller is kept, so you can add a VMD to the Timeline later.

## Import a VMD

![howtouse4](./assets/howtouse4.png)

Add a `.vmd` file under your Unity project's `Assets/` folder.

A VMD asset is referenced by Timeline clips and runtime playback. In normal use, you do not need to create a separate asset that duplicates the original VMD data.

Bind the scene's MMD playback object to the Timeline and create a VMD Timeline clip.

The available editor actions may change between package versions, but the basic idea is as follows.

- A PMX asset creates the scene's playback controller.
- A VMD asset is referenced from a Timeline clip.
- A Timeline clip does not immediately convert the VMD into an AnimationClip. It passes the playback time to the MMD runtime, which calculates the pose at that time.

## Play models with automatic worker playback

Native playback uses long-lived workers automatically from the first eligible model. No worker component or controller list is required.

1. Configure each scene playback controller with its PMX and VMD assets.
2. Select **Physics Mode Off** or **Physics Mode Live** on each controller.
3. Enable **Play On Start**, then enter Play Mode.

Eligible standalone controllers dispatch their evaluations before any controller waits for a result. Completed poses are applied on the main thread before the normal `Update` callbacks, so multiple models can evaluate concurrently without moving Unity object access off the main thread. Controllers may use different frame rates or mix the supported physics modes.

Normal VMD Timeline clips in **Physics Mode Off** use the same automatic worker path. Tracks and Playable Directors evaluated in the same frame are dispatched as one batch, then applied before `LateUpdate`. Timeline Live Physics and Humanoid retarget input continue to use their synchronous compatibility paths.

A single eligible model also uses a worker. This removes the separate one-model and multi-model setup, but one-model playback may cost more than the former serial path. If native worker setup is unavailable, standalone playback keeps the synchronous path. A worker execution fault is isolated to that controller and logs a warning. Reassign its PMX/VMD source, reconfigure the controller, explicitly re-enable fast runtime, or re-enable the component to retry.

## Set up Humanoid

Retarget the motion onto a standard Unity Humanoid rig.

**1. Set the PMX Rig to Humanoid, Apply, then place it in the Scene.**

![howtouse7](./assets/howtouse7.png)

In the PMX Import Settings, open the **Rig** tab, set **Animation Type** to **Humanoid**, and click **Apply**. Then drag the PMX into the Scene.

**2. Add an MMD Humanoid Animation Track to the Timeline and bind it.**

![howtouse8](./assets/howtouse8.png)

Add an **MMD Humanoid Animation Track** and bind it to the scene's `MmdUnityPlaybackController`.

> **Note:** Complex rigs are not supported. Models that rely heavily on arm IK or similar setups may not retarget to the correct pose.

## Set up rendering in URP

MMD Loader expects a URP project. If your project uses multiple URP assets or quality levels, check the Renderer Data that is actually used by the Game View or build target.

![howtouse5](./assets/howtouse5.png)

1. Open **Project Settings > Graphics** and confirm the active URP Asset.
2. Open the Renderer Data asset referenced by that URP Asset.
3. Add **MmdSelfShadowRendererFeature** to the Renderer Features list.
4. Keep the feature enabled. You can start with the default shadow map size and bias.
5. If you use multiple Renderer Data assets, add the feature to each renderer that can render the MMD scene.

## Set up camera and light motion

Use a dedicated Timeline track to play VMD camera and light motion.

**1. Bind the target Camera and Light to `MmdSceneEnvironmentBinding`.**

![howtouse6](./assets/howtouse6.png)

Add `MmdSceneEnvironmentBinding` to a scene GameObject, then assign **Target Camera** and **Target Light**.

**2. Add an MMD VMD Camera Track and drive it with a camera VMD.**

![howtouse9](./assets/howtouse9.png)

Add an **MMD VMD Camera Track** to the Timeline, bind it to the `MmdSceneEnvironmentBinding`, then add a clip and assign the camera **VMD Asset**. Its camera, light, and self-shadow keyframes drive the assigned targets.

## Credits

- Model: [Sour](https://bowlroll.net/file/146103) 
- Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784)
