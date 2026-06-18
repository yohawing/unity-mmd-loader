#nullable enable

using System;

namespace Yohawing.MmdUnity.Physics
{
    public sealed class MmdPhysicsBackendException : InvalidOperationException
    {
        public MmdPhysicsBackendException(
            string operation,
            string backend,
            string status,
            string backendMessage,
            string modelId = "",
            string motionId = "")
            : base(FormatMessage(operation, backend, status, backendMessage, modelId, motionId))
        {
            this.operation = operation;
            this.backend = backend;
            this.status = status;
            this.backendMessage = backendMessage;
            this.modelId = modelId;
            this.motionId = motionId;
        }

        public string operation { get; }
        public string backend { get; }
        public string status { get; }
        public string backendMessage { get; }
        public string modelId { get; }
        public string motionId { get; }

        private static string FormatMessage(
            string operation,
            string backend,
            string status,
            string backendMessage,
            string modelId,
            string motionId)
        {
            return
                $"Physics backend failure; operation={operation}; backend={backend}; status={status}; message={backendMessage}; model={modelId}; motion={motionId}";
        }
    }
}
