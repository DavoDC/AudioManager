using System;
using System.Reflection;

namespace AudioManager
{
    internal static class TestRunner
    {
        internal static bool Run()
        {
            Console.WriteLine("\n###### AudioManager Tests ######\n");

            var testTypes = new[] { typeof(TagFixerTests) };
            int passed = 0, failed = 0;

            foreach (var type in testTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    try
                    {
                        method.Invoke(null, null);
                        Console.WriteLine($"[PASS] {method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Console.WriteLine($"[FAIL] {method.Name}: {tie.InnerException?.Message ?? tie.Message}");
                        failed++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[FAIL] {method.Name}: {ex.Message}");
                        failed++;
                    }
                }
            }

            Console.WriteLine($"\n-------------------------------");
            Console.WriteLine($"Results: {passed} passed, {failed} failed");
            Console.WriteLine($"-------------------------------\n");

            return failed == 0;
        }
    }
}
