using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TehGM.WolfBots
{
    public static class RegexExtensions
    {
        public static bool TryGetMatch(this Regex regex, string input, out Match match)
        {
            match = regex.Match(input);
            return match != null & match.Success;
        }
    }
}
