using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace AudioManager
{
    internal static class TestRunner
    {
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

            var testTypes = new[] { typeof(TagFixerTests), typeof(RoutingTests), typeof(ParseCacheTests), typeof(LibCheckerTests), typeof(TrackTests), typeof(StatListTests), typeof(TrackXMLTests), typeof(ParserTests), typeof(ReflectorTests), typeof(AgeCheckerTests), typeof(TeeWriterTests), typeof(AudioMirrorCommitterTests), typeof(LibCheckerExceptionTests), typeof(ManifestRunnerTests), typeof(IntegrationTests) };
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
