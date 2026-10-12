using System;

namespace AudioManager
{
    /// <summary>
    /// Entry point of AudioManager.Tests.exe, the C# test host (ARCH-2). The shipped AudioManager.exe
    /// contains no test code. Usage: --test | --routing-manifest PATH | --verify PATH.
    /// Exit code 0 when everything passed, 1 otherwise, 2 for bad arguments.
    /// </summary>
    internal static class TestHost
    {
        private static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (mode == "--test")
            {
                return TestRunner.Run(out _, out _) ? 0 : 1;
            }

            if (mode == "--routing-manifest" && args.Length >= 2)
            {
                return ManifestRunner.Run(args[1]) ? 0 : 1;
            }

            if (mode == "--verify" && args.Length >= 2)
            {
                // Unit tests first; the routing manifest only runs when they pass.
                if (!TestRunner.Run(out int unitPass, out int unitFail))
                {
                    Console.WriteLine("\n[VERIFY] FAILED (unit tests failed - manifest skipped)");
                    return 1;
                }
                ManifestRunner.Run(args[1], out int manPass, out int manFail);
                int totalPass = unitPass + manPass;
                int totalFail = unitFail + manFail;
                Console.WriteLine("\n============================================================");
                Console.WriteLine($"[VERIFY] {(totalFail == 0 ? "OK" : "FAILED")}  Total: {totalPass} passed, {totalFail} failed");
                Console.WriteLine("============================================================\n");
                return totalFail == 0 ? 0 : 1;
            }

            Console.WriteLine("AudioManager.Tests usage: --test | --routing-manifest <path> | --verify <path>");
            return 2;
        }
    }
}
