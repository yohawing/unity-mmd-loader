#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Mmd.UnityIntegration;

namespace Mmd.Rendering.Universal
{
    public sealed class MmdSelfShadowRenderPass : ScriptableRenderPass
    {
        public static readonly int MmdSelfShadowMapId = Shader.PropertyToID("_MmdSelfShadowMap");
        public static readonly int MmdSelfShadowWorldToShadowId = Shader.PropertyToID("_MmdSelfShadowWorldToShadow");
        public static readonly int MmdSelfShadowParamsId = Shader.PropertyToID("_MmdSelfShadowParams");
        private static readonly int LightDirectionId = Shader.PropertyToID("_LightDirection");
        private static readonly int LightPositionId = Shader.PropertyToID("_LightPosition");
        private static readonly int ShadowBiasId = Shader.PropertyToID("_ShadowBias");
        private const string CastingPunctualLightShadowKeyword = "_CASTING_PUNCTUAL_LIGHT_SHADOW";

        private static readonly Vector4 DisabledParams = Vector4.zero;
        private static readonly List<MmdSelfShadowTarget> TargetBuffer = new();
        private static readonly List<MmdSelfShadowTarget> ActiveProjectionTargets = new();

        private readonly List<ShadowDrawItem> drawItems = new();
        private Matrix4x4 viewMatrix = Matrix4x4.identity;
        private Matrix4x4 projectionMatrix = Matrix4x4.identity;
        private Matrix4x4 worldToShadow = Matrix4x4.identity;
        private Vector4 shadowParams = DisabledParams;
        private Vector4 lightDirection = new(0.35f, -1.0f, 0.35f, 0.0f);
        private int mapSize = 1024;

        private readonly struct ShadowDrawItem
        {
            public ShadowDrawItem(Renderer renderer, Material material, int submeshIndex, int shadowPassIndex)
            {
                Renderer = renderer;
                Material = material;
                SubmeshIndex = submeshIndex;
                ShadowPassIndex = shadowPassIndex;
            }

            public Renderer Renderer { get; }

            public Material Material { get; }

            public int SubmeshIndex { get; }

            public int ShadowPassIndex { get; }
        }

        private sealed class PassData
        {
            public readonly List<ShadowDrawItem> DrawItems = new();
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 ProjectionMatrix;
            public Matrix4x4 RestoreViewMatrix;
            public Matrix4x4 RestoreProjectionMatrix;
            public Matrix4x4 WorldToShadow;
            public Vector4 ShadowParams;
            public Vector4 LightDirection;
            public float ClearDepth;
        }

        public static void PublishDisabledGlobals()
        {
            Shader.SetGlobalMatrix(MmdSelfShadowWorldToShadowId, Matrix4x4.identity);
            Shader.SetGlobalVector(MmdSelfShadowParamsId, DisabledParams);
        }

        public bool Setup(int requestedMapSize, Vector3 requestedShadowDirection)
        {
            MmdSelfShadowTarget.CollectActiveTargets(TargetBuffer);
            ActiveProjectionTargets.Clear();
            drawItems.Clear();
            shadowParams = DisabledParams;

            if (TargetBuffer.Count == 0)
            {
                PublishDisabledGlobals();
                return false;
            }

            for (int i = 0; i < TargetBuffer.Count; i++)
            {
                TargetBuffer[i].RefreshReceiverGate();
                if (TargetBuffer[i].TryGetActiveProjectionState(out _))
                {
                    ActiveProjectionTargets.Add(TargetBuffer[i]);
                }
            }

            if (ActiveProjectionTargets.Count == 0)
            {
                PublishDisabledGlobals();
                return false;
            }

            if (!TryCreateMatrices(ActiveProjectionTargets, requestedShadowDirection, out viewMatrix, out projectionMatrix, out worldToShadow, out shadowParams, out lightDirection))
            {
                PublishDisabledGlobals();
                return false;
            }

            for (int i = 0; i < ActiveProjectionTargets.Count; i++)
            {
                AddDrawItems(ActiveProjectionTargets[i], drawItems);
            }

            if (drawItems.Count == 0)
            {
                PublishDisabledGlobals();
                return false;
            }

            mapSize = Mathf.Clamp(requestedMapSize, 128, 4096);
            return true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            TextureHandle shadowMap = renderGraph.CreateTexture(new TextureDesc(mapSize, mapSize)
            {
                name = "MMD Self Shadow Map",
                depthBufferBits = DepthBits.Depth32,
                isShadowMap = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.clear
            });

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "MMD Self Shadow Pass",
                out var passData);

            passData.DrawItems.Clear();
            passData.DrawItems.AddRange(drawItems);
            passData.ViewMatrix = viewMatrix;
            passData.ProjectionMatrix = projectionMatrix;
            passData.RestoreViewMatrix = cameraData.GetViewMatrix();
            passData.RestoreProjectionMatrix = cameraData.GetProjectionMatrix();
            passData.WorldToShadow = worldToShadow;
            passData.ShadowParams = shadowParams;
            passData.LightDirection = lightDirection;
            passData.ClearDepth = SystemInfo.usesReversedZBuffer ? 0.0f : 1.0f;

            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderAttachmentDepth(shadowMap, AccessFlags.ReadWrite);
            builder.SetGlobalTextureAfterPass(shadowMap, MmdSelfShadowMapId);
            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                RasterCommandBuffer cmd = context.cmd;
                cmd.ClearRenderTarget(clearDepth: true, clearColor: false, backgroundColor: Color.clear, depth: data.ClearDepth);
                cmd.SetGlobalMatrix(MmdSelfShadowWorldToShadowId, data.WorldToShadow);
                cmd.SetGlobalVector(MmdSelfShadowParamsId, data.ShadowParams);
                cmd.SetGlobalVector(LightDirectionId, data.LightDirection);
                cmd.SetGlobalVector(LightPositionId, Vector4.zero);
                cmd.SetGlobalVector(ShadowBiasId, Vector4.zero);
                cmd.DisableShaderKeyword(CastingPunctualLightShadowKeyword);
                cmd.SetViewProjectionMatrices(data.ViewMatrix, data.ProjectionMatrix);

                for (int i = 0; i < data.DrawItems.Count; i++)
                {
                    ShadowDrawItem item = data.DrawItems[i];
                    if (item.Renderer != null && item.Material != null)
                    {
                        cmd.DrawRenderer(item.Renderer, item.Material, item.SubmeshIndex, item.ShadowPassIndex);
                    }
                }

                cmd.SetViewProjectionMatrices(data.RestoreViewMatrix, data.RestoreProjectionMatrix);
            });
        }

        private static void AddDrawItems(MmdSelfShadowTarget target, List<ShadowDrawItem> output)
        {
            Renderer[] renderers = target.BoundsRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                int submeshCount = renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
                    ? skinned.sharedMesh.subMeshCount
                    : materials.Length;

                for (int submesh = 0; submesh < materials.Length && submesh < submeshCount; submesh++)
                {
                    Material material = materials[submesh];
                    if (material == null)
                    {
                        continue;
                    }

                    int passIndex = material.FindPass("ShadowCaster");
                    if (passIndex >= 0)
                    {
                        output.Add(new ShadowDrawItem(renderer, material, submesh, passIndex));
                    }
                }
            }
        }

        private static bool TryCreateMatrices(
            List<MmdSelfShadowTarget> targets,
            Vector3 shadowDirection,
            out Matrix4x4 view,
            out Matrix4x4 projection,
            out Matrix4x4 worldToShadowMatrix,
            out Vector4 parameters,
            out Vector4 lightDirection)
        {
            view = Matrix4x4.identity;
            projection = Matrix4x4.identity;
            worldToShadowMatrix = Matrix4x4.identity;
            parameters = DisabledParams;
            lightDirection = new Vector4(0.35f, -1.0f, 0.35f, 0.0f);

            if (shadowDirection.sqrMagnitude < 1e-8f)
            {
                shadowDirection = new Vector3(0.35f, -1.0f, 0.35f);
            }

            Vector3 forward = shadowDirection.normalized;
            lightDirection = new Vector4(forward.x, forward.y, forward.z, 0.0f);
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            Quaternion rotation = Quaternion.LookRotation(forward, up);
            view = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one).inverse;

            bool hasBounds = false;
            Bounds aggregate = default;
            float farDistance = 0.0f;
            for (int i = 0; i < targets.Count; i++)
            {
                MmdSelfShadowBoundsResult result = targets[i].CollectBounds();
                if (!result.HasBounds)
                {
                    continue;
                }

                if (!targets[i].TryGetActiveProjectionState(out Mmd.Motion.MmdSelfShadowProjectionState projectionState))
                {
                    continue;
                }

                farDistance = Mathf.Max(farDistance, projectionState.FarDistance);
                if (!hasBounds)
                {
                    aggregate = TransformBounds(result.Bounds, view);
                    hasBounds = true;
                }
                else
                {
                    aggregate.Encapsulate(TransformBounds(result.Bounds, view));
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            Vector3 center = aggregate.center;
            Vector3 extents = aggregate.extents;
            float halfWidth = Mathf.Max(extents.x, 0.01f);
            float halfHeight = Mathf.Max(extents.y, 0.01f);
            float depth = Mathf.Max(extents.z * 2.0f, farDistance, 0.1f);
            float near = center.z - depth * 0.5f - 0.05f;
            float far = Mathf.Max(near + 0.1f, center.z + depth * 0.5f);

            projection = Matrix4x4.Ortho(
                center.x - halfWidth,
                center.x + halfWidth,
                center.y - halfHeight,
                center.y + halfHeight,
                near,
                far);

            worldToShadowMatrix = GetShadowTransform(projection, view);
            parameters = new Vector4(1.0f, 0.0025f, 1.0f / Mathf.Max(1, targets.Count), 0.0f);
            return true;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Bounds result = new Bounds(center, Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                        result.Encapsulate(matrix.MultiplyPoint3x4(corner));
                    }
                }
            }

            return result;
        }

        private static Matrix4x4 GetShadowTransform(Matrix4x4 projection, Matrix4x4 view)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                projection.m20 = -projection.m20;
                projection.m21 = -projection.m21;
                projection.m22 = -projection.m22;
                projection.m23 = -projection.m23;
            }

            return ConvertToTextureSpace(projection * view);
        }

        private static Matrix4x4 ConvertToTextureSpace(Matrix4x4 matrix)
        {
            Matrix4x4 textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            return textureScaleAndBias * matrix;
        }
    }
}
