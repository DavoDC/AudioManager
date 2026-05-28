using System;

namespace AudioManager
{
    internal static class Assert
    {
        internal static void Equal(string expected, string actual, string label = "")
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new Exception(label.Length > 0
                    ? $"{label}: expected \"{expected}\" but got \"{actual}\""
                    : $"expected \"{expected}\" but got \"{actual}\"");
        }

        internal static void True(bool condition, string label = "")
        {
            if (!condition)
                throw new Exception(label.Length > 0 ? label : "Assertion failed");
        }
    }
}
