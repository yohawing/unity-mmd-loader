using System;
using System.Collections.Generic;

namespace Yohawing.MmdUnity.Parser
{
    public static class MmdModelValidator
    {
        public static IReadOnlyList<string> ValidateStructuralModel(MmdModelDefinition model)
        {
            var errors = new List<string>();
            if (model == null)
            {
                errors.Add("model is null");
                return errors;
            }

            IReadOnlyList<MmdVertexDefinition> vertices = model.vertices != null ? model.vertices : Array.Empty<MmdVertexDefinition>();
            IReadOnlyList<int> indices = model.indices != null ? model.indices : Array.Empty<int>();
            IReadOnlyList<MmdBoneDefinition> bones = model.bones != null ? model.bones : Array.Empty<MmdBoneDefinition>();
            IReadOnlyList<MmdMorphDefinition> morphs = model.morphs != null ? model.morphs : Array.Empty<MmdMorphDefinition>();
            IReadOnlyList<MmdMaterialDefinition> materials = model.materials != null ? model.materials : Array.Empty<MmdMaterialDefinition>();
            IReadOnlyList<MmdIkDefinition> ikDefinitions = model.ik != null ? model.ik : Array.Empty<MmdIkDefinition>();
            if (model.vertices == null)
            {
                errors.Add("model vertices must not be null");
            }

            if (model.indices == null)
            {
                errors.Add("model indices must not be null");
            }

            if (model.bones == null)
            {
                errors.Add("model bones must not be null");
            }

            if (model.morphs == null)
            {
                errors.Add("model morphs must not be null");
            }

            if (model.materials == null)
            {
                errors.Add("model materials must not be null");
            }

            if (model.ik == null)
            {
                errors.Add("model ik must not be null");
            }

            var boneIndices = new HashSet<int>(bones.Count);
            var boneNames = new HashSet<string>(bones.Count, StringComparer.Ordinal);
            var vertexIndices = new HashSet<int>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                MmdVertexDefinition vertex = vertices[i];
                if (!vertexIndices.Add(vertex.index))
                {
                    errors.Add($"duplicate vertex index: {vertex.index}");
                }

                if (vertex.position == null || vertex.position.Length != 3)
                {
                    errors.Add($"vertex position must have 3 values: {vertex.index}");
                }
                else if (HasNonFinite(vertex.position))
                {
                    errors.Add($"vertex position must contain only finite values: {vertex.index}");
                }

                if (vertex.normal == null || vertex.normal.Length != 3)
                {
                    errors.Add($"vertex normal must have 3 values: {vertex.index}");
                }
                else if (HasNonFinite(vertex.normal))
                {
                    errors.Add($"vertex normal must contain only finite values: {vertex.index}");
                }

                if (vertex.uv == null || vertex.uv.Length != 2)
                {
                    errors.Add($"vertex uv must have 2 values: {vertex.index}");
                }
                else if (HasNonFinite(vertex.uv))
                {
                    errors.Add($"vertex uv must contain only finite values: {vertex.index}");
                }

                if (vertex.boneIndices == null || vertex.boneWeights == null || vertex.boneIndices.Length != vertex.boneWeights.Length)
                {
                    errors.Add($"vertex bone index/weight arrays must have the same length: {vertex.index}");
                }
                else if (HasNonFinite(vertex.boneWeights))
                {
                    errors.Add($"vertex boneWeights must contain only finite values: {vertex.index}");
                }

                if (vertex.hasSdefParameters)
                {
                    if (!string.Equals(NormalizeSkinningMode(vertex.skinningMode), "sdef", StringComparison.Ordinal))
                    {
                        errors.Add($"vertex SDEF parameters require sdef skinningMode: {vertex.index}");
                    }

                    ValidateRequiredVec3(errors, vertex.sdefC, $"vertex sdefC: {vertex.index}");
                    ValidateRequiredVec3(errors, vertex.sdefR0, $"vertex sdefR0: {vertex.index}");
                    ValidateRequiredVec3(errors, vertex.sdefR1, $"vertex sdefR1: {vertex.index}");
                }
                else
                {
                    ValidateAbsentOrEmpty(errors, vertex.sdefC, $"vertex sdefC must be empty when SDEF parameters are absent: {vertex.index}");
                    ValidateAbsentOrEmpty(errors, vertex.sdefR0, $"vertex sdefR0 must be empty when SDEF parameters are absent: {vertex.index}");
                    ValidateAbsentOrEmpty(errors, vertex.sdefR1, $"vertex sdefR1 must be empty when SDEF parameters are absent: {vertex.index}");
                }
            }

            if (indices.Count > 0)
            {
                if (indices.Count % 3 != 0)
                {
                    errors.Add("model indices count must be a multiple of 3");
                }

                for (int i = 0; i < indices.Count; i++)
                {
                    int vertexIndex = indices[i];
                    if (!vertexIndices.Contains(vertexIndex))
                    {
                        errors.Add($"model index does not reference an existing vertex: {i} -> {vertexIndex}");
                    }
                }
            }

            for (int i = 0; i < bones.Count; i++)
            {
                MmdBoneDefinition bone = bones[i];
                if (!boneIndices.Add(bone.index))
                {
                    errors.Add($"duplicate bone index: {bone.index}");
                }

                if (bone.origin == null || bone.origin.Length != 3)
                {
                    errors.Add($"bone origin must have 3 values: {bone.index}");
                }
                else if (HasNonFinite(bone.origin))
                {
                    errors.Add($"bone origin must contain only finite values: {bone.index}");
                }

                ValidateOptionalAxis(errors, bone.fixedAxis, bone.fixedAxisVector, $"bone fixedAxisVector: {bone.index}");
                ValidateOptionalAxis(errors, bone.localAxes, bone.localXAxis, $"bone localXAxis: {bone.index}");
                ValidateOptionalAxis(errors, bone.localAxes, bone.localZAxis, $"bone localZAxis: {bone.index}");

                if (!string.IsNullOrWhiteSpace(bone.name) && !boneNames.Add(bone.name))
                {
                    errors.Add($"duplicate bone name: {bone.name}");
                }
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                MmdVertexDefinition vertex = vertices[i];
                if (vertex.boneIndices == null)
                {
                    continue;
                }

                for (int boneSlot = 0; boneSlot < vertex.boneIndices.Length; boneSlot++)
                {
                    int boneIndex = vertex.boneIndices[boneSlot];
                    if (!boneIndices.Contains(boneIndex))
                    {
                        errors.Add($"vertex bone index does not reference an existing bone: {vertex.index}[{boneSlot}] -> {boneIndex}");
                    }
                }
            }

            for (int i = 0; i < bones.Count; i++)
            {
                MmdBoneDefinition bone = bones[i];
                if (bone.parentIndex != -1 && !boneIndices.Contains(bone.parentIndex))
                {
                    errors.Add($"bone parentIndex does not reference an existing bone: {bone.index} -> {bone.parentIndex}");
                }

                if (bone.appendParentIndex != -1 && !boneIndices.Contains(bone.appendParentIndex))
                {
                    errors.Add($"bone appendParentIndex does not reference an existing bone: {bone.index} -> {bone.appendParentIndex}");
                }
            }

            for (int i = 0; i < ikDefinitions.Count; i++)
            {
                MmdIkDefinition ik = ikDefinitions[i];
                if (!boneIndices.Contains(ik.boneIndex))
                {
                    errors.Add($"ik boneIndex does not reference an existing bone: {i} -> {ik.boneIndex}");
                }

                if (!boneIndices.Contains(ik.targetBoneIndex))
                {
                    errors.Add($"ik targetBoneIndex does not reference an existing bone: {i} -> {ik.targetBoneIndex}");
                }

                if (ik.iterationCount < 0)
                {
                    errors.Add($"ik iterationCount must not be negative: {i}");
                }

                if (ik.angleLimit < 0.0f)
                {
                    errors.Add($"ik angleLimit must not be negative: {i}");
                }

                IReadOnlyList<MmdIkLinkDefinition> links = ik.links != null ? ik.links : Array.Empty<MmdIkLinkDefinition>();
                if (ik.links == null)
                {
                    errors.Add($"ik links must not be null: {i}");
                }

                for (int linkIndex = 0; linkIndex < links.Count; linkIndex++)
                {
                    MmdIkLinkDefinition link = links[linkIndex];
                    if (!boneIndices.Contains(link.boneIndex))
                    {
                        errors.Add($"ik link boneIndex does not reference an existing bone: {i}[{linkIndex}] -> {link.boneIndex}");
                    }

                    if (link.hasLimit)
                    {
                        if (link.minimumAngle == null || link.minimumAngle.Length != 3)
                        {
                            errors.Add($"ik link minimumAngle must have 3 values: {i}[{linkIndex}]");
                        }
                        else if (HasNonFinite(link.minimumAngle))
                        {
                            errors.Add($"ik link minimumAngle must contain only finite values: {i}[{linkIndex}]");
                        }

                        if (link.maximumAngle == null || link.maximumAngle.Length != 3)
                        {
                            errors.Add($"ik link maximumAngle must have 3 values: {i}[{linkIndex}]");
                        }
                        else if (HasNonFinite(link.maximumAngle))
                        {
                            errors.Add($"ik link maximumAngle must contain only finite values: {i}[{linkIndex}]");
                        }
                    }
                }
            }

            var morphIndices = new HashSet<int>(morphs.Count);
            // First pass: collect morph indices and validate morph-level fields
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                if (!morphIndices.Add(morph.index))
                {
                    errors.Add($"duplicate morph index: {morph.index}");
                }

                if (string.IsNullOrWhiteSpace(morph.name))
                {
                    errors.Add($"morph name is required: {morph.index}");
                }

                if (string.IsNullOrWhiteSpace(morph.type))
                {
                    errors.Add($"morph type is required: {morph.index}");
                }

                IReadOnlyList<MmdVertexMorphOffsetDefinition> vertexOffsets = morph.vertexOffsets != null ? morph.vertexOffsets : Array.Empty<MmdVertexMorphOffsetDefinition>();
                if (morph.vertexOffsets == null)
                {
                    errors.Add($"morph vertexOffsets must not be null: {morph.index}");
                }

                for (int offsetIndex = 0; offsetIndex < vertexOffsets.Count; offsetIndex++)
                {
                    MmdVertexMorphOffsetDefinition offset = vertexOffsets[offsetIndex];
                    if (!vertexIndices.Contains(offset.vertexIndex))
                    {
                        errors.Add($"morph vertex offset does not reference an existing vertex: {morph.index}[{offsetIndex}] -> {offset.vertexIndex}");
                    }

                    if (offset.positionDelta == null || offset.positionDelta.Length != 3)
                    {
                        errors.Add($"morph vertex offset positionDelta must have 3 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.positionDelta))
                    {
                        errors.Add($"morph vertex offset positionDelta must contain only finite values: {morph.index}[{offsetIndex}]");
                    }
                }

                if (morph.groupOffsets == null)
                {
                    errors.Add($"morph groupOffsets must not be null: {morph.index}");
                }

                if (morph.materialOffsets == null)
                {
                    errors.Add($"morph materialOffsets must not be null: {morph.index}");
                }

                if (morph.uvOffsets == null)
                {
                    errors.Add($"morph uvOffsets must not be null: {morph.index}");
                }
            }

            // Second pass: validate group morph offset references (requires all morph indices to be collected)
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                IReadOnlyList<MmdGroupMorphOffsetDefinition> groupOffsets = morph.groupOffsets != null ? morph.groupOffsets : Array.Empty<MmdGroupMorphOffsetDefinition>();
                for (int offsetIndex = 0; offsetIndex < groupOffsets.Count; offsetIndex++)
                {
                    MmdGroupMorphOffsetDefinition offset = groupOffsets[offsetIndex];
                    if (!morphIndices.Contains(offset.morphIndex))
                    {
                        errors.Add($"morph group offset target morph index does not exist: {morph.index}[{offsetIndex}] -> {offset.morphIndex}");
                    }

                    if (offset.morphIndex == morph.index)
                    {
                        errors.Add($"morph group offset self-reference is not allowed: {morph.index}[{offsetIndex}]");
                    }

                    if (float.IsNaN(offset.weight) || float.IsInfinity(offset.weight))
                    {
                        errors.Add($"morph group offset weight must be finite: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            // Third pass: validate material morph offset fields
            var knownOperations = new HashSet<string>(StringComparer.Ordinal)
            {
                "add", "multiply", "unknown"
            };
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                IReadOnlyList<MmdMaterialMorphOffsetDefinition> materialOffsets = morph.materialOffsets != null ? morph.materialOffsets : Array.Empty<MmdMaterialMorphOffsetDefinition>();
                for (int offsetIndex = 0; offsetIndex < materialOffsets.Count; offsetIndex++)
                {
                    MmdMaterialMorphOffsetDefinition offset = materialOffsets[offsetIndex];
                    bool materialIndexExists = offset.materialIndex == -1;
                    for (int materialIndex = 0; materialIndex < materials.Count && !materialIndexExists; materialIndex++)
                    {
                        materialIndexExists = materials[materialIndex].index == offset.materialIndex;
                    }

                    if (!materialIndexExists)
                    {
                        errors.Add($"morph material offset materialIndex does not reference an existing material: {morph.index}[{offsetIndex}] -> {offset.materialIndex}");
                    }

                    if (!knownOperations.Contains(offset.operation))
                    {
                        errors.Add($"morph material offset operation must be add, multiply, or unknown: {morph.index}[{offsetIndex}] -> {offset.operation}");
                    }

                    ValidateRequiredVec3(errors, offset.diffuseColor, $"morph material offset diffuseColor: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec3(errors, offset.ambientColor, $"morph material offset ambientColor: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec3(errors, offset.specularColor, $"morph material offset specularColor: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec3(errors, offset.edgeColor, $"morph material offset edgeColor: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec4(errors, offset.diffuseTextureBlend, $"morph material offset diffuseTextureBlend: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec4(errors, offset.sphereTextureBlend, $"morph material offset sphereTextureBlend: {morph.index}[{offsetIndex}]");
                    ValidateRequiredVec4(errors, offset.toonTextureBlend, $"morph material offset toonTextureBlend: {morph.index}[{offsetIndex}]");

                    if (float.IsNaN(offset.diffuseOpacity) || float.IsInfinity(offset.diffuseOpacity))
                    {
                        errors.Add($"morph material offset diffuseOpacity must be finite: {morph.index}[{offsetIndex}]");
                    }

                    if (float.IsNaN(offset.specularPower) || float.IsInfinity(offset.specularPower))
                    {
                        errors.Add($"morph material offset specularPower must be finite: {morph.index}[{offsetIndex}]");
                    }

                    if (float.IsNaN(offset.edgeOpacity) || float.IsInfinity(offset.edgeOpacity))
                    {
                        errors.Add($"morph material offset edgeOpacity must be finite: {morph.index}[{offsetIndex}]");
                    }

                    if (float.IsNaN(offset.edgeSize) || float.IsInfinity(offset.edgeSize))
                    {
                        errors.Add($"morph material offset edgeSize must be finite: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            // Fourth pass: validate UV morph offset fields
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                IReadOnlyList<MmdUvMorphOffsetDefinition> uvOffsets = morph.uvOffsets != null ? morph.uvOffsets : Array.Empty<MmdUvMorphOffsetDefinition>();
                for (int offsetIndex = 0; offsetIndex < uvOffsets.Count; offsetIndex++)
                {
                    MmdUvMorphOffsetDefinition offset = uvOffsets[offsetIndex];
                    if (!vertexIndices.Contains(offset.vertexIndex))
                    {
                        errors.Add($"morph UV offset does not reference an existing vertex: {morph.index}[{offsetIndex}] -> {offset.vertexIndex}");
                    }

                    if (offset.positionDelta == null || offset.positionDelta.Length != 4)
                    {
                        errors.Add($"morph UV offset positionDelta must have 4 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.positionDelta))
                    {
                        errors.Add($"morph UV offset positionDelta must contain only finite values: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            // Fifth pass: validate bone morph offset fields
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                IReadOnlyList<MmdBoneMorphOffsetDefinition> boneOffsets = morph.boneOffsets != null ? morph.boneOffsets : Array.Empty<MmdBoneMorphOffsetDefinition>();
                if (morph.boneOffsets == null)
                {
                    errors.Add($"morph boneOffsets must not be null: {morph.index}");
                }

                for (int offsetIndex = 0; offsetIndex < boneOffsets.Count; offsetIndex++)
                {
                    MmdBoneMorphOffsetDefinition offset = boneOffsets[offsetIndex];
                    if (!boneIndices.Contains(offset.boneIndex))
                    {
                        errors.Add($"morph bone offset boneIndex does not reference an existing bone: {morph.index}[{offsetIndex}] -> {offset.boneIndex}");
                    }

                    if (offset.translation == null || offset.translation.Length != 3)
                    {
                        errors.Add($"morph bone offset translation must have 3 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.translation))
                    {
                        errors.Add($"morph bone offset translation must contain only finite values: {morph.index}[{offsetIndex}]");
                    }

                    if (offset.orientation == null || offset.orientation.Length != 4)
                    {
                        errors.Add($"morph bone offset orientation must have 4 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.orientation))
                    {
                        errors.Add($"morph bone offset orientation must contain only finite values: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            // Sixth pass: validate flip morph offset fields
            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                if (morph.flipOffsets == null)
                {
                    errors.Add($"morph flipOffsets must not be null: {morph.index}");
                    continue;
                }

                for (int offsetIndex = 0; offsetIndex < morph.flipOffsets.Count; offsetIndex++)
                {
                    MmdFlipMorphOffsetDefinition offset = morph.flipOffsets[offsetIndex];
                    if (!morphIndices.Contains(offset.morphIndex))
                    {
                        errors.Add($"morph flip offset target morph index does not exist: {morph.index}[{offsetIndex}] -> {offset.morphIndex}");
                    }

                    if (offset.morphIndex == morph.index)
                    {
                        errors.Add($"morph flip offset self-reference is not allowed: {morph.index}[{offsetIndex}]");
                    }

                    if (float.IsNaN(offset.weight) || float.IsInfinity(offset.weight))
                    {
                        errors.Add($"morph flip offset weight must be finite: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            // Seventh pass: validate impulse morph offset fields
            var rigidbodyIndices = new HashSet<int>();
            foreach (MmdRigidbodyDefinition rb in model.physics.rigidbodies)
            {
                rigidbodyIndices.Add(rb.index);
            }

            for (int i = 0; i < morphs.Count; i++)
            {
                MmdMorphDefinition morph = morphs[i];
                if (morph.impulseOffsets == null)
                {
                    errors.Add($"morph impulseOffsets must not be null: {morph.index}");
                    continue;
                }

                for (int offsetIndex = 0; offsetIndex < morph.impulseOffsets.Count; offsetIndex++)
                {
                    MmdImpulseMorphOffsetDefinition offset = morph.impulseOffsets[offsetIndex];
                    if (!rigidbodyIndices.Contains(offset.rigidbodyIndex))
                    {
                        errors.Add($"morph impulse offset rigidbodyIndex does not reference an existing rigidbody: {morph.index}[{offsetIndex}] -> {offset.rigidbodyIndex}");
                    }

                    if (offset.velocity == null || offset.velocity.Length != 3)
                    {
                        errors.Add($"morph impulse offset velocity must have 3 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.velocity))
                    {
                        errors.Add($"morph impulse offset velocity must contain only finite values: {morph.index}[{offsetIndex}]");
                    }

                    if (offset.torque == null || offset.torque.Length != 3)
                    {
                        errors.Add($"morph impulse offset torque must have 3 values: {morph.index}[{offsetIndex}]");
                    }
                    else if (HasNonFinite(offset.torque))
                    {
                        errors.Add($"morph impulse offset torque must contain only finite values: {morph.index}[{offsetIndex}]");
                    }
                }
            }

            var materialIndices = new HashSet<int>(materials.Count);
            for (int i = 0; i < materials.Count; i++)
            {
                MmdMaterialDefinition material = materials[i];
                if (!materialIndices.Add(material.index))
                {
                    errors.Add($"duplicate material index: {material.index}");
                }

                if (material.vertexCount < 0)
                {
                    errors.Add($"material vertexCount must not be negative: {material.index}");
                }
                else if (material.vertexCount % 3 != 0)
                {
                    errors.Add($"material vertexCount must be zero or a multiple of 3: {material.index}");
                }

                if (string.IsNullOrWhiteSpace(material.name))
                {
                    errors.Add($"material name is required: {material.index}");
                }

                if (float.IsNaN(material.alpha) || float.IsInfinity(material.alpha) || material.alpha < 0.0f || material.alpha > 1.0f)
                {
                    errors.Add($"material alpha must be finite and between 0 and 1: {material.index}");
                }

                if (material.sphereTextureMode != null && material.sphereTextureMode.Length > 0 && string.IsNullOrWhiteSpace(material.sphereTextureMode))
                {
                    errors.Add($"material sphereTextureMode must be non-blank when present: {material.index}");
                }

                if (!IsKnownMaterialCullingPolicy(material.cullingPolicy))
                {
                    errors.Add($"material cullingPolicy must be unknown, double-sided, or backface-culling: {material.index}");
                }
            }

            if (indices.Count > 0 && materials.Count > 0)
            {
                int materialVertexCount = 0;
                for (int i = 0; i < materials.Count; i++)
                {
                    materialVertexCount += materials[i].vertexCount;
                }

                if (materialVertexCount != indices.Count)
                {
                    errors.Add($"material vertexCount sum must match model indices count: {materialVertexCount} != {indices.Count}");
                }
            }

            return errors;
        }

        public static void ThrowIfInvalid(MmdModelDefinition model)
        {
            IReadOnlyList<string> errors = ValidateStructuralModel(model);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("; ", errors));
            }
        }

        private static bool HasNonFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateRequiredVec3(List<string> errors, float[] values, string label)
        {
            if (values == null || values.Length != 3)
            {
                errors.Add($"{label} must have 3 values");
            }
            else if (HasNonFinite(values))
            {
                errors.Add($"{label} must contain only finite values");
            }
        }

        private static void ValidateRequiredVec4(List<string> errors, float[] values, string label)
        {
            if (values == null || values.Length != 4)
            {
                errors.Add($"{label} must have 4 values");
            }
            else if (HasNonFinite(values))
            {
                errors.Add($"{label} must contain only finite values");
            }
        }

        private static void ValidateAbsentOrEmpty(List<string> errors, float[] values, string message)
        {
            if (values != null && values.Length != 0)
            {
                errors.Add(message);
            }
        }

        private static string NormalizeSkinningMode(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
        }

        private static void ValidateOptionalAxis(List<string> errors, bool enabled, float[] values, string label)
        {
            if (!enabled)
            {
                return;
            }

            if (values == null || values.Length != 3)
            {
                errors.Add($"{label} must have 3 values when enabled");
            }
            else if (HasNonFinite(values))
            {
                errors.Add($"{label} must contain only finite values when enabled");
            }
        }

        private static bool IsKnownMaterialCullingPolicy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return string.Equals(value, "unknown", StringComparison.Ordinal) ||
                string.Equals(value, "double-sided", StringComparison.Ordinal) ||
                string.Equals(value, "backface-culling", StringComparison.Ordinal);
        }
    }
}
