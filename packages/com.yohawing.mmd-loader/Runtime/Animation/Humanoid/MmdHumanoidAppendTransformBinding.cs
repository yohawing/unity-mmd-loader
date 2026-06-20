#nullable enable

using System;
using UnityEngine;

namespace Mmd
{
    [Serializable]
    public sealed class MmdHumanoidAppendTransformBinding
    {
        [SerializeField] private Transform? targetTransform;
        [SerializeField] private int targetMmdBoneIndex = -1;
        [SerializeField] private Transform? appendParentTransform;
        [SerializeField] private int appendParentMmdBoneIndex = -1;
        [SerializeField] private float appendRatio;
        [SerializeField] private bool appendRotation;
        [SerializeField] private bool appendTranslation;
        [SerializeField] private bool appendLocal;
        [SerializeField] private Quaternion targetBindLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 targetBindLocalPosition;
        [SerializeField] private Quaternion appendParentBindLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 appendParentBindLocalPosition;
        [SerializeField] private int evaluationOrder;

        public MmdHumanoidAppendTransformBinding()
        {
        }

        public MmdHumanoidAppendTransformBinding(
            Transform? targetTransform,
            int targetMmdBoneIndex,
            Transform? appendParentTransform,
            int appendParentMmdBoneIndex,
            float appendRatio,
            bool appendRotation,
            bool appendTranslation,
            bool appendLocal,
            Quaternion targetBindLocalRotation,
            Vector3 targetBindLocalPosition,
            Quaternion appendParentBindLocalRotation,
            Vector3 appendParentBindLocalPosition,
            int evaluationOrder)
        {
            this.targetTransform = targetTransform;
            this.targetMmdBoneIndex = targetMmdBoneIndex;
            this.appendParentTransform = appendParentTransform;
            this.appendParentMmdBoneIndex = appendParentMmdBoneIndex;
            this.appendRatio = appendRatio;
            this.appendRotation = appendRotation;
            this.appendTranslation = appendTranslation;
            this.appendLocal = appendLocal;
            this.targetBindLocalRotation = targetBindLocalRotation;
            this.targetBindLocalPosition = targetBindLocalPosition;
            this.appendParentBindLocalRotation = appendParentBindLocalRotation;
            this.appendParentBindLocalPosition = appendParentBindLocalPosition;
            this.evaluationOrder = evaluationOrder;
        }

        public Transform? TargetTransform => targetTransform;

        public int TargetMmdBoneIndex => targetMmdBoneIndex;

        public Transform? AppendParentTransform => appendParentTransform;

        public int AppendParentMmdBoneIndex => appendParentMmdBoneIndex;

        public float AppendRatio => appendRatio;

        public bool AppendRotation => appendRotation;

        public bool AppendTranslation => appendTranslation;

        public bool AppendLocal => appendLocal;

        public Quaternion TargetBindLocalRotation => targetBindLocalRotation;

        public Vector3 TargetBindLocalPosition => targetBindLocalPosition;

        public Quaternion AppendParentBindLocalRotation => appendParentBindLocalRotation;

        public Vector3 AppendParentBindLocalPosition => appendParentBindLocalPosition;

        public int EvaluationOrder => evaluationOrder;
    }
}
