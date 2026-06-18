#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Yohawing.MmdUnity.Native
{
    internal static class MmdNativePhysicsMethods
    {
        internal const string LibraryName = "yohawing_mmd_unity_bullet";

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_get_version")]
        internal static extern IntPtr GetVersion();

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_get_last_error")]
        internal static extern IntPtr GetLastError();

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_create")]
        internal static extern int WorldCreate(out IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_destroy")]
        internal static extern void WorldDestroy(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_reset")]
        internal static extern int WorldReset(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_settle_to_current")]
        internal static extern int WorldSettleToCurrent(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_step")]
        internal static extern int WorldStep(IntPtr world, float deltaTime, int maxSubSteps);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_add_rigidbody", CharSet = CharSet.Ansi)]
        internal static extern int WorldAddRigidbody(
            IntPtr world,
            string shapeType,
            float sizeX,
            float sizeY,
            float sizeZ,
            float positionX,
            float positionY,
            float positionZ,
            float rotationX,
            float rotationY,
            float rotationZ,
            float mass,
            float linearDamping,
            float angularDamping,
            float friction,
            float restitution,
            int collisionGroup,
            int collisionMask,
            out int nativeIndex);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_add_point2point_joint")]
        internal static extern int WorldAddPoint2PointJoint(
            IntPtr world,
            int bodyAIndex,
            int bodyBIndex,
            float pivotX,
            float pivotY,
            float pivotZ,
            out int nativeIndex);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_add_6dof_spring_joint")]
        internal static extern int WorldAdd6DofSpringJoint(
            IntPtr world,
            int bodyAIndex,
            int bodyBIndex,
            [In] float[] positionXyz,
            [In] float[] rotationXyz,
            [In] float[] linearLowerXyz,
            [In] float[] linearUpperXyz,
            [In] float[] angularLowerXyz,
            [In] float[] angularUpperXyz,
            [In] float[] linearSpringXyz,
            [In] float[] angularSpringXyz,
            out int nativeIndex);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_rigidbody_count")]
        internal static extern int WorldGetRigidbodyCount(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_joint_count")]
        internal static extern int WorldGetJointCount(IntPtr world);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_6dof_spring_joint_descriptor")]
        internal static extern int WorldGet6DofSpringJointDescriptor(
            IntPtr world,
            int jointIndex,
            out int bodyAIndex,
            out int bodyBIndex,
            [In, Out] float[] outPositionXyz,
            [In, Out] float[] outRotationXyz,
            [In, Out] float[] outLinearLowerXyz,
            [In, Out] float[] outLinearUpperXyz,
            [In, Out] float[] outAngularLowerXyz,
            [In, Out] float[] outAngularUpperXyz,
            [In, Out] float[] outLinearSpringXyz,
            [In, Out] float[] outAngularSpringXyz,
            [In, Out] float[] outFrameAPositionXyz,
            [In, Out] float[] outFrameARotationXyzw,
            [In, Out] float[] outFrameBPositionXyz,
            [In, Out] float[] outFrameBRotationXyzw);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_rigidbody_shape_kind")]
        internal static extern int WorldGetRigidbodyShapeKind(IntPtr world, int bodyIndex);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_rigidbody_collision_filter")]
        internal static extern int WorldGetRigidbodyCollisionFilter(
            IntPtr world,
            int bodyIndex,
            out int collisionGroup,
            out int collisionMask);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_get_rigidbody_transform")]
        internal static extern int WorldGetRigidbodyTransform(
            IntPtr world,
            int bodyIndex,
            [In, Out] float[] outPositionXyz,
            [In, Out] float[] outRotationXyzw);

        [DllImport(LibraryName, EntryPoint = "ymu_bullet_world_set_rigidbody_transform")]
        internal static extern int WorldSetRigidbodyTransform(
            IntPtr world,
            int bodyIndex,
            [In] float[] positionXyz,
            [In] float[] rotationXyzw);
    }
}
