# Architecture

This file keeps stable design decisions that have already been backed by
contract tests or validation gates.

## Refactoring Guardrails

Refactoring may change private file layout and implementation ownership, but it
must preserve the public boundaries below unless an explicit compatibility
decision changes them. `CompatibilitySurfaceContractTests` pins serialized field
names and types for `MmdPmxAsset`, `MmdVmdAsset`, and
`MmdUnityPlaybackController`; selected public properties and method signatures;
the package assembly-definition inventory; public playback and live-physics
diagnostic field names and types; and the managed `DllImport` entry-point map.
The test is a boundary inventory, not permission to add new public surface.
When a boundary intentionally changes, update the contract test, migration
notes, and the affected consumer evidence in the same reviewed slice.

Committed PMX/VMD goldens are immutable test inputs. Normal contract tests fail
when a committed golden is absent or mismatched; they never create replacement
goldens as part of a passing run. Maintainers may opt in to
`YMU_GENERATE_GOLDENS=1` to write reviewed candidates under the ignored
`artifacts/golden-candidates/` directory. Candidates require explicit review,
provenance, and promotion into tracked fixtures; candidate generation alone is
not compatibility evidence.

NUnit evidence is green only when the XML exists and is well-formed, has a
non-zero and internally consistent test count, has no failed, inconclusive, or
invalid tests, meets the configured minimum passed/total counts, and contains
only explicitly allowlisted skipped test names. Release evidence additionally
requires a configured Unity license, successful native platform builds, and
complete EditMode and PlayMode XML; a missing license or an unverified skip is
an evidence failure, not a release pass.

Serialized PMX/VMD assets keep source bytes, metadata, and Unity sub-assets as
their compatibility surface. Synchronous PMX playback state is owned by
`MmdPmxPlaybackCache`; VMD native context and raw-source readback state are
owned by `MmdVmdNativeContextCache`. Source replacement invalidates the
PMX cache under one synchronization boundary. The VMD cache owns its native
cleanup/retry boundary, while `MmdVmdAsset` retains `OnDisable` as the native
cleanup delegation point. A same-source faulted or canceled VMD preload is
evicted before a later preload attempt; a successful task remains coalesced.
`BeginNativePlaybackPreload` is an advisory, fire-and-forget latency
optimization; synchronous `TryGetOrCreateNativeVmdContext` remains the
playback authority.

The controller's raw importer, provider-model, and direct asset routes share
the immutable `MmdNativePlaybackSetup` and the final native-first transaction,
but they intentionally keep source acquisition separate. PMX bytes may be
borrowed from an asset cache while VMD bytes and a shared native context have
different owners. `MmdVmdNativeContextCache.SourceSnapshot` now carries a
monotonic generation so a caller that crossed a source invalidation is rejected
before it starts a new native task. A deterministic EditMode contract also
drives a pending preload through a main-thread raw-source replacement and
checks stale-caller rejection plus exactly-once cleanup. This does not remove
the existing synchronous cleanup wait, cover cleanup-failure ownership, or
establish the native-free thread-affinity contract.
Do not introduce a generic prepared-source abstraction or move native cleanup
off the current owner boundary until buffer lifetime, stale-result disposal,
and native-free thread contract are covered by deterministic tests.
VMD source/readback synchronization remains a separate follow-up contract.

## Format And Native Boundaries

PMX / VMD parsing is owned by the Format layer under
`packages/com.yohawing.mmd-loader/Runtime/Format/Parser`. The public managed
namespace remains `Mmd.Parser` so existing runtime, editor, tests,
and dotnet smoke callers do not take a source-breaking API change from the
folder move.

`NativeMmdParser` calls the `mmd_runtime_ffi` non-JSON summary entry points
through `MmdParserFfiMethods` summary handles:

- PMX: `mmd_runtime_pmx_summary_create_from_bytes`
- VMD: `mmd_runtime_vmd_summary_create_from_bytes`

The summary handles are copied immediately into managed source snapshots and
then into the neutral PMX/VMD IR validated by `PmxIrContractTests` and
`VmdIrContractTests`. Empty parser input is rejected before native calls, native
summary handles are disposed deterministically, and parser errors must not
include local asset paths unless the caller explicitly logs those paths.

Format adapters keep parser indices as stable ids and preserve MMD-space values
until a later Unity conversion boundary. The PMX adapter normalizes unsupported
or malformed parser details at the IR boundary: unused or negative skin bone
slots become a valid bone index with zero weight, non-finite normals become a
finite fallback normal, material names and physics names get deterministic
fallbacks, material color/alpha values are clamped to finite descriptor values,
and material `vertexCount` is derived from face count. SDEF C/R0/R1 arrays are
present only when `hasSdefParameters` is true; non-SDEF vertices must carry empty
SDEF arrays.

PMX physics descriptors are part of neutral IR. The parser emits rigidbody and
joint descriptors even when a runtime path ignores them. This keeps parser
coverage independent from the selected physics mode.

The VMD adapter preserves bone and morph keyframes plus property frames as
`modelKeyframes` with `visible` and `constraintStates`. VMD interpolation bytes
are split into `translationX`, `translationY`, `translationZ`, and `rotation`
channels; Bezier evaluation belongs to motion sampling, not the parser adapter.
Current adapter output sets `physicsEnabled` to false on bone keyframes.

Runtime correctness traces use MMD-space values. Unity coordinate conversion is
an import/rendering boundary and must not be mixed into motion sampling, append
transform, IK, morph evaluation, or trace comparison. PMX and VMD neutral IR
store parser-provided MMD-space values, runtime motion sampling returns
MMD-space local transforms, pose propagation returns MMD-space matrices, and
trace schema records MMD-space values when `space` is `mmd`.

Unity-facing integration converts MMD display values only at boundaries such as
mesh upload, GameObject transform binding, SkinnedMeshRenderer binding,
material/rendering integration, and visual capture. The current basis conversion
is `[-x, y, -z]` for positions/normals and `[-x, y, -z, w]` for quaternions.
Trace quaternions remain `[x, y, z, w]`, and trace `worldMatrix` stays row-major
with translation in indices 3, 7, and 11.

The old nanoem compatibility parser path is removed. Do not reintroduce
`native/src/ymu_api.*`, `native/nanoem`, `yohawing_mmd_unity_native.dll`, or the
old `MmdNativeMethods` handle/accessor wrapper for PMX/VMD parsing.

## Neutral IR Shape

PMX neutral IR is the managed parser output consumed by import, animation,
rendering, physics descriptor handoff, and trace generation. It is not a full
PMX schema. Required top-level data includes model name, vertices, indices,
bones, morphs, materials, IK definitions, and physics descriptors.

PMX vertex records keep stable parser indices, MMD-space position/normal/UV,
normalized skinning mode, paired bone indices/weights, and conditional SDEF
C/R0/R1 arrays. Bone records keep stable parser indices, MMD names, parent and
append parent ids, transform order, MMD-space origin, motion flags, append
flags, fixed-axis data, local-axis data, and external-parent flags. IK records
keep the IK bone id, target bone id, iteration/angle settings, and per-link
limit data.

PMX morph records keep stable parser indices, names, type, panel, and payload
collections for vertex, group, material, UV, bone, flip, and impulse morphs.
Runtime evaluators decide which payloads have current effects; parser IR must
preserve payloads even when a runtime path is deferred. Material records keep
stable indices, names, texture/sphere/toon references, alpha and color values,
edge data, sphere mode, toon shared flag, culling policy, draw-edge flag, and
material vertex count. PMX physics records keep rigidbody and joint descriptors,
including names, linked indices, shape/size/transform values, mass/damping,
friction/restitution, collision group/mask, physics kind, joint limits, and
springs.

VMD neutral IR is the managed parser output consumed by motion sampling. It
contains target model name, max frame, bone keyframes, morph keyframes, and
model keyframes. Bone keyframes keep MMD bone name, frame, MMD-space local
translation, quaternion rotation, per-channel interpolation bytes
(`translationX`, `translationY`, `translationZ`, `rotation`), and the reserved
`physicsEnabled` parser field. Morph keyframes keep target morph name, frame,
and finite weight. Model keyframes preserve VMD property-frame visibility and
constraint/IK enable states. Camera, light, and self-shadow frame payloads are
preserved in neutral IR as raw scene-motion data; self-shadow stores only VMD
`frame`, `mode`, and `distance`. PMX materials keep a ShadowCaster baseline for
the dedicated MMD self-shadow map, but MMD toon ForwardLit shading does not use
Unity/URP main-light shadow attenuation. ShadowCaster alpha discard uses
material binding policy:
opaque materials keep a zero shadow alpha threshold, while alphaBlend/alphaTest
materials clip fully transparent texels. VMD self-shadow runtime application is
available only through explicit `MmdSceneEnvironmentBinding` state recording and
`MmdSelfShadowTarget` participation. `VmdSelfShadowSampler` samples VMD `mode` /
`distance`; when a scene binding has `SelfShadowEnabled` but no VMD self-shadow
keys have been sampled, it uses the binding default self-shadow state instead of
remaining `NotApplied`. `MmdSelfShadowTarget` maps active modes into a
character-bounds projection for the dedicated `MmdSelfShadowRendererFeature`
shadow texture. This path does not mutate Unity lights, global scene lighting,
`RenderSettings`, `QualitySettings.shadowDistance`, URP assets, or Materials.
Implementation notes and debugging pitfalls for this path are tracked in
`docs/MMD_SELF_SHADOW.md`.
Import-time cached summary counts (camera/light/self-shadow keyframe counts) are now
surfaced via MmdVmdParseSummary / MmdVmdAsset / inspector for diagnostics
and readiness preview. The VMD asset Inspector Timeline readiness helper
(`MmdVmdTimelineReadiness` + `GetVmdTimelineReadiness(MmdVmdAsset)`) derives
compact duration source (MaxFrame), camera/light scene-motion presence, and
self-shadow opt-in scene-motion diagnostics exclusively from the cached asset
summary properties; the helper contract guarantees the display/readiness path
performs no `LoadMotion` or full parse. Self-shadow-only VMD is not classified
as ordinary camera/light scene motion, but it is surfaced as opt-in scene state
for `MmdSceneEnvironmentBinding` and `MmdSelfShadowTarget`.

Structural validation is owned by `MmdModelValidator.ValidateStructuralModel`
and `MmdMotionValidator.ValidateStructuralMotion`, with focused coverage in
`PmxIrContractTests` and `VmdIrContractTests`. The validation boundary rejects
missing top-level collections, malformed vector/quaternion/interpolation
lengths, non-finite numeric values, invalid index references, invalid triangle
ranges, unsupported material morph operations, duplicate non-empty bone names,
invalid IK settings, invalid morph payload references, and invalid VMD target
names or frame indices. Duplicate PMX morph names remain allowed for local asset
compatibility.

## Native Binary Layout

The Windows package plugin folder is:

```text
packages/com.yohawing.mmd-loader/Runtime/Plugins/x86_64
```

Only these native DLLs are expected there:

- `mmd_runtime_ffi.dll`

`mmd_runtime_ffi.dll` is built from the `native/mmd-anim` submodule. The Rust
crate emits `mmd_runtime_ffi.dll`, and the build script copies it to the
package plugin folder. Rebuild it with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-mmd-runtime-ffi.ps1
```

`mmd-anim` is the authority for parser, animation, and native physics behavior.
Host-driven physics applies a host pose, evaluates the before-physics phase,
advances the native physics world, applies rigidbody readback, and evaluates
the after-physics phase through this same FFI. Clip-driven physics evaluates a
native animation clip through the same FFI and can bake sequential physics
frames without a second managed plugin. Both flows use
`mmd_runtime_ffi.dll`; the package has no standalone physics wrapper binary.

`mmd_runtime_ffi` is both the parser summary provider and the fast-runtime
playback library. The accepted ABI version is `3`. Managed playback must keep a
fallback path when the fast runtime is unavailable, ABI-incompatible, or rejected
by managed validation. Explicit `TryEnableFastRuntime` / `DisableFastRuntime`
helpers remain diagnostic and manual-comparison hooks.

The only extra fast-runtime evaluation entry point currently accepted is
`mmd_runtime_instance_evaluate_clip_frame_without_ik`, and it is diagnostic
only. Unity integration must not change the fast-runtime IK solver semantics to
force parity with the managed Unity solver.

Fast-runtime copy APIs use element counts, not byte counts. `out_f32_len` is the
count of writable `float` values and `out_u8_len` is the count of writable
`byte` values. Managed DllImport bindings use the C calling convention and must
reject ABI versions other than `3` until this contract is revised.

Physics is an explicit runtime boundary owned by mmd-anim for Live playback.
`mmd_runtime_evaluate_host_frame` applies the host pose, steps the native physics
world, and performs the native after-physics evaluation atomically. Unity reads
back rigidbody states for diagnostics and copies the native current world
matrices for the `deformAfterPhysics` bones. The former managed append / IK /
pose re-evaluation path is deleted; Unity must not reconstruct the post-physics
pose from its own transforms.

Physics modes are owned by `MmdPhysicsPolicy`: `off` is the random-access
default for Timeline scrubbing and bake, `live` is explicit sequential
forward-playback only with ascending unique frames and finite positive frame
rate, and `cache` remains reserved/unsupported until cache readback exists.
Invalid modes, blank backend names, non-finite delta times, negative frames,
duplicate frames, and unsorted live frame sequences are validation failures.

The mmd-anim physics FFI is opt-in for live playback. It initializes from
neutral PMX rigidbody/joint descriptors, preserves collision filters and joint
limits/springs, synchronizes host-driven bodies before each step, and reads back
both rigidbody transforms and the final native pose for Unity visualization.
Random-access Timeline evaluation remains physics-off unless a validated physics
cache path is explicitly provided.

Current test coverage:

- `NativeInteropContractTests` pins parser summary entry point names,
  fast-runtime ABI version, the runtime-only Windows plugin layout, and
  absence of legacy native DLLs.
- `PmxIrContractTests` and `VmdIrContractTests` pin parser output as neutral IR.
- `PmxUnityInstantiationContractTests` pins PMX fixture parsing followed by
  static and skinned Unity instantiation.
- `scripts/check-cli.ps1 -Tier fast` and package layout validation pin the
  portable file layout and dotnet smoke contract.

## Import Boundaries

PMX Unity object construction is owned by the Import layer under
`packages/com.yohawing.mmd-loader/Runtime/Import`. The public managed namespace
for these types remains `Mmd.UnityIntegration` to avoid a source
breaking API change while the folder layout is normalized.

`MmdUnityModelFactory` is a partial facade over narrow helpers:

- `Coordinates` owns MMD-to-Unity basis conversion and import scale
  normalization.
- `Validation` owns rendering descriptor validation before Unity objects are
  created.
- `Mesh`, `Bones`, `Skinning`, and `Physics` own the corresponding Unity object
  construction details.
- `ImportedHierarchy` owns rebinding importer-owned hierarchies into scene
  instances.
- `MaterialRemaps` owns runtime material slot replacement without rebuilding
  the model.

Texture resolution and runtime material creation are import/runtime Unity
boundaries, not parser or pure rendering descriptor responsibilities.
`MmdRuntimeTextureResolver` resolves PMX-relative references against the source
PMX directory when a source context exists. Its default boundary rejects rooted,
UNC, device, URI, traversal, and reparse-point references before texture content
is read. Operational absolute paths remain internal; structured diagnostics use
PMX-root-relative paths and redact rejected rooted references. Failures do not
abort model creation, and successful decode returns runtime-owned `Texture2D`
instances for transient display.

Callers that intentionally consume textures outside the PMX directory may use
the three-argument resolver overload with an explicit list of fully-qualified
local directories. The existing two-argument API remains fail-closed. External
roots are not search paths for relative PMX references, and UNC, device, URI,
prefix-sibling, or reparse-point paths are not authorized. Public diagnostics
identify successful external references only as `<external-root:n>/...`.

Playback bindings classify model instances at construction time. Instances
created by a binding factory are owned by that binding; `Dispose`, controller
replacement, and controller destruction release their runtime Root, Mesh,
Materials, and decoded Textures. Caller-supplied and existing-Scene instances
are borrowed and survive binding disposal. Restoration of temporary mutations
to borrowed renderers is a separate transaction boundary.
Normal ScriptedImporter placement uses importer-generated Material sub-assets
and may bind existing project Texture assets, but it must not embed decoded
Texture2D objects as `.pmx` sub-assets during normal import.

Runtime texture binding accepts PMX diffuse, sphere, and toon references as
diagnostic/source strings. Blank diffuse references mean no texture. Relative
references resolve under the PMX source directory; traversal escaping that
directory is rejected. Missing files, unsupported extensions, and decode
failures produce diagnostics instead of failing model creation. Diffuse textures
bind to `_BaseMap` and `_MainTex` when those properties exist. Runtime-created
textures are transient `MmdUnityModelInstance` owned objects and are never
persisted as project assets.

Normal PMX import resolves PMX-relative texture references against the imported
`.pmx` asset location under `Assets/...` and binds already-imported project
`Texture2D` assets to generated Material sub-assets when found. It does not add
Texture objects as `.pmx` sub-assets, copy source texture files, or replace the
source texture reference strings used as provenance.

Sphere/toon texture handling is diagnostic/provisional. Sphere and toon
references, sphere mode, and toon source hints must stay visible in descriptors
and artifacts, but full PMX/rayMMD sphere/toon shading is not a release-path
runtime feature. Contract-artifact sync requires
`runtimeSphereShadingAppliedNow: false` and
`runtimeToonTextureSamplingAppliedNow: false`.

Texture orientation is fixed at the boundary: source descriptors preserve raw
MMD UVs, Unity viewport creation writes `1 - v`, and decoded texture pixels are
not flipped. Main texture UV morphs evaluate in source MMD UV space, then write
converted Unity viewport UVs through the mesh `SetUVs` path. Extra UV channels
remain payload-only.

Rendering descriptors remain Unity-independent handoff data. They preserve
MMD-space vertex positions, normals, UVs, parser-order indices, PMX material
texture reference strings, material colors, edge data, culling policy, skinning
weights, and submesh ranges. Unity coordinate conversion, `Mesh` construction,
runtime texture decoding, and Material property writes occur after this
descriptor boundary.

Rendering descriptor builders must validate PMX IR before generation, copy data
instead of aliasing parser arrays, emit deterministic ordering, and allocate no
UnityEngine objects. Mesh descriptors are sorted by PMX `vertexIndex`, preserve
MMD-space position/normal/UV, and keep index buffers in parser order. Skinning
descriptors are sorted by `vertexIndex`, keep paired bone indices/weights,
preserve normalized skinning mode and support status, and duplicate those
fields on split-vertex copies. BDEF is the current exact Unity bone-weight
handoff path; SDEF and QDEF remain linear fallback with preserved evidence
until exact CPU/GPU deformation paths exist.

Submesh descriptors are derived from material ranges in ascending PMX material
order. Their `submeshIndex`, `materialIndex`, `indexStart`, and `indexCount`
must match the corresponding material range and remain triangle-aligned inside
the rendering index buffer.

URP material binding descriptors are also Unity-independent handoff data. They
preserve PMX material index/name, default shader request, diffuse/sphere/toon
references, sphere/toon diagnostic hints, alpha, edge color/size, draw-edge
flag, culling policy, transparency mode, render-order bucket, and material
range. Runtime Unity material creation may resolve shaders and properties
later, but descriptor binding validation remains independent from actual
`Material` allocation.

Transparency classification boundary: `transparencyMode` (`opaque`,
`alphaTest`, `alphaBlend`) and `render-order bucket` are part of the
descriptor contract and URP binding. The reference classifier is summarized in
`docs/TODO.md` (半透明 Material / Texture TODO): PMX diffuse alpha baseline,
texture alpha metadata/scan with TGA overlay caution, geometry-aware UV
rasterization to avoid atlas padding false positives, material morph alpha as a
separate reason, and name heuristics for soft overlays. Unity preserves PMX
material/submesh definition order for draw ordering; compute order uses the
source index, buckets are diagnostic, and transparent materials are not
arbitrarily reordered. Name heuristics and unconditional TGA promotion are
reference behavior only, not default release policy without artifact-backed
validation. See the Phase13 material queue policy (`RenderQueue.Transparent` +
`materialRenderOrder`).

Material descriptors preserve these PMX material fields:

| Field | Meaning |
| --- | --- |
| `diffuseColor` | PMX diffuse color as a 3-component RGB array. |
| `ambientColor` | PMX ambient color as a 3-component RGB array. |
| `edgeColor` | PMX outline color as a 4-component RGBA array. |
| `drawEdgeFlag` | PMX draw-edge material flag. |

`diffuseColor` is copied from validated PMX material IR, clamped to finite 0..1
RGB components, and kept as descriptor data even when no renderer is created.
`ambientColor` is copied from validated PMX material IR, clamped to finite 0..1
RGB components, and kept as descriptor data. `edgeColor` and `edgeSize` are
preserved as outline handoff data. The PMX draw-edge material flag is exposed as `drawEdgeFlag`
on the descriptor. Outline eligibility requires both `drawEdgeFlag == true` and
`edgeSize > 0`; otherwise the outline handoff is
disabled for that material.

Morph descriptors expose vertex morph offsets for the vertex evaluator and keep
non-vertex morph payloads visible through inventory/runtime artifacts. Vertex
morph descriptors include PMX morph index/name and offsets with source
`vertexIndex` plus MMD-space `positionDelta`; they do not apply morph weights.
Non-vertex morph inventory classifies vertex, composite/group/flip, bone, UV,
material, physics, and unknown morph families. Group and flip weights are
expanded at the weight-map layer before downstream evaluators; texture UV, bone,
and material morphs have their own evaluators.

Main texture UV morph runtime evaluation is implemented. Extra-UV channels (uva1-uva4) remain payload-only. Contract-artifact sync pins:

| Field | Status |
| --- | --- |
| `runtimeTextureUvMorphEvaluationImplementedNow` | Always `true`. |
| `extraUvMorphRuntimeEvaluationImplementedNow` | Always `false`. |

UV morph payload handoff remains broader than the current mesh pipeline:
`runtimeUvMorphEvaluationImplementedNow` and
`meshPipelineConsumesUvMorphOffsetsNow` must both be `false`.

Import scale is applied at Unity construction boundaries. Mesh positions, bone
bind positions, vertex morph BlendShape deltas, and physics debug body/collider
transforms are scaled into Unity units. The neutral PMX IR and rendering
descriptors remain unscaled MMD-space data.

Imported character PMX assets use `importScale` as their single Unity-facing
scale source. Existing raw/direct model-definition paths remain `1.0`
compatible unless an explicit scale is passed. The preferred implementation is
baked scale, not root-transform scale: generated roots and model roots stay at
`Vector3.one`, while mesh vertices, bone bind positions, evaluated bone
translation offsets, vertex morph position deltas, and Unity debug physics
objects are scaled at the Unity boundary. Normals, rotations, UVs, UV morph
deltas, material colors, angular limits, and angular springs are not scaled.
Scale must not change parser IR, trace JSON, golden fixtures, comparer
tolerances, or rendering descriptor source values.

Physics scale-aware backend handoff remains future work. Until a scale-aware
backend descriptor exists, mmd-anim live diagnostics are evidence for the current
legacy `importScale = 1.0` / MMD-space path only. Scale-aware physics must report
the gravity, rigidbody size, joint linear-limit, and readback comparison space
it used before it can claim `0.1` character-scale physics parity.

Importer-owned hierarchy readiness is evidence computed from actual in-memory
Unity objects. Static zero-bone imports can be hierarchy/renderer ready without
a `SkinnedMeshRenderer`; their bone binding readiness is `NotEvaluated`.
Skinned imports require a `SkinnedMeshRenderer`, non-null bones, matching bone
count, and matching bindpose count. Diagnostics describe the observed missing or
mismatched object state rather than relying on hardcoded success.

Current test coverage:

- `ImportScalePhysicsUnitContractTests` pins Unity-scale construction while
  preserving unscaled descriptors.
- `RuntimeTextureBindingContractTests` pins PMX-relative runtime texture
  resolution, material slot binding, and traversal rejection diagnostics.
- `PmxImportHierarchyReadinessContractTests` pins importer hierarchy readiness
  categories for static and missing-root cases.
- `RenderingDescriptorContractTests` pins mesh, submesh, skinning, material, and
  URP material binding handoff data before Unity object creation.
- `PmxUnityInstantiationContractTests` pins package PMX fixtures through static
  and skinned Unity instantiation.

## Animation Boundaries

Runtime animation evaluation is owned by
`packages/com.yohawing.mmd-loader/Runtime/Animation`. The public managed
namespaces are intentionally preserved while the folder layout is normalized:

- `Mmd.Motion` remains the neutral motion and playback-data namespace. IK and
  append-transform evaluation for production playback belongs to mmd-anim.
- `Mmd` continues to expose the loader-facing evaluated-frame,
  playback snapshot, Humanoid, and bake-plan APIs.

`Animation/Motion` owns neutral VMD bone/morph data and model-keyframe metadata.
The native mmd-anim runtime owns append/Grant transforms, IK, world-matrix
propagation, and physics ordering.
`Animation/Playback` owns pure playback handoff DTOs, snapshot diagnostics,
snapshot JSON dumping, and the floor-unclamped playback time policy.
`Animation/Humanoid` owns imported Humanoid mapping contracts, proxy rig creation,
and rotation-only retargeting. `MmdHumanoidSetupAsset` is a compatibility-only
serialized container and is not an active authoring input.

`MmdEvaluatedFrame` lives directly under `Runtime/Animation` as the managed
projection of native world-matrix output. `MmdAnimationBakePlan` also lives
directly under `Runtime/Animation` because it builds a Unity-independent
Transform bake summary from `MmdRuntimeSession`; it is not Humanoid-specific.

Production frame evaluation enters mmd-anim. Random-access playback evaluates
the native clip directly. Live playback captures the Unity host pose, calls the
atomic native host-frame API, applies native rigidbody readback, then projects
native world matrices back to Unity only for PMX `deformAfterPhysics` bones
before refreshing the evaluated frame. There is no second managed append or IK
pass after physics.

Native frame projection and playback contract tests pin frame ordering and
world-matrix layout. Native checkpoint names, when emitted by mmd-anim
diagnostics, remain a native ABI contract rather than a managed solver class
boundary.

Vertex morph evaluation is a pure rendering-side boundary.
`MmdVertexMorphEvaluator.ApplyVertexMorphs` consumes validated rendering
vertices, vertex morph descriptors, and resolved morph weights. It applies only
non-zero finite weights, preserves output ordering by `vertexIndex`, applies
weighted MMD-space position deltas without mutating inputs, and leaves normals,
UVs, skinning, materials, and Unity objects untouched. Duplicate vertex indices,
non-finite weights/vectors, blank morph names, and missing offset payloads are
input errors pinned by `MorphEvaluationContractTests`.

Unity playback bakes PMX vertex morph descriptors into BlendShape frames for
skinned runtime models and applies evaluated weights through
`SkinnedMeshRenderer.SetBlendShapeWeight()` when bindings are available. The
CPU mesh-buffer path remains a static/no-BlendShape fallback and oracle path.
BlendShape playback reports `boundsUpdatePolicy =
skinned-local-bounds-no-recalculate`; CPU fallback playback reports
`mesh-recalculate-bounds`. Main texture UV morphs use the texture-UV evaluator
and mesh `SetUVs` path; extra UV channels remain payload-only. Unity
BlendShapes are not a UV morph solution.

Phase 1 trace schema is a controlled compatibility surface. Top-level traces use
`schemaVersion: 1`, non-blank `model`, `motion`, `space: mmd`, and non-empty
`frames`. Frame records carry non-negative integer `frame`, optional
non-negative finite `time`, fixed `checkpoint`, non-empty `bones`, `morphs`,
and optional `ik` records. Bone records require non-blank names,
`localPosition[3]`, quaternion `localRotation[4]` as `[x, y, z, w]`,
`localScale[3]`, and row-major `worldMatrix[16]`. Morph records require
non-blank name and finite weight. IK records require non-blank name, enabled
state, target, effector, and non-blank chain entries. Default comparer
tolerance is `0.00001`; missing required fields, malformed JSON, duplicate
`(frame, checkpoint)` pairs, duplicate names inside checkpoint records, NaN,
infinity, identity mismatches, array length mismatches, and Phase 1 `space`
values other than `mmd` are failures. Extra fields are allowed and ignored by
the Phase 1 comparer.

IK and append-transform semantics are native mmd-anim contract surfaces. Unity
does not expose or maintain a managed replacement solver; native fixture and
ABI tests are the authority for this ordering.

`MmdRuntimeSession` lives under `Runtime/Components/Playback`. It is still the
public loader-facing entry point for one validated PMX/VMD Neutral IR pair; only
the physical folder changed. It must not own UnityEngine objects.

Current test coverage:

- `MotionEvaluationOrderContractTests` pins checkpoint names, Phase 1 checkpoint
  order, absence of `afterPhysics` in Phase 1 traces, and multi-frame checkpoint
  grouping.
- `MorphEvaluationContractTests` pins the pure vertex morph evaluator boundary:
  non-zero finite weights, MMD-space weighted deltas, non-accumulating inputs,
  vertex-index ordering, and invalid payload diagnostics.
- `MmdHumanoidClipConversionPlannerTests` and
  `MmdHumanoidClipConversionWriterTests` now live under `Tests/EditMode/Contracts`
  and pin VMD -> Humanoid Clip conversion readiness, in-memory clip creation,
  explicit `.anim` writes, output path validation, and clip-local frame timing.
- Existing IK and pose coverage remains in dotnet smoke and EditMode tests while
  `ik.md` / `ik-evaluation-parity.md` stay as active contracts for future solver
  parity work.

## Components Boundaries

Runtime Unity-facing assets, MonoBehaviours, scene binding, and Timeline
surfaces are owned by `packages/com.yohawing.mmd-loader/Runtime/Components`.
Public managed namespaces are intentionally preserved while the physical folder
layout is normalized:

- `Components/Assets` owns imported PMX/VMD ScriptableObject containers and
  prefab provenance metadata.
- `Components/Playback` owns loader-facing playback session/configuration,
  playback controller/binding, and raw-path runtime importer.
- `Components/Scene` owns Unity scene instance representation, frame appliers,
  and physics debug body components.
- `Components/Timeline` owns the Timeline runtime asmdef and VMD Timeline clip,
  behaviour, and track types.
- `MmdSceneEnvironmentBinding` (tentative; `Components/Scene`) is the future
  scene/prefab authoring component for VMD camera/light binding. It holds
  explicit refs to pre-existing scene `Camera` and Directional `Light` targets,
  with an optional model/controller source ref for scoped motion only. It must
  not create PMX, Camera, or Light objects. Timeline playback/scrub updates only
  bound existing objects. Missing binding, wrong `LightType`, or unsupported
  tracks produce structured diagnostics / not-ready state, not auto-generation.
  VMD Timeline clip, behaviour, and importers do not own scene camera/light
  targets or this binding.
  VMD camera motion is evaluated first as an MMD camera state: target/look-at
  position, distance, Euler rotation, field of view, perspective flag, and
  interpolation. That state is converted to the bound Unity `Camera` only at
  this scene binding boundary, using the existing Unity-facing basis conversion
  without mutating MMD-space IR or trace data. Timeline random access must be
  stateless: evaluating the same time writes the same bound camera transform/FOV
  without drift or accumulation. `perspective=false` is diagnostic/not-ready
  until an orthographic conversion policy is explicitly evidenced.
  VMD light motion is evaluated first as MMD-space color and direction.
  Direction is converted only at the `MmdSceneEnvironmentBinding` boundary using
  the existing Unity-facing basis conversion; MMD-space IR/trace is not mutated.
  Bound `Light` (release path: `LightType.Directional` only; other types
  diagnostic/not-ready) receives VMD RGB mapped to color. Binding-owned/base
  Unity intensity is preserved; VMD color must not implicitly rewrite intensity.
  Timeline random access is stateless: evaluating the same time writes the same
  color/rotation without drift or accumulation. Missing light, wrong light type,
  or unsupported VMD light data produces structured diagnostics/not-ready.
  VMD clip/importer must not create Light or rewrite scene lighting globally.
  Camera/light release path is VMD camera plus Directional Light binding only.
  Classification of explicitly separate/deferred features:
  - SelfShadow: VMD `frame` / `mode` / `distance` is preserved in motion IR.
    PMX material/renderers have a ShadowCaster baseline for the dedicated MMD
    self-shadow map, while MMD toon ForwardLit ignores Unity/URP standard shadow
    receive. VMD self-shadow runtime application is
    explicit scene/render state: Timeline samples the self-shadow state into
    `MmdSceneEnvironmentBinding`, `MmdSelfShadowTarget` selects active character
    roots, and `MmdSelfShadowRendererFeature` renders a dedicated MMD shadow map
    fit to those bounds. This path does not mutate Light, global scene lighting,
    `QualitySettings.shadowDistance`, URP assets, or Materials.
  - Outline: PMX material/rendering feature (`drawEdgeFlag`, edge color/size), not
    VMD scene motion; do not implement in camera/light binding.
  - Gravity: physics/world/mmd-anim/PMM/cache policy, not VMD camera/light random
    access; keep out of Timeline random-access scene binding.
  VMD clip/importer surface no SelfShadow/outline/gravity controls as part of
  the camera/light MVP. `MmdSceneEnvironmentBinding` may surface only the
  explicit self-shadow scene-state policy described above.
  VMD import summary now includes camera / light / self-shadow keyframe counts
  (via MmdVmdParseSummary and MmdVmdAsset cached fields); these are
  diagnostic/source metadata only. Self-shadow frame payload is preserved in
  motion IR. The URP/material shadow-caster baseline and dedicated MMD
  self-shadow texture path are present. ForwardLit toon shadowing comes only
  from the dedicated MMD self-shadow map when `MmdSelfShadowTarget` opts in;
  otherwise MMD toon materials render with no shadow receive. Self-shadow visual
  tuning remains a separate fidelity backlog item.
  See import summary surfacing slice and Components
  Boundaries for current boundaries.

`MmdRuntimeInfo` remains directly under `Runtime` because it is package-level
metadata for compile/import smoke tests, not a component or asset surface.
The legacy `Runtime/UnityIntegration` and `Runtime/Timeline` physical folders
are retired; source compatibility is preserved by keeping the public namespaces
and asmdef identity stable.

`MmdUnityModelFactory` is the UnityEngine object binding boundary for runtime
display work. It consumes validated PMX neutral IR or rendering descriptors and
creates root objects, Mesh/Material/Texture instances, optional bone hierarchy,
static or skinned renderers, frame appliers, and playback bindings. It must not
add UnityEngine object ownership to `MmdRuntimeSession`; PMX/VMD neutral IR,
rendering descriptors, traces, and golden fixtures remain MMD-space and
unscaled until this Unity-facing boundary.

`MmdUnityPlaybackController` is scene-facing playback. Persistent asset-backed
PMX/VMD source ownership belongs to the controller so normal scene objects do
not need extra source-owner components; `MmdRuntimeImporterComponent` remains
the raw-path owner. The controller also owns normal playback state such as
physics mode and delegates pose application to `MmdUnityPlaybackBinding`.
`MmdUnityPlaybackControllerEditor` and editor workflow backends must not expose
direct legacy source fields, migration diagnostics, cleanup readiness panels, or
migration action buttons. New source creation must resolve playback settings
from existing source/settings owners before falling back to compatibility fields.
Controller playback settings field physical deletion is allowed only after the
Editor resolver, source creation, Timeline mixed-source path, and devtools config
asset builder all use source-owned playback config resolution. When deleting controller-local `frameRate`, `initialFrame`, or `playOnStart`, the
compatibility
resolver and diagnostics must be updated in the same slice.

## Runtime API And Snapshot Boundaries

The public runtime surface remains conservative. Loader and diagnostics callers
may use `NativeMmdParser` / `IMmdParser`, neutral IR DTOs and validators,
`MmdRuntimeSession`, PMX/VMD asset containers, `MmdRuntimeFrameEvaluator`,
`MmdPlaybackSnapshotBuilder`, snapshot diagnostics and dumper helpers. Managed
physics backend and managed IK/pose compatibility types are not part of the
current production surface; Live physics is the native mmd-anim host-frame
route. Unity object ownership belongs to Import and Components surfaces, not
neutral IR or pure frame evaluators. `MmdEditorVerificationFacade` is
Editor-only and keeps failures classified by stable stage strings such as input
validation, PMX/VMD read, PMX/VMD parse, IR validation, Unity instantiation,
and runtime frame apply.

`MmdRuntimeSession` owns one already-validated PMX/VMD neutral IR pair plus
stable model and motion ids. Parser connection belongs outside the session.
Session methods produce validation traces, evaluated frames, playback
snapshots, snapshot summaries, time-based snapshots using the floor-unclamped
time-to-frame policy, and transform-bake summaries. Random-access session paths
must remain physics-off unless a future Physics Cache readback artifact exists;
Live physics is owned by the Unity playback binding/controller forward-playback
path.

Physics Cache is not implemented. Current cache-mode artifacts are validator
backed policy or unsupported-status artifacts only; they are not readback
samples and must not be treated as evidence that Timeline random-access physics
is available. Cache artifacts and plans must avoid licensed local source paths,
must not fall back to Live or Null physics as a cache substitute, and must keep
Timeline random-access physics off until a real cache readback schema,
provenance, replay, and comparison path exists. This boundary is pinned by the
Phase11, Phase11.5, and Phase17 physics-cache validators and testdata under
`tools/`.

`MmdEvaluatedFrame` is the Unity-independent final-frame snapshot consumed by
rendering and binding code. It is separate from trace DTOs: traces carry
checkpoint evidence, while evaluated frames carry final frame state. Frame
entries keep non-negative frame/time, bones sorted by bone index, morphs sorted
by morph name, and material descriptors sorted by material index. Coordinate
conversion belongs to Import/Rendering boundaries, not the MMD-space evaluated
frame.

`MmdPlaybackSnapshot` combines stable model/motion ids, an evaluated frame, and
a rendering descriptor. Snapshot artifacts preserve MMD-space values, stable
ordering, unique frame/rendering ids, non-blank names, material/index-range
integrity, URP binding diagnostics, and explicit missing-field diagnostics.
`tools/validate-playback-snapshot.py`, `tools/validate-playback-summary.py`,
and `tools/validate-playback-sequence-summary.py` pin the JSON shapes and
negative fixtures used by CLI gates.

Fast-runtime playback is an opt-in/default-when-available Unity binding path,
not a replacement for the pure managed session contract. When
`MmdUnityPlaybackBinding` uses `mmd_runtime_ffi`, animation-only `ApplyFrame`
may return a lightweight snapshot with empty `frame.bones` because world
matrices are applied directly to Unity transforms. The snapshot may be cached
and reused across calls; callers must not mutate it or hold it as a stable
point-in-time bone snapshot across subsequent fast-runtime calls. Fast-runtime
diagnostics may use the without-IK MMD-space FFI entry point for benchmark
parity, but normal playback must not use that diagnostic path to change IK
solver behavior. The animation-only native ABI returns the complete pose through
the post-physics append/IK phase. Live Physics cannot use that complete pose as
its pre-step seed when a PMX contains `deformAfterPhysics` bones: it evaluates
the managed pipeline only through the before-physics boundary, applies physics
readback, then runs the after-physics append/IK pass from the resulting Unity
transforms. Models without after-physics bones may retain the reusable native
pre-step path because their complete animation pose is phase-equivalent.

Memory and scratch ownership is layered. Model immutable data, motion immutable
data, session mutable state, per-frame scratch, and rendering handoff state have
separate lifetimes. Scratch reuse must not change trace checkpoint names,
evaluation order, solver rollback, append reapply behavior, emitted snapshot
object semantics, or caller-visible arrays after trace/snapshot emission.
Benchmark artifacts keep whole trace elapsed time separate from
`phaseTimings`; timing DTOs remain benchmark-only implementation details.

## Validation Artifact Boundaries

Portable validation artifacts are source-of-truth only when they have an
explicit validator and committed positive/negative fixtures. Local MMD assets
are referenced through `data-local/fixtures.local.json`, which stays gitignored;
licensed PMX/VMD/PMM/source assets must not be committed. Local fixture paths
must resolve under their declared root, fixture ids must be stable and unique,
and local manifests are diagnostics or corpus plans, not golden truth.

Committed parser and runtime fixtures live under `tools/fixtures/` and
`packages/com.yohawing.mmd-loader/Tests/Fixtures/`. Their manifests are
redistribution-safe verification maps with non-blank ids, reproducible commands,
expected trace/model paths, and structural coverage fields. The local runtime
plan and local playback smoke wrappers produce artifacts under `artifacts/` and
validate frame lists, frame rates, manifest provenance, and resolved paths before
Unity-backed checks run.

GoldenOracle and MMD4Mecanim comparison data remain local opt-in diagnostics
unless redistribution provenance is explicitly cleared. The MMD4Mecanim Humanoid
comparison manifest collects case metadata only: absolute local roots, relative
model/motion/reference paths under those roots, unique non-blank case names,
finite positive frame rates, sorted unique non-negative frames, supported
reference artifact kinds, allowed failure categories, and non-blank provenance.
Actual MMD4Mecanim reference generation and numeric comparison remain deferred.

GoldenOracle motion-numeric data is a MMDDumper/MikuMikuDance numeric oracle
for local comparison, not redistributable source data. Its manifest uses
`schemaVersion: 1`, `kind: motion-numeric`, `producer.tool: MMDDumper`,
`producer.runtime: MikuMikuDance 9.32 x64`, and non-empty cases with stable
names, PMX/VMD paths, and sorted unique non-negative frame lists. Each run case
contains `fixture.json` and `oracle.actual.jsonl`; selected records require
finite frame numbers, one or more models, non-empty bone/morph arrays, finite
16-number bone world matrices, and finite morph weights. Converted oracle JSON
uses `oracleKind: mmd-exported-runtime-dump`, a stable model inventory, and
per-frame stage payloads. GoldenOracle output and runtime traces are compared
in MMD-space before Unity coordinate conversion. Local GoldenOracle checks are
opt-in/report-style unless a redistribution-safe case is explicitly added.

Visual baseline artifacts are rendering-fidelity evidence, separate from
numeric render-target smokes. Unity visual capture writes PNG plus JSON under
`artifacts/`, with `schemaVersion: 1`, PNG path, shader names, dimensions,
nonblank pixels, outline pixels, scene background mode, camera post-processing
mode, and diff metric. The default Phase17 capture scene is isolated with
transparent background and post-processing disabled unless explicit command
options declare otherwise. NVIDIA FLIP LDR is the comparison metric; missing
FLIP is a skip only when explicitly allowed, and missing reference images are
not passes. Committed baselines must be redistribution-safe and intentionally
reviewed; licensed local captures stay under `artifacts/` or `data-local/`.
three-mmd-loader shaderball and generated-PMX references are guideline
diagnostics, not Unity golden truth.

## Product Import Experience

The product axis is PMX/VMD as ordinary Unity assets, close to FBX and
AnimationClip authoring expectations. The golden path is PMX drag or import,
scene placement, VMD as a Timeline clip, and optional Humanoid AnimationClip
bake.

PMX import targets FBX parity. The imported model hierarchy is the user-visible
model object; `MmdPmxAsset` remains the source/metadata compatibility surface
for PMX bytes, import settings summary, parse summary, diagnostics, hierarchy
readiness, source ownership, and existing API access. Importer-owned hierarchy,
Mesh, and Material sub-assets are the normal scene placement source. Existing
compatibility APIs may still resolve the `MmdPmxAsset` sub-asset.

PMX Rig/Humanoid authoring belongs to importer-facing workflow, while the
internal implementation may continue to use hidden proxy rig and retargeting
helpers. Selecting `Humanoid` and applying the importer persists the generated
Avatar and retarget bindings with the imported PMX. No separate setup asset or
migration button participates in the current workflow.

The import visual acceptance bar is production-facing rather than trace-parity
fidelity: after import and scene placement, textures should bind when available,
alpha/transparent material order should follow the material queue policy, toon
shading should have a visible approximation, and PMX draw-edge materials should
produce an outline. Sphere-map exactness, SDEF/QDEF exact deformation,
SelfShadow, detailed outline color parity, and MMD-reference screenshot parity
remain separate fidelity backlog items.

VMD remains an independent imported asset. PMX import must not automatically
generate VMD, Timeline, or Humanoid clip assets. The primary VMD authoring path
is scene PMX/controller source plus a `MmdVmdTimelineTrack` /
`MmdVmdTimelineClip` referencing an imported `MmdVmdAsset`. Timeline evaluation
uses random-access runtime evaluation with physics off by default; Live physics
belongs to forward playback outside Timeline unless a future cache readback
artifact exists. Raw path playback is a non-primary diagnostics/runtime path,
not the default authoring experience.

`MmdVmdTimelineClip` does not advertise Timeline blend capabilities
(`ClipCaps.None`). Overlapping body-motion VMD clips on the same
`MmdVmdTimelineTrack` use **deterministic single-winner arbitration** only —
not weighted pose blending:

- `MmdVmdTimelineTrack` creates an `MmdVmdTimelineMixerBehaviour` mixer.
- Among active clip inputs, the winner is the greatest positive effective input
  weight; equal weights resolve to the later input index.
- The mixer applies that winner's full pose once per graph evaluation (no bone /
  morph scaling by weight, no multi-clip last-writer, no duplicate Live-physics
  steps). Zero-weight inputs are ignored; an all-zero input frame applies no pose.
- Track-managed clip behaviours do not apply pose from their own `ProcessFrame`;
  direct non-track `MmdVmdTimelineBehaviour.ProcessFrame` remains for tests /
  compatibility.
- True weighted blend is deferred until a concrete user and fixture justify it.
  The primary composition path is Humanoid/AnimationClip bake followed by Unity
  Timeline/Animator blend, layers, and AvatarMask. Keeping raw VMD Timeline at
  deterministic single-winner/hard-cut semantics avoids double native evaluation
  and undefined composition across IK/append, group/flip/bone/UV/material/impulse
  morphs, scene motion, and especially two stateful Live-physics simulations.
  If a concrete fixture reopens this work, start with body bones plus ordinary
  morph weights, physics off, and camera/light/self-shadow/impulse excluded or
  winner-takes-all; do not infer general blend support from that limited policy.

AnimationClip bake is explicit and optional after PMX/VMD source prerequisites
are satisfied. It is not run during PMX or VMD import or Apply. The single
release-facing product bake surface is `MmdGenericAnimationClipBakeWindow` (the
legacy type name is retained for editor API compatibility), opened from
the VMD Inspector through `OpenFromVmd`. The window owns no persistent PMX, VMD,
or bake-setting state
and delegates to the existing writer boundaries. Its PMX
field displays and accepts the imported `.pmx` main `GameObject`, resolving the
hidden `MmdPmxAsset` metadata internally; VMD authoring never requires users to
find or persist that sub-asset.

`Generic` uses `MmdGenericAnimationClipWriter` and writes the final native PMX
hierarchy transform/BlendShape curves. `Humanoid` requires the selected PMX to
have been imported as Humanoid with a valid Avatar and persisted retarget
bindings; the window displays the cache-only readiness result from
`MmdHumanoidClipConversionPlanner` and calls
`MmdHumanoidClipConversionWriter` only after the user presses Create. Both paths
share PMX/VMD, frame range, frame rate, and `Assets/*.anim` output fields. VMD
max-frame defaults and output naming are refreshed from cached imported metadata;
the window does not parse VMD merely for selection or readiness display.

## Humanoid Clip Conversion Boundary

VMD to Humanoid AnimationClip conversion is an explicit optional step after
PMX/VMD authoring prerequisites are satisfied. `MmdHumanoidClipConversionPlanner`
checks null inputs, the PMX Humanoid import mode, imported Avatar and mapping
readiness, persisted retarget bindings, imported hierarchy and renderer
availability, and cached VMD structural validation. Planning/analyze calls must
not parse VMD or create or write AnimationClip assets.

VMD Inspector readiness preview (cache-only helper path) may call analyze for
preview but must not create AnimationClip assets. Its explicit
`Bake to AnimationClip...` launcher is the product entry point and opens the
shared bake window; `.anim` creation still occurs only after the user presses
Create and delegates to the existing Generic or Humanoid writer (never VMD import
or live Timeline). The shared window is opened only from the VMD Inspector via
`OpenFromVmd`; PMX Importer UI does not expose a duplicate bake launcher. The existing
Humanoid setup-asset editor and creation workflow have been removed.

`MmdHumanoidSetupAsset` and `MmdHumanoidSetupPreset` remain obsolete runtime
types only so projects created before 0.2.0 retain the old MonoScript binding,
serialized metadata, and source references. They are not read by the planner or
writer, have no creation UI, and must not be presented as a migration target.

`MmdHumanoidClipConversionWriter` can create an in-memory clip when
prerequisites are ready, and can write an `.anim` asset only through the
explicit `CreateHumanoidAnimationClipAsset` API. Output paths must be
project-relative `Assets/*.anim` paths; rooted paths, traversal, empty segments,
and non-`.anim` paths are rejected. Clip timing is local to the requested frame
range and frame rate. These boundaries are pinned by
`MmdHumanoidClipConversionPlannerTests` and
`MmdHumanoidClipConversionWriterTests`.
