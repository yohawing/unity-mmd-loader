#nullable enable

using System;
using UnityEngine;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity
{
    /// <summary>
    /// Readiness status for importer-owned hierarchy sub-asset diagnostics.
    /// NotEvaluated is the default for uninitialized or pre-Slice-C assets.
    /// </summary>
    public enum MmdImportReadiness
    {
        NotEvaluated = 0,
        Ready = 1,
        Blocked = 2
    }

    public enum MmdPmxImportSummaryStatus
    {
        NotParsed = 0,
        Passed = 1
    }

    [Serializable]
    public sealed class MmdPmxMaterialSummary
    {
        public int index;
        public string name = string.Empty;
        public string diffuseTexture = string.Empty;
        public string sphereTexture = string.Empty;
        public string toonTexture = string.Empty;
        public bool transparent;
        public bool edgeEnabled;
    }

    [Serializable]
    public readonly struct MmdPmxParseSummary
    {
        public MmdPmxParseSummary(
            string modelName,
            int vertexCount,
            int indexCount,
            int boneCount,
            int morphCount,
            int materialCount,
            int diffuseTextureReferenceCount,
            int sphereTextureReferenceCount,
            int toonTextureReferenceCount,
            int transparentMaterialCount,
            int edgeMaterialCount,
            int ikCount,
            int rigidbodyCount,
            int jointCount,
            Vector3 boundsMin,
            Vector3 boundsMax,
            MmdPmxMaterialSummary[]? materialSummaries = null)
        {
            ModelName = modelName ?? string.Empty;
            VertexCount = Math.Max(vertexCount, 0);
            IndexCount = Math.Max(indexCount, 0);
            BoneCount = Math.Max(boneCount, 0);
            MorphCount = Math.Max(morphCount, 0);
            MaterialCount = Math.Max(materialCount, 0);
            DiffuseTextureReferenceCount = Math.Max(diffuseTextureReferenceCount, 0);
            SphereTextureReferenceCount = Math.Max(sphereTextureReferenceCount, 0);
            ToonTextureReferenceCount = Math.Max(toonTextureReferenceCount, 0);
            TransparentMaterialCount = Math.Max(transparentMaterialCount, 0);
            EdgeMaterialCount = Math.Max(edgeMaterialCount, 0);
            IkCount = Math.Max(ikCount, 0);
            RigidbodyCount = Math.Max(rigidbodyCount, 0);
            JointCount = Math.Max(jointCount, 0);
            BoundsMin = SanitizeVector(boundsMin);
            BoundsMax = SanitizeVector(boundsMax);
            BoundsSize = new Vector3(
                Math.Max(0.0f, BoundsMax.x - BoundsMin.x),
                Math.Max(0.0f, BoundsMax.y - BoundsMin.y),
                Math.Max(0.0f, BoundsMax.z - BoundsMin.z));
            MaterialSummaries = materialSummaries != null
                ? (MmdPmxMaterialSummary[])materialSummaries.Clone()
                : Array.Empty<MmdPmxMaterialSummary>();
        }

        public string ModelName { get; }

        public int VertexCount { get; }

        public int IndexCount { get; }

        public int BoneCount { get; }

        public int MorphCount { get; }

        public int MaterialCount { get; }

        public int DiffuseTextureReferenceCount { get; }

        public int SphereTextureReferenceCount { get; }

        public int ToonTextureReferenceCount { get; }

        public int TransparentMaterialCount { get; }

        public int EdgeMaterialCount { get; }

        public int IkCount { get; }

        public int RigidbodyCount { get; }

        public int JointCount { get; }

        public Vector3 BoundsMin { get; }

        public Vector3 BoundsMax { get; }

        public Vector3 BoundsSize { get; }

        public MmdPmxMaterialSummary[] MaterialSummaries { get; }

        public static MmdPmxParseSummary FromModel(MmdModelDefinition model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            int diffuseTextureReferenceCount = 0;
            int sphereTextureReferenceCount = 0;
            int toonTextureReferenceCount = 0;
            int transparentMaterialCount = 0;
            int edgeMaterialCount = 0;
            if (model.materials != null)
            {
                foreach (MmdMaterialDefinition material in model.materials)
                {
                    if (!string.IsNullOrWhiteSpace(material.texture))
                    {
                        diffuseTextureReferenceCount++;
                    }

                    if (!string.IsNullOrWhiteSpace(material.sphereTexture))
                    {
                        sphereTextureReferenceCount++;
                    }

                    if (!string.IsNullOrWhiteSpace(material.toonTexture))
                    {
                        toonTextureReferenceCount++;
                    }

                    if (material.alpha < 1.0f)
                    {
                        transparentMaterialCount++;
                    }

                    if (material.drawEdgeFlag && material.edgeSize > 0.0f)
                    {
                        edgeMaterialCount++;
                    }
                }
            }

            Bounds bounds = CalculateBounds(model);
            return new MmdPmxParseSummary(
                model.name,
                model.vertices?.Count ?? 0,
                model.indices?.Count ?? 0,
                model.bones?.Count ?? 0,
                model.morphs?.Count ?? 0,
                model.materials?.Count ?? 0,
                diffuseTextureReferenceCount,
                sphereTextureReferenceCount,
                toonTextureReferenceCount,
                transparentMaterialCount,
                edgeMaterialCount,
                model.ik?.Count ?? 0,
                model.physics?.rigidbodies?.Count ?? 0,
                model.physics?.joints?.Count ?? 0,
                bounds.min,
                bounds.max,
                BuildMaterialSummaries(model));
        }

        private static MmdPmxMaterialSummary[] BuildMaterialSummaries(MmdModelDefinition model)
        {
            if (model.materials == null || model.materials.Count == 0)
            {
                return Array.Empty<MmdPmxMaterialSummary>();
            }

            var summaries = new MmdPmxMaterialSummary[model.materials.Count];
            for (int i = 0; i < model.materials.Count; i++)
            {
                MmdMaterialDefinition material = model.materials[i];
                summaries[i] = new MmdPmxMaterialSummary
                {
                    index = material.index,
                    name = material.name ?? string.Empty,
                    diffuseTexture = material.texture ?? string.Empty,
                    sphereTexture = material.sphereTexture ?? string.Empty,
                    toonTexture = material.toonTexture ?? string.Empty,
                    transparent = material.alpha < 1.0f,
                    edgeEnabled = material.drawEdgeFlag && material.edgeSize > 0.0f
                };
            }

            return summaries;
        }

        private static Bounds CalculateBounds(MmdModelDefinition model)
        {
            bool hasVertex = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            if (model.vertices == null)
            {
                return bounds;
            }

            foreach (MmdVertexDefinition vertex in model.vertices)
            {
                if (vertex.position == null || vertex.position.Length < 3)
                {
                    continue;
                }

                var position = new Vector3(vertex.position[0], vertex.position[1], vertex.position[2]);
                if (!IsFinite(position))
                {
                    continue;
                }

                if (!hasVertex)
                {
                    bounds = new Bounds(position, Vector3.zero);
                    hasVertex = true;
                }
                else
                {
                    bounds.Encapsulate(position);
                }
            }

            return bounds;
        }

        private static Vector3 SanitizeVector(Vector3 value)
        {
            return IsFinite(value) ? value : Vector3.zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }

    public sealed class MmdPmxAsset : ScriptableObject
    {
        [SerializeField] private byte[] data = Array.Empty<byte>();
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string sourcePath = string.Empty;
        [SerializeField] private float importScale = 1.0f;
        [SerializeField] private string modelPreset = "Custom";
        [SerializeField] private string meshGenerationMode = "SingleMesh";
        [SerializeField] private string materialTexturePolicy = "ResolveReferencesOnly";
        [SerializeField] private string animationType = "Generic";
        [SerializeField] private string shaderPreset = "MmdBasicUrpToon";
        [SerializeField] private Avatar? importedAvatar;
        [SerializeField] private string humanoidAvatarReadiness = MmdHumanoidSetupAsset.NotEvaluatedReadiness;
        [SerializeField] private string humanoidAvatarDiagnostic = string.Empty;
        [SerializeField] private MmdPmxImportSummaryStatus importSummaryStatus = MmdPmxImportSummaryStatus.NotParsed;
        [SerializeField] private string modelName = string.Empty;
        [SerializeField] private int vertexCount;
        [SerializeField] private int indexCount;
        [SerializeField] private int boneCount;
        [SerializeField] private int morphCount;
        [SerializeField] private int materialCount;
        [SerializeField] private int diffuseTextureReferenceCount;
        [SerializeField] private int sphereTextureReferenceCount;
        [SerializeField] private int toonTextureReferenceCount;
        [SerializeField] private int transparentMaterialCount;
        [SerializeField] private int edgeMaterialCount;
        [SerializeField] private int ikCount;
        [SerializeField] private int rigidbodyCount;
        [SerializeField] private int jointCount;
        [SerializeField] private Vector3 boundsMin;
        [SerializeField] private Vector3 boundsMax;
        [SerializeField] private Vector3 boundsSize;
        [SerializeField] private MmdPmxMaterialSummary[] materialSummaries = Array.Empty<MmdPmxMaterialSummary>();
        [SerializeField] private Mesh importedMesh;
        [SerializeField] private Material[] importedMaterials = Array.Empty<Material>();
        [SerializeField] private Material[] materialRemaps = Array.Empty<Material>();
        [SerializeField] private GameObject importedRoot;
        [SerializeField] private MmdImportReadiness hierarchyReadiness = MmdImportReadiness.NotEvaluated;
        [SerializeField] private MmdImportReadiness rendererReadiness = MmdImportReadiness.NotEvaluated;
        [SerializeField] private MmdImportReadiness boneBindingReadiness = MmdImportReadiness.NotEvaluated;
        [SerializeField] private string hierarchyReadinessDiagnostic = string.Empty;
        [SerializeField] private string rendererReadinessDiagnostic = string.Empty;
        [SerializeField] private string boneBindingReadinessDiagnostic = string.Empty;

        public string SourceId => sourceId;

        public string SourcePath => sourcePath;

        public float ImportScale => importScale;

        public string ModelPreset => modelPreset;

        public string MeshGenerationMode => meshGenerationMode;

        public string MaterialTexturePolicy => materialTexturePolicy;

        public string AnimationType => animationType;

        public string ShaderPreset => shaderPreset;

        public Avatar? ImportedAvatar => importedAvatar;

        public string HumanoidAvatarReadiness => humanoidAvatarReadiness;

        public string HumanoidAvatarDiagnostic => humanoidAvatarDiagnostic;

        public MmdPmxImportSummaryStatus ImportSummaryStatus => importSummaryStatus;

        public string ModelName => modelName;

        public int VertexCount => vertexCount;

        public int IndexCount => indexCount;

        public int BoneCount => boneCount;

        public int MorphCount => morphCount;

        public int MaterialCount => materialCount;

        public int DiffuseTextureReferenceCount => diffuseTextureReferenceCount;

        public int SphereTextureReferenceCount => sphereTextureReferenceCount;

        public int ToonTextureReferenceCount => toonTextureReferenceCount;

        public int TransparentMaterialCount => transparentMaterialCount;

        public int EdgeMaterialCount => edgeMaterialCount;

        public int IkCount => ikCount;

        public int RigidbodyCount => rigidbodyCount;

        public int JointCount => jointCount;

        public Vector3 BoundsMin => boundsMin;

        public Vector3 BoundsMax => boundsMax;

        public Vector3 BoundsSize => boundsSize;

        public MmdPmxMaterialSummary[] MaterialSummaries => materialSummaries;

        public int ByteLength => data.Length;

        public Mesh ImportedMesh => importedMesh;

        public Material[] ImportedMaterials => importedMaterials;

        public Material[] MaterialRemaps => materialRemaps;

        public MmdImportReadiness HierarchyReadiness => hierarchyReadiness;
        public MmdImportReadiness RendererReadiness => rendererReadiness;
        public MmdImportReadiness BoneBindingReadiness => boneBindingReadiness;
        public string HierarchyReadinessDiagnostic => hierarchyReadinessDiagnostic;
        public string RendererReadinessDiagnostic => rendererReadinessDiagnostic;
        public string BoneBindingReadinessDiagnostic => boneBindingReadinessDiagnostic;

        public GameObject ImportedRoot => importedRoot;

        public void Initialize(
            byte[] bytes,
            string assetSourceId,
            string assetSourcePath,
            float assetImportScale = 1.0f,
            string assetModelPreset = "Custom",
            string assetMeshGenerationMode = "SingleMesh",
            string assetMaterialTexturePolicy = "ResolveReferencesOnly",
            string assetShaderPreset = "MmdBasicUrpToon",
            MmdPmxParseSummary? parseSummary = null,
            Mesh importedMeshAsset = null,
            Material[] importedMaterialAssets = null,
            Material[] materialRemapAssets = null,
            GameObject importedRootAsset = null,
            MmdImportReadiness hierarchyReadinessValue = MmdImportReadiness.NotEvaluated,
            MmdImportReadiness rendererReadinessValue = MmdImportReadiness.NotEvaluated,
            MmdImportReadiness boneBindingReadinessValue = MmdImportReadiness.NotEvaluated,
            string hierarchyReadinessDiagnosticValue = null,
            string rendererReadinessDiagnosticValue = null,
            string boneBindingReadinessDiagnosticValue = null,
            string assetAnimationType = "Generic")
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("PMX asset bytes are required.", nameof(bytes));
            }

            data = (byte[])bytes.Clone();
            sourceId = assetSourceId ?? string.Empty;
            sourcePath = assetSourcePath ?? string.Empty;
            importScale = NormalizeImportScale(assetImportScale);
            modelPreset = NormalizeSummaryValue(assetModelPreset, "Custom");
            meshGenerationMode = NormalizeSummaryValue(assetMeshGenerationMode, "SingleMesh");
            materialTexturePolicy = NormalizeSummaryValue(assetMaterialTexturePolicy, "ResolveReferencesOnly");
            animationType = NormalizeSummaryValue(assetAnimationType, "Generic");
            shaderPreset = NormalizeSummaryValue(assetShaderPreset, "MmdBasicUrpToon");
            importedAvatar = null;
            humanoidAvatarReadiness = string.Equals(animationType, "Humanoid", StringComparison.Ordinal)
                ? MmdHumanoidSetupAsset.NotEvaluatedReadiness
                : "NotRequested";
            humanoidAvatarDiagnostic = string.Equals(animationType, "Humanoid", StringComparison.Ordinal)
                ? "humanoid-avatar: not evaluated"
                : "humanoid-avatar: animation type is " + animationType;
            importedMesh = importedMeshAsset;
            importedMaterials = importedMaterialAssets != null
                ? (Material[])importedMaterialAssets.Clone()
                : Array.Empty<Material>();
            materialRemaps = materialRemapAssets != null
                ? (Material[])materialRemapAssets.Clone()
                : Array.Empty<Material>();
            importedRoot = importedRootAsset;
            hierarchyReadiness = hierarchyReadinessValue;
            rendererReadiness = rendererReadinessValue;
            boneBindingReadiness = boneBindingReadinessValue;
            hierarchyReadinessDiagnostic = hierarchyReadinessDiagnosticValue ?? string.Empty;
            rendererReadinessDiagnostic = rendererReadinessDiagnosticValue ?? string.Empty;
            boneBindingReadinessDiagnostic = boneBindingReadinessDiagnosticValue ?? string.Empty;
            ApplyParseSummary(parseSummary);
        }

        public void ApplyHumanoidAvatarImportSummary(
            string assetAnimationType,
            Avatar? avatar,
            string readiness,
            string diagnostic)
        {
            animationType = NormalizeSummaryValue(assetAnimationType, "Generic");
            importedAvatar = avatar;
            humanoidAvatarReadiness = NormalizeSummaryValue(readiness, MmdHumanoidSetupAsset.NotEvaluatedReadiness);
            humanoidAvatarDiagnostic = diagnostic ?? string.Empty;
        }

        public byte[] GetBytesCopy()
        {
            return (byte[])data.Clone();
        }

        public MmdModelDefinition LoadModel(IMmdParser? parser = null)
        {
            if (data.Length == 0)
            {
                throw new InvalidOperationException("PMX asset has no imported bytes.");
            }

            parser ??= new NativeMmdParser();
            return parser.LoadModel(data);
        }

        private static float NormalizeImportScale(float value)
        {
            return float.IsFinite(value) && value > 0.0f ? value : 1.0f;
        }

        private static string NormalizeSummaryValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private void ApplyParseSummary(MmdPmxParseSummary? parseSummary)
        {
            if (!parseSummary.HasValue)
            {
                importSummaryStatus = MmdPmxImportSummaryStatus.NotParsed;
                modelName = string.Empty;
                vertexCount = 0;
                indexCount = 0;
                boneCount = 0;
                morphCount = 0;
                materialCount = 0;
                diffuseTextureReferenceCount = 0;
                sphereTextureReferenceCount = 0;
                toonTextureReferenceCount = 0;
                transparentMaterialCount = 0;
                edgeMaterialCount = 0;
                ikCount = 0;
                rigidbodyCount = 0;
                jointCount = 0;
                boundsMin = Vector3.zero;
                boundsMax = Vector3.zero;
                boundsSize = Vector3.zero;
                materialSummaries = Array.Empty<MmdPmxMaterialSummary>();
                return;
            }

            MmdPmxParseSummary summary = parseSummary.Value;
            importSummaryStatus = MmdPmxImportSummaryStatus.Passed;
            modelName = summary.ModelName;
            vertexCount = summary.VertexCount;
            indexCount = summary.IndexCount;
            boneCount = summary.BoneCount;
            morphCount = summary.MorphCount;
            materialCount = summary.MaterialCount;
            diffuseTextureReferenceCount = summary.DiffuseTextureReferenceCount;
            sphereTextureReferenceCount = summary.SphereTextureReferenceCount;
            toonTextureReferenceCount = summary.ToonTextureReferenceCount;
            transparentMaterialCount = summary.TransparentMaterialCount;
            edgeMaterialCount = summary.EdgeMaterialCount;
            ikCount = summary.IkCount;
            rigidbodyCount = summary.RigidbodyCount;
            jointCount = summary.JointCount;
            boundsMin = summary.BoundsMin;
            boundsMax = summary.BoundsMax;
            boundsSize = summary.BoundsSize;
            materialSummaries = summary.MaterialSummaries != null
                ? (MmdPmxMaterialSummary[])summary.MaterialSummaries.Clone()
                : Array.Empty<MmdPmxMaterialSummary>();
        }

        /// <summary>
        /// Computes hierarchy/renderer/bone-binding readiness from an imported hierarchy root
        /// and the model's bone count. Evidence-based: examines actual GameObject/Component state.
        /// </summary>
        internal static void ComputeHierarchyReadiness(
            GameObject? importedRoot,
            int assetBoneCount,
            out MmdImportReadiness hierarchyReadiness,
            out MmdImportReadiness rendererReadiness,
            out MmdImportReadiness boneBindingReadiness,
            out string hierarchyDiagnostic,
            out string rendererDiagnostic,
            out string boneBindingDiagnostic)
        {
            // Hierarchy readiness.
            if (importedRoot == null)
            {
                hierarchyReadiness = MmdImportReadiness.Blocked;
                hierarchyDiagnostic = "ImportedRoot is null";
            }
            else if (importedRoot.transform.childCount == 0 && importedRoot.GetComponentInChildren<Transform>(includeInactive: true) == importedRoot.transform)
            {
                hierarchyReadiness = MmdImportReadiness.Ready;
                hierarchyDiagnostic = "ImportedRoot exists, no child transforms (static model)";
            }
            else
            {
                hierarchyReadiness = MmdImportReadiness.Ready;
                hierarchyDiagnostic = "ImportedRoot exists with children";
            }

            // Renderer readiness.
            SkinnedMeshRenderer? smr = importedRoot != null
                ? importedRoot.GetComponentInChildren<SkinnedMeshRenderer>(includeInactive: true)
                : null;
            MeshRenderer? meshRenderer = importedRoot != null
                ? importedRoot.GetComponentInChildren<MeshRenderer>(includeInactive: true)
                : null;
            MeshFilter? meshFilter = meshRenderer != null
                ? meshRenderer.GetComponent<MeshFilter>()
                : null;

            if (smr != null)
            {
                if (smr.sharedMesh == null)
                {
                    rendererReadiness = MmdImportReadiness.Blocked;
                    rendererDiagnostic = "SkinnedMeshRenderer has no sharedMesh";
                }
                else
                {
                    rendererReadiness = MmdImportReadiness.Ready;
                    rendererDiagnostic = $"SkinnedMeshRenderer exists with sharedMesh '{smr.sharedMesh.name}'";
                }
            }
            else if (assetBoneCount > 0)
            {
                rendererReadiness = MmdImportReadiness.Blocked;
                rendererDiagnostic = "No SkinnedMeshRenderer found (skinned model has bones)";
            }
            else if (meshRenderer == null)
            {
                rendererReadiness = MmdImportReadiness.Blocked;
                rendererDiagnostic = "No MeshRenderer found for static model";
            }
            else if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                rendererReadiness = MmdImportReadiness.Blocked;
                rendererDiagnostic = "MeshRenderer has no MeshFilter with sharedMesh";
            }
            else
            {
                rendererReadiness = MmdImportReadiness.Ready;
                rendererDiagnostic = $"MeshRenderer exists with sharedMesh '{meshFilter.sharedMesh.name}'";
            }

            // Bone binding readiness.
            if (smr == null)
            {
                boneBindingReadiness = assetBoneCount > 0 ? MmdImportReadiness.Blocked : MmdImportReadiness.NotEvaluated;
                boneBindingDiagnostic = assetBoneCount > 0
                    ? "No SkinnedMeshRenderer to bind bones"
                    : "Static renderer path, no bone binding evaluated";
            }
            else
            {
                Transform[] bones = smr.bones;
                int nullBoneCount = 0;
                if (bones != null)
                {
                    foreach (Transform t in bones)
                    {
                        if (t == null) nullBoneCount++;
                    }
                }

                int actualBoneCount = bones?.Length ?? 0;
                int bindposeCount = smr.sharedMesh?.bindposes?.Length ?? 0;

                if (nullBoneCount > 0)
                {
                    boneBindingReadiness = MmdImportReadiness.Blocked;
                    boneBindingDiagnostic = $"{nullBoneCount} null bone(s) in renderer.bones";
                }
                else if (assetBoneCount > 0 && (actualBoneCount != assetBoneCount || bindposeCount != assetBoneCount))
                {
                    boneBindingReadiness = MmdImportReadiness.Blocked;
                    boneBindingDiagnostic = $"Bone count mismatch: renderer.bones={actualBoneCount}, bindposes={bindposeCount}, asset={assetBoneCount}";
                }
                else if (actualBoneCount == 0 && assetBoneCount == 0)
                {
                    boneBindingReadiness = MmdImportReadiness.Ready;
                    boneBindingDiagnostic = "No bones (static model)";
                }
                else
                {
                    boneBindingReadiness = MmdImportReadiness.Ready;
                    boneBindingDiagnostic = $"{actualBoneCount} bone(s) with {bindposeCount} bindpose(s), no nulls";
                }
            }
        }
    }
}
