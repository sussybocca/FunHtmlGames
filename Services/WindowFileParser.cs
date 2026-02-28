using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FunHtmlGames.Services;

public class WindowFileParser
{
    // Parses custom .Window syntax into CSS key-value pairs.
    // Supported patterns:
    //   Window>CSS>STYLES)background(text)extra
    //   Org)Styles*value1*value2*key>value*...
    public Dictionary<string, string> Parse(string content)
    {
        var styles = new Dictionary<string, string>();
        var lines = content.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Pattern 1: Window>CSS>STYLES)background(text)extra
            var match1 = Regex.Match(line, @"^Window>CSS>STYLES\)(?<bg>[^(]+)\((?<text>[^)]+)\)(?<extra>.+)?$");
            if (match1.Success)
            {
                styles["background-color"] = match1.Groups["bg"].Value.Trim();
                styles["color"] = match1.Groups["text"].Value.Trim();
                continue;
            }

            // Pattern 2: Org)Styles*value1*value2*key>value*...
            var match2 = Regex.Match(line, @"^Org\)Styles\*(?<p1>[^*]+)\*(?<p2>[^*]+)\*(?<p3>[^>]+)>(?<p4>[^*]+)\*.*$");
            if (match2.Success)
            {
                styles["background"] = match2.Groups["p1"].Value.Trim();
                styles["border"] = match2.Groups["p2"].Value.Trim();
                styles[match2.Groups["p3"].Value.Trim()] = match2.Groups["p4"].Value.Trim();
                continue;
            }

            // Fallback: simple key=value lines (for extensibility)
            var eq = line.IndexOf('=');
            if (eq > 0)
            {
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                styles[key] = val;
            }
        }

        return styles;
    }
}