#nullable enable

using System.Collections.Generic;

namespace Mmd.Motion
{
    public sealed class MmdIkSolveBreakdownAccumulator
    {
        public int ChainCount { get; set; }
        public int EnabledChainCount { get; set; }
        public int DisabledChainCount { get; set; }
        public int InvalidChainCount { get; set; }
        public int ZeroIterationChainCount { get; set; }
        public double SetupMs { get; set; }
        public double ChainSolveMs { get; set; }
        public double IterationMs { get; set; }
        public double CcdStepMs { get; set; }
        public double LimitClampMs { get; set; }
        public double WorldUpdateMs { get; set; }
        public double FullWorldRebuildMs { get; set; }
        public List<MmdIkChainBreakdownAccumulator> Chains { get; } = new();

        public MmdIkChainBreakdownAccumulator BeginChain(
            string chainName,
            int goalBoneIndex,
            int targetBoneIndex,
            int linkCount,
            int requestedIterationCount,
            int iterationLimit)
        {
            var chain = new MmdIkChainBreakdownAccumulator
            {
                chainName = chainName,
                goalBoneIndex = goalBoneIndex,
                targetBoneIndex = targetBoneIndex,
                linkCount = linkCount,
                requestedIterationCount = requestedIterationCount,
                iterationLimit = iterationLimit,
                earlyExitReason = iterationLimit == 0 ? "zero-iteration" : "limit-reached"
            };
            Chains.Add(chain);
            return chain;
        }
    }

    public sealed class MmdIkChainBreakdownAccumulator
    {
        public string chainName = string.Empty;
        public int goalBoneIndex;
        public int targetBoneIndex;
        public int linkCount;
        public int requestedIterationCount;
        public int iterationLimit;
        public int iterationsRun;
        public int linkVisitCount;
        public int ccdStepCount;
        public int linkAdjustmentCount;
        public int worldUpdateCount;
        public int fullWorldRebuildCount;
        public int limitClampCount;
        public int rollbackCount;
        public double chainSolveMs;
        public double iterationMs;
        public double ccdStepMs;
        public double limitClampMs;
        public double worldUpdateMs;
        public double fullWorldRebuildMs;
        public string earlyExitReason = string.Empty;
    }
}
