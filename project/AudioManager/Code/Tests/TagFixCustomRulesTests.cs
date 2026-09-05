using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AudioManager
{
    /// <summary>
    /// Tests for the additive user-defined rule pass (config/tagfix-custom-rules.xml).
    /// Built-in TagFixer behaviour is covered by TagFixerTests - these only cover the new layer.
    /// </summary>
    internal static class TagFixCustomRulesTests
    {
        // ---- helpers ----

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid());
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>Writes a rules file containing the given raw &lt;Rule/&gt; elements.</summary>
        private static string WriteRulesFile(string directory, string ruleElements)
        {
            string path = Path.Combine(directory, "tagfix-custom-rules.xml");
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<TagFixCustomRules>\n" + ruleElements + "\n</TagFixCustomRules>\n");
            return path;
        }

        /// <summary>Runs an action with Console.Out captured, returning what it printed.</summary>
        private static string CaptureConsole(Action action)
        {
            var capture = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(capture);
            try { action(); }
            finally { Console.SetOut(original); }
            return capture.ToString();
        }

        // ---- Load: fail-safe behaviour ----

        public static void Load_MissingFile_ReturnsEmptyRuleSetWithoutThrowing()
        {
            string missing = Path.Combine(Path.GetTempPath(), "AudioManagerTest_" + Guid.NewGuid(), "no-such-rules.xml");
            var set = TagFixCustomRuleSet.Load(missing);
            Assert.True(set != null, "Load must return a rule set even when the file is missing");
            Assert.True(set.Rules.Count == 0, $"Missing rules file should yield 0 rules, got {set.Rules.Count}");
        }

        public static void Load_MalformedFile_SkipsRulesAndWarns()
        {
            string dir = NewTempDir();
            try
            {
                string path = Path.Combine(dir, "tagfix-custom-rules.xml");
                File.WriteAllText(path, "<TagFixCustomRules><Rule id=\"broken\" field=\"title\"  <<< not xml");

                TagFixCustomRuleSet set = null;
                string output = CaptureConsole(() => set = TagFixCustomRuleSet.Load(path));

                Assert.True(set != null, "Malformed rules file must not crash Load");
                Assert.True(set.Rules.Count == 0, $"Malformed rules file should yield 0 rules, got {set.Rules.Count}");
                Assert.True(output.IndexOf("[WARN]", StringComparison.Ordinal) >= 0,
                    "Malformed rules file should log a [WARN] line");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void Load_UnknownFieldOrAction_SkipsOnlyThatRule()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"bad-field\" field=\"comment\" match=\"contains\" value=\"x\" action=\"set-value\" replacement=\"y\" />\n" +
                    "<Rule id=\"good\" field=\"title\" match=\"contains\" value=\"x\" action=\"set-value\" replacement=\"y\" />");

                TagFixCustomRuleSet set = null;
                CaptureConsole(() => set = TagFixCustomRuleSet.Load(path));

                Assert.True(set.Rules.Count == 1, $"One invalid rule should be skipped, leaving 1 rule, got {set.Rules.Count}");
                Assert.Equal("good", set.Rules[0].Id, "The surviving rule should be the valid one");
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- Apply: matching and non-matching ----

        public static void Apply_MatchingRule_ChangesFieldAndRecordsChange()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"strip-demo\" field=\"title\" match=\"contains\" value=\"[DEMO]\" " +
                    "action=\"regex-replace\" pattern=\"\\s*\\[DEMO\\]\" replacement=\"\" />");
                var set = TagFixCustomRuleSet.Load(path);
                var values = new TagFieldValues { Title = "Song Title [DEMO]", Album = "Album", Artists = "Artist", Genre = "Rock" };

                List<TagFixCustomRuleChange> changes = set.Apply(values);

                Assert.Equal("Song Title", values.Title, "Matching rule should strip the [DEMO] marker from the title");
                Assert.True(changes.Count == 1, $"Exactly one change should be recorded, got {changes.Count}");
                Assert.Equal("strip-demo", changes[0].RuleId, "Change should carry the rule id");
                Assert.Equal("Album", values.Album, "Untargeted fields must be left alone");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void Apply_NonMatchingRule_IsNoOp()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"strip-demo\" field=\"title\" match=\"contains\" value=\"[DEMO]\" " +
                    "action=\"regex-replace\" pattern=\"\\s*\\[DEMO\\]\" replacement=\"\" />");
                var set = TagFixCustomRuleSet.Load(path);
                var values = new TagFieldValues { Title = "Plain Song Title", Album = "Album", Artists = "Artist", Genre = "Rock" };

                List<TagFixCustomRuleChange> changes = set.Apply(values);

                Assert.Equal("Plain Song Title", values.Title, "Non-matching rule must leave the title untouched");
                Assert.True(changes.Count == 0, $"Non-matching rule should record no changes, got {changes.Count}");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void Apply_SetValueAction_ReplacesWholeField()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"force-genre\" field=\"genre\" match=\"equals\" value=\"Hip Hop\" " +
                    "action=\"set-value\" replacement=\"Hip-Hop\" />");
                var set = TagFixCustomRuleSet.Load(path);
                var values = new TagFieldValues { Title = "T", Album = "A", Artists = "Ar", Genre = "Hip Hop" };

                var changes = set.Apply(values);

                Assert.Equal("Hip-Hop", values.Genre, "set-value should replace the whole field value");
                Assert.True(changes.Count == 1, $"set-value rule should record one change, got {changes.Count}");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void Apply_DisabledRule_IsSkipped()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"off\" field=\"title\" match=\"contains\" value=\"Song\" " +
                    "action=\"set-value\" replacement=\"Replaced\" enabled=\"false\" />");
                var set = TagFixCustomRuleSet.Load(path);
                var values = new TagFieldValues { Title = "Song Title", Album = "A", Artists = "Ar", Genre = "G" };

                var changes = set.Apply(values);

                Assert.Equal("Song Title", values.Title, "Disabled rule must not run");
                Assert.True(changes.Count == 0, $"Disabled rule should record no changes, got {changes.Count}");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void Apply_InvalidRegex_WarnsAndKeepsGoing()
        {
            string dir = NewTempDir();
            try
            {
                string path = WriteRulesFile(dir,
                    "<Rule id=\"bad-regex\" field=\"title\" match=\"regex\" value=\"([unclosed\" action=\"regex-replace\" replacement=\"\" />\n" +
                    "<Rule id=\"ok\" field=\"album\" match=\"contains\" value=\"Old\" action=\"set-value\" replacement=\"New\" />");
                var set = TagFixCustomRuleSet.Load(path);
                var values = new TagFieldValues { Title = "Song Title", Album = "Old Album", Artists = "Ar", Genre = "G" };

                List<TagFixCustomRuleChange> changes = null;
                string output = CaptureConsole(() => changes = set.Apply(values));

                Assert.True(output.IndexOf("[WARN]", StringComparison.Ordinal) >= 0, "A failing rule should log a [WARN] line");
                Assert.Equal("New", values.Album, "A failing rule must not stop later rules from applying");
                Assert.True(changes.Count == 1, $"Only the working rule should record a change, got {changes.Count}");
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- ProcessFile integration: custom changes tagged distinctly from built-in ones ----

        public static void ProcessFile_CustomRule_ChangeIsTaggedDistinctlyFromBuiltIn()
        {
            string dir = NewTempDir();
            try
            {
                string rulesPath = WriteRulesFile(dir,
                    "<Rule id=\"strip-demo\" field=\"title\" match=\"contains\" value=\"[DEMO]\" " +
                    "action=\"regex-replace\" pattern=\"\\s*\\[DEMO\\]\" replacement=\"\" />");
                string fixturePath = TagFixerTests.CreateSilentMp3Fixture(dir, "fixture.mp3",
                    "Song Title (feat. Featured Artist) [DEMO]", "Album Name", "Primary Artist");

                var fixer = TagFixer.ForTesting(dryRun: false, customRulesPath: rulesPath);
                var log = fixer.ProcessFile(fixturePath);

                var customLines = log.Changes.Where(c => c.IndexOf("[custom rule: strip-demo]", StringComparison.Ordinal) >= 0).ToList();
                Assert.True(customLines.Count == 1,
                    $"Exactly one change line should carry the custom-rule tag, got {customLines.Count}: {string.Join(" | ", log.Changes)}");

                var builtInTitleLines = log.Changes
                    .Where(c => c.StartsWith("Title:", StringComparison.Ordinal) &&
                                c.IndexOf("[custom rule:", StringComparison.Ordinal) < 0)
                    .ToList();
                Assert.True(builtInTitleLines.Count == 1,
                    "The built-in feat.-stripping change should be logged separately, without the custom-rule tag");

                string resultPath = Path.Combine(dir, log.Filename);
                TagLib.File resultFile = TagLib.File.Create(resultPath);
                Assert.Equal("Song Title", resultFile.Tag.Title,
                    "Custom rule should have stripped [DEMO] after the built-in feat. strip, and been written to disk");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void ProcessFile_CustomRuleAppliesWhenNoBuiltInFixNeeded()
        {
            string dir = NewTempDir();
            try
            {
                string rulesPath = WriteRulesFile(dir,
                    "<Rule id=\"rename-album\" field=\"album\" match=\"equals\" value=\"Old Album\" " +
                    "action=\"set-value\" replacement=\"New Album\" />");
                string fixturePath = TagFixerTests.CreateSilentMp3Fixture(dir, "Primary Artist - Clean Title.mp3",
                    "Clean Title", "Old Album", "Primary Artist");

                var fixer = TagFixer.ForTesting(dryRun: false, customRulesPath: rulesPath);
                fixer.ProcessFile(fixturePath); // first pass also sets TCMP
                TagLib.File resultFile = TagLib.File.Create(fixturePath);

                Assert.Equal("New Album", resultFile.Tag.Album,
                    "Custom rule should apply even when the built-in fixes found nothing to change");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void ProcessFile_MissingRulesFile_StillAppliesBuiltInFixes()
        {
            string dir = NewTempDir();
            try
            {
                string missingRules = Path.Combine(dir, "does-not-exist.xml");
                string fixturePath = TagFixerTests.CreateSilentMp3Fixture(dir, "fixture.mp3",
                    "Song Title (feat. Featured Artist)", "Album Name (Deluxe Edition)", "Primary Artist");

                var fixer = TagFixer.ForTesting(dryRun: false, customRulesPath: missingRules);
                var log = fixer.ProcessFile(fixturePath);

                Assert.Equal("fixed", log.Status, "A missing custom rules file must not stop the built-in fixes");
                Assert.True(!log.Changes.Any(c => c.IndexOf("[custom rule:", StringComparison.Ordinal) >= 0),
                    "No custom-rule change lines should appear when the rules file is missing");
                TagLib.File resultFile = TagLib.File.Create(Path.Combine(dir, log.Filename));
                Assert.Equal("Song Title", resultFile.Tag.Title, "Built-in feat. strip should still have run");
            }
            finally { Directory.Delete(dir, true); }
        }

        public static void ProcessFile_MalformedRulesFile_StillAppliesBuiltInFixes()
        {
            string dir = NewTempDir();
            try
            {
                string rulesPath = Path.Combine(dir, "tagfix-custom-rules.xml");
                File.WriteAllText(rulesPath, "<TagFixCustomRules><Rule this is not valid xml");
                string fixturePath = TagFixerTests.CreateSilentMp3Fixture(dir, "fixture.mp3",
                    "Song Title (feat. Featured Artist)", "Album Name (Deluxe Edition)", "Primary Artist");

                TagFixer.FixLog log = null;
                string output = CaptureConsole(() =>
                {
                    var fixer = TagFixer.ForTesting(dryRun: false, customRulesPath: rulesPath);
                    log = fixer.ProcessFile(fixturePath);
                });

                Assert.True(output.IndexOf("[WARN]", StringComparison.Ordinal) >= 0,
                    "A malformed rules file should log a [WARN] line");
                Assert.Equal("fixed", log.Status, "A malformed custom rules file must not stop the built-in fixes");
                TagLib.File resultFile = TagLib.File.Create(Path.Combine(dir, log.Filename));
                Assert.Equal("Album Name", resultFile.Tag.Album, "Built-in album suffix strip should still have run");
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- Shipped config file must stay loadable ----

        public static void RepoRulesFile_LoadsWithoutWarning()
        {
            if (!File.Exists(Constants.TagFixCustomRulesPath)) return; // exe run outside the repo
            TagFixCustomRuleSet set = null;
            string output = CaptureConsole(() => set = TagFixCustomRuleSet.Load(Constants.TagFixCustomRulesPath));
            Assert.True(output.IndexOf("[WARN]", StringComparison.Ordinal) < 0,
                $"config/tagfix-custom-rules.xml should load cleanly, got: {output.Trim()}");
            Assert.True(set != null, "Shipped rules file should produce a rule set");
        }
    }
}
