using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using File = System.IO.File;

namespace AudioManager
{
    /// <summary>
    /// The four tag fields a custom rule can inspect and modify.
    /// Mirrors the values allowed in the field="" attribute of config/tagfix-custom-rules.xml.
    /// </summary>
    internal class TagFieldValues
    {
        public string Title = "";
        public string Album = "";
        public string Artists = "";
        public string Genre = "";

        public string Get(string field)
        {
            switch ((field ?? "").ToLowerInvariant())
            {
                case "title": return Title;
                case "album": return Album;
                case "artists": return Artists;
                case "genre": return Genre;
                default: return null;
            }
        }

        public void Set(string field, string value)
        {
            switch ((field ?? "").ToLowerInvariant())
            {
                case "title": Title = value; break;
                case "album": Album = value; break;
                case "artists": Artists = value; break;
                case "genre": Genre = value; break;
            }
        }
    }

    /// <summary>
    /// One user-defined tag cleanup rule loaded from config/tagfix-custom-rules.xml.
    /// </summary>
    internal class TagFixCustomRule
    {
        public string Id;
        public string Field;        // title | album | artists | genre
        public string Match;        // contains | equals | regex | startsWith | endsWith
        public string Value;        // the operand the match is tested against
        public string Action;       // regex-replace | set-value
        public string Pattern;      // regex-replace only; defaults to Value when omitted
        public string Replacement;  // regex-replace: replacement text. set-value: the new field value
        public bool Enabled = true;
    }

    /// <summary>
    /// A single change made by a custom rule, so callers can label it distinctly from built-in fixes.
    /// </summary>
    internal class TagFixCustomRuleChange
    {
        public string RuleId;
        public string Field;
        public string OldValue;
        public string NewValue;

        /// <summary>
        /// Change string in the same shape TagFixer uses for built-in fixes, suffixed with the
        /// custom-rule marker so a GUI layer can render user rules differently.
        /// </summary>
        public string Describe()
        {
            string label = char.ToUpperInvariant(Field[0]) + Field.Substring(1).ToLowerInvariant();
            return $"{label}: \"{OldValue}\"  -> \"{NewValue}\" {TagFixCustomRuleSet.ChangeMarkerPrefix}{RuleId}]";
        }
    }

    /// <summary>
    /// Loads and applies user-defined tag cleanup rules from config/tagfix-custom-rules.xml.
    ///
    /// These rules are ADDITIVE: TagFixer runs its built-in hardcoded transforms first and
    /// unchanged, then this rule set runs as a second pass over the already-cleaned values.
    ///
    /// Loading always fails safe - a missing or malformed file logs a warning and yields an
    /// empty rule set, so custom rules are simply skipped rather than breaking tag fixing.
    /// </summary>
    internal class TagFixCustomRuleSet
    {
        /// <summary>Prefix of the marker appended to every custom-rule change string.</summary>
        internal const string ChangeMarkerPrefix = "[custom rule: ";

        private static readonly string[] ValidFields = { "title", "album", "artists", "genre" };
        private static readonly string[] ValidMatches = { "contains", "equals", "regex", "startswith", "endswith" };
        private static readonly string[] ValidActions = { "regex-replace", "set-value" };

        public List<TagFixCustomRule> Rules { get; } = new List<TagFixCustomRule>();

        /// <summary>
        /// Loads rules from the given path (or Constants.TagFixCustomRulesPath by default).
        /// Never throws: a missing or malformed file yields an empty rule set plus a warning.
        /// </summary>
        internal static TagFixCustomRuleSet Load(string rulesPath = null)
        {
            string effectivePath = rulesPath ?? Constants.TagFixCustomRulesPath;
            var set = new TagFixCustomRuleSet();
            try
            {
                if (!File.Exists(effectivePath))
                {
                    // Absent file is the normal "no custom rules" state - note it quietly, don't warn loudly.
                    return set;
                }

                var doc = new XmlDocument();
                doc.Load(effectivePath);
                var nodes = doc.SelectNodes("//Rule");
                if (nodes == null) return set;

                foreach (XmlElement el in nodes.OfType<XmlElement>())
                {
                    var rule = ParseRule(el);
                    if (rule != null) set.Rules.Add(rule);
                }
            }
            catch (Exception ex)
            {
                // Malformed XML (or any read failure): skip custom rules entirely, never crash TagFixer.
                Console.WriteLine($"  [WARN] Could not load custom tag rules ({effectivePath}): {ex.Message}");
                set.Rules.Clear();
            }
            return set;
        }

        /// <summary>
        /// Validates one Rule element. Returns null (with a warning) if the rule is unusable,
        /// so one bad rule never disables the rest of the file.
        /// </summary>
        private static TagFixCustomRule ParseRule(XmlElement el)
        {
            string id = el.GetAttribute("id");
            string field = el.GetAttribute("field");
            string match = el.GetAttribute("match");
            string action = el.GetAttribute("action");

            if (string.IsNullOrWhiteSpace(id))
            {
                Console.WriteLine("  [WARN] Custom tag rule skipped: missing id attribute");
                return null;
            }
            if (!ValidFields.Contains((field ?? "").ToLowerInvariant()))
            {
                Console.WriteLine($"  [WARN] Custom tag rule '{id}' skipped: unknown field '{field}'");
                return null;
            }
            if (!ValidMatches.Contains((match ?? "").ToLowerInvariant()))
            {
                Console.WriteLine($"  [WARN] Custom tag rule '{id}' skipped: unknown match '{match}'");
                return null;
            }
            if (!ValidActions.Contains((action ?? "").ToLowerInvariant()))
            {
                Console.WriteLine($"  [WARN] Custom tag rule '{id}' skipped: unknown action '{action}'");
                return null;
            }

            bool enabled = true;
            string enabledAttr = el.GetAttribute("enabled");
            if (!string.IsNullOrEmpty(enabledAttr) && !bool.TryParse(enabledAttr, out enabled))
                enabled = true;

            return new TagFixCustomRule
            {
                Id = id,
                Field = field.ToLowerInvariant(),
                Match = match.ToLowerInvariant(),
                Value = el.GetAttribute("value"),
                Action = action.ToLowerInvariant(),
                Pattern = el.GetAttribute("pattern"),
                Replacement = el.GetAttribute("replacement"),
                Enabled = enabled
            };
        }

        /// <summary>
        /// Applies every enabled rule, in file order, to the given field values (mutated in place).
        /// Returns one change record per rule that actually altered a value; a rule that matches but
        /// produces an identical value records nothing.
        /// </summary>
        internal List<TagFixCustomRuleChange> Apply(TagFieldValues values)
        {
            var changes = new List<TagFixCustomRuleChange>();
            if (values == null) return changes;

            foreach (var rule in Rules)
            {
                if (!rule.Enabled) continue;
                try
                {
                    string current = values.Get(rule.Field) ?? "";
                    if (!Matches(rule, current)) continue;

                    string updated = ApplyAction(rule, current);
                    if (updated == null || updated == current) continue;

                    values.Set(rule.Field, updated);
                    changes.Add(new TagFixCustomRuleChange
                    {
                        RuleId = rule.Id,
                        Field = rule.Field,
                        OldValue = current,
                        NewValue = updated
                    });
                }
                catch (Exception ex)
                {
                    // A bad regex in one rule must not stop the other rules or the built-in fixes.
                    Console.WriteLine($"  [WARN] Custom tag rule '{rule.Id}' failed: {ex.Message}");
                }
            }
            return changes;
        }

        /// <summary>Case-insensitive match test for one rule against a field value.</summary>
        private static bool Matches(TagFixCustomRule rule, string current)
        {
            string operand = rule.Value ?? "";
            switch (rule.Match)
            {
                case "contains": return current.IndexOf(operand, StringComparison.OrdinalIgnoreCase) >= 0;
                case "equals": return current.Equals(operand, StringComparison.OrdinalIgnoreCase);
                case "startswith": return current.StartsWith(operand, StringComparison.OrdinalIgnoreCase);
                case "endswith": return current.EndsWith(operand, StringComparison.OrdinalIgnoreCase);
                case "regex": return Regex.IsMatch(current, operand, RegexOptions.IgnoreCase);
                default: return false;
            }
        }

        /// <summary>Produces the new field value for a rule that has already matched.</summary>
        private static string ApplyAction(TagFixCustomRule rule, string current)
        {
            switch (rule.Action)
            {
                case "regex-replace":
                    // pattern="" falls back to value="" so a regex-match rule needs only one expression.
                    string pattern = string.IsNullOrEmpty(rule.Pattern) ? rule.Value : rule.Pattern;
                    if (string.IsNullOrEmpty(pattern)) return null;
                    string replaced = Regex.Replace(current, pattern, rule.Replacement ?? "", RegexOptions.IgnoreCase);
                    // Collapse the double spaces a removal leaves behind, matching built-in fix behaviour.
                    return Regex.Replace(replaced, @"\s+", " ").Trim();

                case "set-value":
                    return rule.Replacement ?? "";

                default:
                    return null;
            }
        }
    }
}
