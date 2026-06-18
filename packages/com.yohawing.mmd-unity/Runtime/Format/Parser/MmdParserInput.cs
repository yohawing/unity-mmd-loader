using System;

namespace Yohawing.MmdUnity.Parser
{
    public static class MmdParserInput
    {
        public static void RequireNonEmpty(ReadOnlySpan<byte> data, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name is required.", nameof(parameterName));
            }

            if (data.IsEmpty)
            {
                throw new ArgumentException("Input data must not be empty.", parameterName);
            }
        }
    }
}
