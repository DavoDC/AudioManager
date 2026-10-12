using System.Linq;

namespace AudioManager
{
    /// <summary>Guards for ARCH-2: test classes are discovered, never listed by hand, and none can hide.</summary>
    internal static class TestRunnerTests
    {
        public static void TestRunner_Discovery_FindsTheKnownClassesIncludingItself()
        {
            var names = TestRunner.DiscoverTestClasses().Select(t => t.Name).ToList();
            Assert.True(names.Contains("TestRunnerTests"), "discovery finds this class");
            Assert.True(names.Contains("LibCheckerTests") && names.Contains("RoutingTests") && names.Contains("TagFixerTests"),
                "discovery finds long-standing test classes");
            Assert.True(names.Count >= 20, "discovery finds the whole suite, not a handful");
        }

        public static void TestRunner_NoStaticClassWithTestShapedMethodsIsMisnamed()
        {
            // A static class with public static void parameterless methods that is not named *Tests would
            // be skipped by discovery. Rename it (or make its methods internal / give them parameters).
            var misnamed = TestRunner.FindMisnamedTestClasses().Select(t => t.Name).ToList();
            Assert.True(misnamed.Count == 0, "misnamed test classes would silently never run: " + string.Join(", ", misnamed));
        }
    }
}
