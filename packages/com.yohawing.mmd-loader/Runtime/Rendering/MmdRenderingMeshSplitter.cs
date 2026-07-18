#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmd.Rendering
{
    [Serializable]
    public sealed class MmdSplitVertexSourceDescriptor
    {
        public int splitVertexIndex;
        public int sourceVertexIndex;
        public int submeshIndex;
        public int materialIndex;
    }

    [Serializable]
    public sealed class MmdRenderingMeshSplitResult
    {
        public MmdRenderingDescriptor rendering = new();
        public List<MmdSplitVertexSourceDescriptor> vertexSources = new();
        public int sourceVertexCount;
        public int splitVertexCount;
        public int duplicatedVertexCount;
    }

    public static class MmdRenderingMeshSplitter
    {
        public static MmdRenderingMeshSplitResult SplitBySubmesh(MmdRenderingDescriptor source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ValidateSource(source);

            var sourceVertices = source.vertices.ToDictionary(vertex => vertex.vertexIndex, vertex => vertex);
            var sourceSkinning = (source.skinning ?? new List<MmdSkinningDescriptor>())
                .ToDictionary(skinning => skinning.vertexIndex, skinning => skinning);
            var splitVertices = new List<MmdMeshVertexDescriptor>(source.indices.Count);
            var splitIndices = new List<int>(source.indices.Count);
            var splitSkinning = new List<MmdSkinningDescriptor>(source.indices.Count);
            var splitSubmeshes = new List<MmdSubmeshDescriptor>(source.submeshes.Count);
            var vertexSources = new List<MmdSplitVertexSourceDescriptor>(source.indices.Count);
            var splitIndicesBySourceVertex = new Dictionary<int, List<int>>(source.vertices.Count);
            var splitIndexBySubmeshAndSource = new Dictionary<string, int>(source.indices.Count, StringComparer.Ordinal);

            foreach (MmdSubmeshDescriptor submesh in source.submeshes.OrderBy(item => item.submeshIndex))
            {
                int splitIndexStart = splitIndices.Count;
                int sourceEnd = submesh.indexStart + submesh.indexCount;
                for (int i = submesh.indexStart; i < sourceEnd; i++)
                {
                    int sourceVertexIndex = source.indices[i];
                    string key = BuildKey(submesh.submeshIndex, sourceVertexIndex);
                    if (!splitIndexBySubmeshAndSource.TryGetValue(key, out int splitVertexIndex))
                    {
                        splitVertexIndex = splitVertices.Count;
                        splitIndexBySubmeshAndSource[key] = splitVertexIndex;
                        splitVertices.Add(CloneVertex(sourceVertices[sourceVertexIndex], splitVertexIndex));
                        splitSkinning.Add(CloneSkinning(sourceSkinning[sourceVertexIndex], splitVertexIndex));
                        vertexSources.Add(new MmdSplitVertexSourceDescriptor
                        {
                            splitVertexIndex = splitVertexIndex,
                            sourceVertexIndex = sourceVertexIndex,
                            submeshIndex = submesh.submeshIndex,
                            materialIndex = submesh.materialIndex
                        });

                        if (!splitIndicesBySourceVertex.TryGetValue(sourceVertexIndex, out List<int>? splitVertexIndices))
                        {
                            splitVertexIndices = new List<int>();
                            splitIndicesBySourceVertex[sourceVertexIndex] = splitVertexIndices;
                        }

                        splitVertexIndices.Add(splitVertexIndex);
                    }

                    splitIndices.Add(splitVertexIndex);
                }

                splitSubmeshes.Add(new MmdSubmeshDescriptor
                {
                    submeshIndex = submesh.submeshIndex,
                    materialIndex = submesh.materialIndex,
                    indexStart = splitIndexStart,
                    indexCount = submesh.indexCount
                });
            }

            MmdRenderingDescriptor splitRendering = new()
            {
                vertices = splitVertices,
                indices = splitIndices,
                skinning = splitSkinning.OrderBy(skinning => skinning.vertexIndex).ToList(),
                materials = CloneMaterials(source.materials),
                submeshes = splitSubmeshes,
                urpMaterialBindings = CloneUrpBindings(source.urpMaterialBindings),
                vertexMorphs = SplitVertexMorphs(source.vertexMorphs, splitIndicesBySourceVertex),
                groupMorphs = CloneGroupMorphs(source.groupMorphs),
                uvMorphs = SplitUvMorphs(source.uvMorphs, splitIndicesBySourceVertex),
                materialMorphs = CloneMaterialMorphs(source.materialMorphs),
                flipMorphs = CloneFlipMorphs(source.flipMorphs),
                textureOrientation = MmdTextureOrientationDescriptorBuilder.Build(splitVertices),
                ikCount = source.ikCount
            };

            return new MmdRenderingMeshSplitResult
            {
                rendering = splitRendering,
                vertexSources = vertexSources.OrderBy(item => item.splitVertexIndex).ToList(),
                sourceVertexCount = source.vertices.Count,
                splitVertexCount = splitVertices.Count,
                duplicatedVertexCount = Math.Max(0, splitVertices.Count - source.vertices.Count)
            };
        }

        private static void ValidateSource(MmdRenderingDescriptor source)
        {
            if (source.vertices == null)
            {
                throw new ArgumentException("Rendering descriptor vertices are required.", nameof(source));
            }

            if (source.indices == null)
            {
                throw new ArgumentException("Rendering descriptor indices are required.", nameof(source));
            }

            if (source.skinning == null)
            {
                throw new ArgumentException("Rendering descriptor skinning is required.", nameof(source));
            }

            if (source.materials == null)
            {
                throw new ArgumentException("Rendering descriptor materials are required.", nameof(source));
            }

            if (source.submeshes == null)
            {
                throw new ArgumentException("Rendering descriptor submeshes are required.", nameof(source));
            }

            var vertexIndices = new HashSet<int>(source.vertices.Count);
            foreach (MmdMeshVertexDescriptor vertex in source.vertices)
            {
                if (vertex == null)
                {
                    throw new ArgumentException("Rendering descriptor vertices must not contain null entries.", nameof(source));
                }

                if (!vertexIndices.Add(vertex.vertexIndex))
                {
                    throw new ArgumentException($"Duplicate rendering vertex index: {vertex.vertexIndex}", nameof(source));
                }
            }

            var skinningIndices = new HashSet<int>(source.skinning.Count);
            foreach (MmdSkinningDescriptor skinning in source.skinning)
            {
                if (skinning == null)
                {
                    throw new ArgumentException("Rendering descriptor skinning must not contain null entries.", nameof(source));
                }

                skinningIndices.Add(skinning.vertexIndex);
            }

            foreach (int index in source.indices)
            {
                if (!vertexIndices.Contains(index))
                {
                    throw new ArgumentException($"Rendering index references missing source vertex: {index}", nameof(source));
                }

                if (!skinningIndices.Contains(index))
                {
                    throw new ArgumentException($"Rendering index references a vertex without skinning: {index}", nameof(source));
                }
            }

            foreach (MmdSubmeshDescriptor submesh in source.submeshes)
            {
                if (submesh.indexStart < 0 || submesh.indexCount < 0 || submesh.indexStart + submesh.indexCount > source.indices.Count)
                {
                    throw new ArgumentException($"Submesh {submesh.submeshIndex} range exceeds the source index buffer.", nameof(source));
                }
            }
        }

        private static string BuildKey(int submeshIndex, int sourceVertexIndex)
        {
            return submeshIndex.ToString() + ":" + sourceVertexIndex.ToString();
        }

        private static MmdMeshVertexDescriptor CloneVertex(MmdMeshVertexDescriptor source, int splitVertexIndex)
        {
            return new MmdMeshVertexDescriptor
            {
                vertexIndex = splitVertexIndex,
                position = source.position.ToArray(),
                normal = source.normal.ToArray(),
                uv = source.uv.ToArray(),
                edgeScale = source.edgeScale
            };
        }

        private static MmdSkinningDescriptor CloneSkinning(MmdSkinningDescriptor source, int splitVertexIndex)
        {
            return new MmdSkinningDescriptor
            {
                vertexIndex = splitVertexIndex,
                skinningMode = source.skinningMode,
                normalizedFamily = source.normalizedFamily,
                boneIndices = source.boneIndices.ToArray(),
                boneWeights = source.boneWeights.ToArray(),
                supportStatus = source.supportStatus,
                supportReason = source.supportReason,
                exactRuntimeSkinning = source.exactRuntimeSkinning,
                linearFallbackToBoneWeights = source.linearFallbackToBoneWeights
            };
        }

        private static List<MmdMaterialDescriptor> CloneMaterials(IReadOnlyList<MmdMaterialDescriptor> source)
        {
            return source
                .OrderBy(material => material.materialIndex)
                .Select(material => new MmdMaterialDescriptor
                {
                    materialIndex = material.materialIndex,
                    name = material.name,
                    texture = material.texture,
                    sphereTexture = material.sphereTexture,
                    toonTexture = material.toonTexture,
                    alpha = material.alpha,
                    diffuseColor = material.diffuseColor.ToArray(),
                    ambientColor = material.ambientColor.ToArray(),
                    toonBoundary = material.toonBoundary,
                    toonFeather = material.toonFeather,
                    toonBandCount = material.toonBandCount,
                    stylizedSpecularColor = material.stylizedSpecularColor.ToArray(),
                    stylizedSpecularBoundary = material.stylizedSpecularBoundary,
                    stylizedSpecularFeather = material.stylizedSpecularFeather,
                    rimColor = material.rimColor.ToArray(),
                    rimBoundary = material.rimBoundary,
                    rimFeather = material.rimFeather,
                    rimLightFollow = material.rimLightFollow,
                    emissionColor = material.emissionColor.ToArray(),
                    emissionIntensity = material.emissionIntensity,
                    usesEmissionMap = material.usesEmissionMap,
                    usesEmissionMask = material.usesEmissionMask,
                    edgeColor = material.edgeColor.ToArray(),
                    edgeSize = material.edgeSize,
                    sphereTextureMode = material.sphereTextureMode,
                    toonShared = material.toonShared,
                    sharedToonIndex = material.sharedToonIndex,
                    cullingPolicy = material.cullingPolicy,
                    drawEdgeFlag = material.drawEdgeFlag,
                    vertexStart = material.vertexStart,
                    vertexCount = material.vertexCount
                })
                .ToList();
        }

        private static List<MmdUrpMaterialBindingDescriptor> CloneUrpBindings(IReadOnlyList<MmdUrpMaterialBindingDescriptor> source)
        {
            return source
                .OrderBy(binding => binding.materialIndex)
                .Select(binding => new MmdUrpMaterialBindingDescriptor
                {
                    materialIndex = binding.materialIndex,
                    name = binding.name,
                    shaderName = binding.shaderName,
                    baseMapTexture = binding.baseMapTexture,
                    sphereTexture = binding.sphereTexture,
                    toonTexture = binding.toonTexture,
                    usesSphereTexture = binding.usesSphereTexture,
                    usesToonTexture = binding.usesToonTexture,
                    sphereTextureModeHint = binding.sphereTextureModeHint,
                    toonTextureSourceHint = binding.toonTextureSourceHint,
                    alpha = binding.alpha,
                    diffuseColor = binding.diffuseColor.ToArray(),
                    ambientColor = binding.ambientColor.ToArray(),
                    toonBoundary = binding.toonBoundary,
                    toonFeather = binding.toonFeather,
                    toonBandCount = binding.toonBandCount,
                    stylizedSpecularColor = binding.stylizedSpecularColor.ToArray(),
                    stylizedSpecularBoundary = binding.stylizedSpecularBoundary,
                    stylizedSpecularFeather = binding.stylizedSpecularFeather,
                    rimColor = binding.rimColor.ToArray(),
                    rimBoundary = binding.rimBoundary,
                    rimFeather = binding.rimFeather,
                    rimLightFollow = binding.rimLightFollow,
                    emissionColor = binding.emissionColor.ToArray(),
                    emissionIntensity = binding.emissionIntensity,
                    usesEmissionMap = binding.usesEmissionMap,
                    usesEmissionMask = binding.usesEmissionMask,
                    edgeColor = binding.edgeColor.ToArray(),
                    edgeSize = binding.edgeSize,
                    drawEdgeFlag = binding.drawEdgeFlag,
                    isTransparent = binding.isTransparent,
                    transparencyMode = binding.transparencyMode,
                    renderOrderBucket = binding.renderOrderBucket,
                    cullingPolicy = binding.cullingPolicy,
                    vertexStart = binding.vertexStart,
                    vertexCount = binding.vertexCount
                })
                .ToList();
        }

        private static List<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor> CloneMaterialMorphs(
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor>? source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor>();
            }

            return source
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => new MmdMorphDescriptorBuilder.MmdMaterialMorphDescriptor
                {
                    morphIndex = morph.morphIndex,
                    morphName = morph.morphName,
                    offsets = (morph.offsets ?? new List<MmdMorphDescriptorBuilder.MmdMaterialMorphOffsetDescriptor>())
                        .OrderBy(offset => offset.materialIndex)
                        .Select(offset => new MmdMorphDescriptorBuilder.MmdMaterialMorphOffsetDescriptor
                        {
                            materialIndex = offset.materialIndex,
                            operation = offset.operation,
                            diffuseColor = offset.diffuseColor.ToArray(),
                            diffuseOpacity = offset.diffuseOpacity,
                            ambientColor = offset.ambientColor.ToArray(),
                            edgeColor = offset.edgeColor.ToArray(),
                            edgeOpacity = offset.edgeOpacity,
                            edgeSize = offset.edgeSize,
                            allOffsetsFinite = offset.allOffsetsFinite
                        })
                        .ToList()
                })
                .ToList();
        }

        private static List<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor> CloneFlipMorphs(
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>? source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor>();
            }

            return source
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => new MmdMorphDescriptorBuilder.MmdFlipMorphDescriptor
                {
                    morphIndex = morph.morphIndex,
                    morphName = morph.morphName,
                    flipOffsetCount = morph.flipOffsetCount,
                    offsets = (morph.offsets ?? new List<MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor>())
                        .Select(offset => new MmdMorphDescriptorBuilder.MmdFlipMorphOffsetDescriptor
                        {
                            targetMorphIndex = offset.targetMorphIndex,
                            targetMorphName = offset.targetMorphName,
                            targetMorphType = offset.targetMorphType,
                            weight = offset.weight,
                            finiteWeight = offset.finiteWeight
                        })
                        .ToList()
                })
                .ToList();
        }

        private static List<MmdVertexMorphDescriptor> SplitVertexMorphs(
            IReadOnlyList<MmdVertexMorphDescriptor> source,
            IReadOnlyDictionary<int, List<int>> splitIndicesBySourceVertex)
        {
            return (source ?? Array.Empty<MmdVertexMorphDescriptor>())
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => new MmdVertexMorphDescriptor
                {
                    morphIndex = morph.morphIndex,
                    morphName = morph.morphName,
                    offsets = SplitVertexMorphOffsets(morph, splitIndicesBySourceVertex)
                })
                .ToList();
        }

        private static List<MmdGroupMorphDescriptor> CloneGroupMorphs(IReadOnlyList<MmdGroupMorphDescriptor>? source)
        {
            if (source == null || source.Count == 0)
            {
                return new List<MmdGroupMorphDescriptor>();
            }

            return source
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => new MmdGroupMorphDescriptor
                {
                    morphIndex = morph.morphIndex,
                    morphName = morph.morphName,
                    offsets = (morph.offsets ?? new List<MmdGroupMorphOffsetDescriptor>())
                        .OrderBy(offset => offset.targetMorphIndex)
                        .Select(offset => new MmdGroupMorphOffsetDescriptor
                        {
                            targetMorphIndex = offset.targetMorphIndex,
                            targetMorphName = offset.targetMorphName,
                            weight = offset.weight
                        })
                        .ToList()
                })
                .ToList();
        }

        private static List<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor> SplitUvMorphs(
            IReadOnlyList<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor>? source,
            IReadOnlyDictionary<int, List<int>> splitIndicesBySourceVertex)
        {
            return (source ?? Array.Empty<MmdMorphDescriptorBuilder.MmdUvMorphDescriptor>())
                .OrderBy(morph => morph.morphIndex)
                .Select(morph => new MmdMorphDescriptorBuilder.MmdUvMorphDescriptor
                {
                    morphIndex = morph.morphIndex,
                    morphName = morph.morphName,
                    morphType = morph.morphType,
                    uvOffsetCount = morph.uvOffsetCount,
                    offsets = SplitUvMorphOffsets(morph, splitIndicesBySourceVertex)
                })
                .ToList();
        }

        private static List<MmdVertexMorphOffsetDescriptor> SplitVertexMorphOffsets(
            MmdVertexMorphDescriptor morph,
            IReadOnlyDictionary<int, List<int>> splitIndicesBySourceVertex)
        {
            var offsets = new List<MmdVertexMorphOffsetDescriptor>();
            foreach (MmdVertexMorphOffsetDescriptor offset in (morph.offsets ?? new List<MmdVertexMorphOffsetDescriptor>()).OrderBy(item => item.vertexIndex))
            {
                if (!splitIndicesBySourceVertex.TryGetValue(offset.vertexIndex, out List<int>? splitVertexIndices))
                {
                    continue;
                }

                foreach (int splitVertexIndex in splitVertexIndices.OrderBy(index => index))
                {
                    offsets.Add(new MmdVertexMorphOffsetDescriptor
                    {
                        vertexIndex = splitVertexIndex,
                        positionDelta = offset.positionDelta.ToArray()
                    });
                }
            }

            return offsets.OrderBy(offset => offset.vertexIndex).ToList();
        }

        private static List<MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor> SplitUvMorphOffsets(
            MmdMorphDescriptorBuilder.MmdUvMorphDescriptor morph,
            IReadOnlyDictionary<int, List<int>> splitIndicesBySourceVertex)
        {
            var offsets = new List<MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor>();
            foreach (MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor offset in (morph.offsets ?? new List<MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor>()).OrderBy(item => item.vertexIndex))
            {
                if (!splitIndicesBySourceVertex.TryGetValue(offset.vertexIndex, out List<int>? splitVertexIndices))
                {
                    continue;
                }

                foreach (int splitVertexIndex in splitVertexIndices.OrderBy(index => index))
                {
                    offsets.Add(new MmdMorphDescriptorBuilder.MmdUvMorphOffsetDescriptor
                    {
                        vertexIndex = splitVertexIndex,
                        positionDelta = offset.positionDelta.ToArray(),
                        targetVertexExists = true,
                        allPayloadFinite = offset.allPayloadFinite
                    });
                }
            }

            return offsets.OrderBy(offset => offset.vertexIndex).ToList();
        }
    }
}
