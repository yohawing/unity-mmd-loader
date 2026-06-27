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

Add a `.pmx` file under your Unity project's `Assets/` folder.

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

> Credits — Model: [Sour](https://bowlroll.net/file/146103) / Motion: [mobiusP](https://www.nicovideo.jp/watch/sm42576784)
