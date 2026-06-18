#nullable enable

using UnityEngine;

namespace Yohawing.MmdUnity.UnityIntegration
{
    public sealed class MmdUnityPhysicsBody : MonoBehaviour
    {
        [SerializeField] private int bodyIndex = -1;
        [SerializeField] private string bodyName = string.Empty;
        [SerializeField] private int boneIndex = -1;
        [SerializeField] private string boneName = string.Empty;
        [SerializeField] private string shapeType = string.Empty;
        [SerializeField] private Vector3 descriptorSize;
        [SerializeField] private Vector3 descriptorPosition;
        [SerializeField] private Vector3 descriptorRotation;
        [SerializeField] private string physicsKind = string.Empty;
        [SerializeField] private float mass;
        [SerializeField] private float linearDamping;
        [SerializeField] private float angularDamping;
        [SerializeField] private float friction;
        [SerializeField] private float restitution;
        [SerializeField] private int collisionGroup;
        [SerializeField] private int collisionMask;
        [SerializeField] private bool hasNativeTransform;
        [SerializeField] private Vector3 nativePosition;
        [SerializeField] private Quaternion nativeRotation = Quaternion.identity;

        public int BodyIndex => bodyIndex;
        public string BodyName => bodyName;
        public int BoneIndex => boneIndex;
        public string BoneName => boneName;
        public string ShapeType => shapeType;
        public Vector3 DescriptorSize => descriptorSize;
        public Vector3 DescriptorPosition => descriptorPosition;
        public Vector3 DescriptorRotation => descriptorRotation;
        public string PhysicsKind => physicsKind;
        public float Mass => mass;
        public float LinearDamping => linearDamping;
        public float AngularDamping => angularDamping;
        public float Friction => friction;
        public float Restitution => restitution;
        public int CollisionGroup => collisionGroup;
        public int CollisionMask => collisionMask;
        public bool HasNativeTransform => hasNativeTransform;
        public Vector3 NativePosition => nativePosition;
        public Quaternion NativeRotation => nativeRotation;

        public void Initialize(
            int bodyIndex,
            string bodyName,
            int boneIndex,
            string boneName,
            string shapeType,
            Vector3 descriptorSize,
            Vector3 descriptorPosition,
            Vector3 descriptorRotation,
            string physicsKind,
            float mass,
            float linearDamping,
            float angularDamping,
            float friction,
            float restitution,
            int collisionGroup,
            int collisionMask)
        {
            this.bodyIndex = bodyIndex;
            this.bodyName = bodyName ?? string.Empty;
            this.boneIndex = boneIndex;
            this.boneName = boneName ?? string.Empty;
            this.shapeType = shapeType ?? string.Empty;
            this.descriptorSize = descriptorSize;
            this.descriptorPosition = descriptorPosition;
            this.descriptorRotation = descriptorRotation;
            this.physicsKind = physicsKind ?? string.Empty;
            this.mass = mass;
            this.linearDamping = linearDamping;
            this.angularDamping = angularDamping;
            this.friction = friction;
            this.restitution = restitution;
            this.collisionGroup = collisionGroup;
            this.collisionMask = collisionMask;
            this.hasNativeTransform = false;
            this.nativePosition = Vector3.zero;
            this.nativeRotation = Quaternion.identity;
        }

        public void RecordNativeTransform(float[] position, float[] rotation)
        {
            this.nativePosition = ToVector3(position);
            this.nativeRotation = ToQuaternion(rotation);
            this.hasNativeTransform = true;
        }

        private static Vector3 ToVector3(float[] values)
        {
            if (values == null || values.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(values[0], values[1], values[2]);
        }

        private static Quaternion ToQuaternion(float[] values)
        {
            if (values == null || values.Length < 4)
            {
                return Quaternion.identity;
            }

            return new Quaternion(values[0], values[1], values[2], values[3]);
        }
    }
}
