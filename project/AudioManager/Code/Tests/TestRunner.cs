using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AudioManager
{
    internal static class TestRunner
    {
        private static bool IsStaticClass(Type t) => t.IsClass && t.IsAbstract && t.IsSealed && !t.IsNested;

        private static bool HasParameterlessPublicStaticMethod(Type t) =>
            t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Any(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(void));

        /// <summary>
        /// Every static class in this assembly whose name ends in "Tests". Discovery is automatic (ARCH-2):
        /// no hand-maintained list, so a new test class cannot be silently skipped.
        /// </summary>
        internal static List<Type> DiscoverTestClasses()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => IsStaticClass(t) && t.Name.EndsWith("Tests", StringComparison.Ordinal))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Static classes that look like tests (public static void methods with no parameters) but are not
        /// named *Tests, so discovery would skip them. Must be empty; TestRunnerTests enforces it.
        /// </summary>
        internal static List<Type> FindMisnamedTestClasses()
        {
            return Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => IsStaticClass(t) && !t.Name.EndsWith("Tests", StringComparison.Ordinal) && HasParameterlessPublicStaticMethod(t))
                .ToList();
        }

        internal static bool Run(out int passed, out int failed)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var sb = new StringBuilder();

            void Out(string line)
            {
                Console.WriteLine(line);
                sb.AppendLine(line);
            }

            Out("\n###### AudioManager Tests ######\n");

            var testTypes = DiscoverTestClasses();
            passed = 0; failed = 0;

            foreach (var type in testTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    var capture = new StringWriter();
                    var originalOut = Console.Out;
                    Console.SetOut(capture);
                    try
                    {
                        method.Invoke(null, null);
                        Console.SetOut(originalOut);
                        Out($"[PASS] {method.Name}");
                        passed++;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Console.SetOut(originalOut);
                        string innerOutput = capture.ToString().TrimEnd();
                        if (innerOutput.Length > 0)
                            Out(innerOutput);
                        Out($"[FAIL] {method.Name}: {tie.InnerException?.Message ?? tie.Message}");
                        failed++;
                    }
                    catch (Exception ex)
                    {
                        Console.SetOut(originalOut);
                        string innerOutput = capture.ToString().TrimEnd();
                        if (innerOutput.Length > 0)
                            Out(innerOutput);
                        Out($"[FAIL] {method.Name}: {ex.Message}");
                        failed++;
                    }
                }
            }

            Out($"\n-------------------------------");
            Out($"Results: {passed} passed, {failed} failed");
            Out($"-------------------------------\n");

            try
            {
                if (!Directory.Exists(Constants.LogsPath))
                    Directory.CreateDirectory(Constants.LogsPath);
                string logPath = Path.Combine(Constants.LogsPath, $"test-{timestamp}.log");
                File.WriteAllText(logPath, sb.ToString());
                Console.WriteLine($"  Log: {logPath}");
            }
            catch { /* log write failures must not break the test run */ }

            return failed == 0;
        }
    }
}
