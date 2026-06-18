#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using Yohawing.MmdUnity.Parser;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public static partial class MmdUnityModelFactory
    {
        private static MmdUnityPhysicsBody[] BuildPhysicsBodies(
            Transform root,
            IReadOnlyList<MmdBoneDefinition>? bones,
            Transform[] boneTransforms,
            MmdPhysicsDefinition? physics)
        {
            return BuildPhysicsBodies(root, bones, boneTransforms, physics, importScale: 1.0f);
        }

        private static MmdUnityPhysicsBody[] BuildPhysicsBodies(
            Transform root,
            IReadOnlyList<MmdBoneDefinition>? bones,
            Transform[] boneTransforms,
            MmdPhysicsDefinition? physics,
            float importScale)
        {
            if (physics == null || physics.rigidbodies == null || physics.rigidbodies.Count == 0)
            {
                return Array.Empty<MmdUnityPhysicsBody>();
            }

            float scale = NormalizeImportScale(importScale);
            Dictionary<int, MmdBoneDefinition> bonesByIndex = BuildBoneDefinitionMap(bones);
            Dictionary<int, Transform> boneTransformsByIndex = BuildBoneTransformMap(bones, boneTransforms);
            var result = new MmdUnityPhysicsBody[physics.rigidbodies.Count];
            for (int i = 0; i < physics.rigidbodies.Count; i++)
            {
                MmdRigidbodyDefinition body = physics.rigidbodies[i];
                Transform parent = root;
                Vector3 localPosition = ToUnityPosition(body.position, scale);
                if (boneTransformsByIndex.TryGetValue(body.boneIndex, out Transform boneTransform))
                {
                    parent = boneTransform;
                    Vector3 bodyPosition = ToVector3(body.position);
                    Vector3 boneOrigin = bonesByIndex.TryGetValue(body.boneIndex, out MmdBoneDefinition bone)
                        ? ToVector3(bone.origin)
                        : Vector3.zero;
                    localPosition = ToUnityPosition(bodyPosition - boneOrigin, scale);
                }

                var bodyObject = new GameObject(ResolvePhysicsBodyName(body, i));
                bodyObject.transform.SetParent(parent, worldPositionStays: false);
                bodyObject.transform.localPosition = localPosition;
                bodyObject.transform.localRotation = ToUnityModelRotation(ToMmdEulerRotation(body.rotation));
                bodyObject.transform.localScale = Vector3.one;

                Collider collider = AddCollider(bodyObject, body, scale);
                var rigidbody = bodyObject.AddComponent<Rigidbody>();
                rigidbody.mass = Math.Max(0.0001f, body.mass);
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = false;
                rigidbody.linearDamping = Math.Max(0.0f, body.linearDamping);
                rigidbody.angularDamping = Math.Max(0.0f, body.angularDamping);

                var metadata = bodyObject.AddComponent<MmdUnityPhysicsBody>();
                metadata.Initialize(
                    body.index,
                    body.name,
                    body.boneIndex,
                    body.boneName,
                    body.shapeType,
                    ToVector3(body.size),
                    ToVector3(body.position),
                    ToVector3(body.rotation),
                    body.physicsKind,
                    body.mass,
                    body.linearDamping,
                    body.angularDamping,
                    body.friction,
                    body.restitution,
                    body.group,
                    body.mask);
                collider.enabled = true;
                result[i] = metadata;
            }

            return result;
        }

        private static Collider AddCollider(GameObject bodyObject, MmdRigidbodyDefinition body)
        {
            return AddCollider(bodyObject, body, importScale: 1.0f);
        }

        private static Collider AddCollider(GameObject bodyObject, MmdRigidbodyDefinition body, float importScale)
        {
            float scale = NormalizeImportScale(importScale);
            string shapeType = body.shapeType ?? string.Empty;
            Vector3 size = ToVector3(body.size);
            if (string.Equals(shapeType, "sphere", StringComparison.Ordinal))
            {
                SphereCollider collider = bodyObject.AddComponent<SphereCollider>();
                collider.radius = Math.Max(0.0001f, size.x * scale);
                return collider;
            }

            if (string.Equals(shapeType, "box", StringComparison.Ordinal))
            {
                BoxCollider collider = bodyObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(
                    Math.Max(0.0001f, size.x * 2.0f * scale),
                    Math.Max(0.0001f, size.y * 2.0f * scale),
                    Math.Max(0.0001f, size.z * 2.0f * scale));
                return collider;
            }

            if (string.Equals(shapeType, "capsule", StringComparison.Ordinal))
            {
                CapsuleCollider collider = bodyObject.AddComponent<CapsuleCollider>();
                collider.radius = Math.Max(0.0001f, size.x * scale);
                collider.height = Math.Max(collider.radius * 2.0f, size.y * scale + collider.radius * 2.0f);
                collider.direction = 1;
                return collider;
            }

            throw new ArgumentException($"Unsupported MMD rigidbody shape type: {shapeType}");
        }

        private static string ResolvePhysicsBodyName(MmdRigidbodyDefinition body, int fallbackIndex)
        {
            string bodyName = string.IsNullOrWhiteSpace(body.name)
                ? $"rigidbody_{fallbackIndex}"
                : body.name;
            return $"MMD Rigidbody {body.index} {bodyName}";
        }
    }
}
