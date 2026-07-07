#nullable enable

using System;
using UnityEngine;

namespace Mmd.Samples.RuntimeVerification
{
    [Serializable]
    public sealed class MmdRuntimeVerificationReport
    {
        public int schemaVersion = 1;
        public string unityVersion = Application.unityVersion;
        public string drive = string.Empty;
        public bool fastRuntimeRequested = true;
        public float requestedDurationSeconds;
        public float requestedFrameRate;
        public float physicsMaxSubStepFixedStepSeconds;
        public string startedAtUtc = string.Empty;
        public string finishedAtUtc = string.Empty;
        public float durationSeconds;
        public string status = "not-run";
        public int exitCode;
        public MmdRuntimeVerificationCaseResult[] caseResults = Array.Empty<MmdRuntimeVerificationCaseResult>();
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationCaseResult
    {
        public string name = string.Empty;
        public string pmxPath = string.Empty;
        public string vmdPath = string.Empty;
        public bool parseOnly;
        public string parseStatus = "not-run";
        public string playbackStatus = "not-run";
        public string status = "not-run";
        public float durationSeconds;
        public string requestedMaterialPreset = string.Empty;
        public string[] materialShaders = Array.Empty<string>();
        public MmdRuntimeVerificationModelSummary model = new();
        public MmdRuntimeVerificationMotionSummary motion = new();
        public MmdRuntimeVerificationPlaybackSummary playback = new();
        public MmdRuntimeVerificationPhysicsSummary physics = new();
        public MmdRuntimeVerificationSampledFrame[] sampledFrames =
            Array.Empty<MmdRuntimeVerificationSampledFrame>();
        public string[] expectedFeatures = Array.Empty<string>();
        public string exception = string.Empty;
        public int consoleErrorCount;
        public int consoleWarningCount;
        public string skipReason = string.Empty;
        public MmdRuntimeVerificationVisualSmoke visualSmoke = new();
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationVisualSmoke
    {
        public bool captured;
        public string screenshotPath = string.Empty;
        public int width;
        public int height;
        public bool isBlank;
        public bool isAllBlack;
        public bool isAllWhite;
        public float averageLuminance;
        public int uniqueColorCount;
        public string smokeStatus = "not-run";
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationModelSummary
    {
        public bool parsed;
        public string name = string.Empty;
        public int vertexCount;
        public int indexCount;
        public int boneCount;
        public int morphCount;
        public int materialCount;
        public int ikCount;
        public int rigidbodyCount;
        public int jointCount;
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationMotionSummary
    {
        public bool parsed;
        public string targetModelName = string.Empty;
        public int maxFrame;
        public int boneKeyframeCount;
        public int morphKeyframeCount;
        public int modelKeyframeCount;
        public int cameraKeyframeCount;
        public int lightKeyframeCount;
        public int selfShadowKeyframeCount;
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationPlaybackSummary
    {
        public bool configured;
        public bool fastRuntimeEnabled;
        public string fastRuntimeReason = string.Empty;
        public string driver = string.Empty;
        public int finalFrame;
        public float finalTimeSeconds;
        public string controllerSourceId = string.Empty;
        public string motionSourceId = string.Empty;
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationPhysicsSummary
    {
        public bool available;
        public int frame;
        public float deltaTime;
        public double totalMs;
        public int unsupportedWorldAnchorJointCount;
        public int bodyDiagnosticCount;
        public int pinnedBodyCount;
        public int staticPinnedBodyCount;
        public int dynamicOrientationPinnedBodyCount;
        public int dynamicInitialPinnedBodyCount;
        public float maxPinnedBodySyncDistance;
        public float maxPinnedBodyRotationAngle;
        public string comparisonSpace = string.Empty;
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationSampledFrame
    {
        public float timeSeconds;
        public int frame;
        public bool configured;
        public bool fastRuntimeEnabled;
        public bool physicsDiagnosticsAvailable;
        public MmdRuntimeVerificationBoneSample[]? bones;
        public string matrixSpace = "mmd-model";
        public string matrixLayout = "column-major";
        public float importScale;
    }

    [Serializable]
    public sealed class MmdRuntimeVerificationBoneSample
    {
        public int index;
        public string name = string.Empty;
        public float[] worldMatrix = Array.Empty<float>();
    }
}
