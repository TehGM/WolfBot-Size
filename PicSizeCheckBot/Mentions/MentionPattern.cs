using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using TehGM.WolfBots.PicSizeCheckBot.Mentions.Filters;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionPattern
    {
        [BsonElement("Pattern"), BsonRequired]
        public string Pattern { get; set; }
        [BsonElement("IgnoreCase")]
        public bool IgnoreCase { get; set; } = true;
        [BsonElement("PatternType")]
        public MentionPatternType PatternType { get; set; } = MentionPatternType.Contains;
        [BsonElement("Filters"), BsonIgnoreIfNull]
        public ICollection<IMentionFilter> Filters { get; set; }

        // lazy created Regex
        [BsonIgnore]
        private Regex _regex;

        [BsonConstructor]
        private MentionPattern() { }

        public MentionPattern(string pattern, MentionPatternType patternType, bool ignoreCase = true)
        {
            this.Pattern = pattern;
            this.PatternType = patternType;
            this.IgnoreCase = ignoreCase;
        }


        public bool IsMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (this.PatternType == MentionPatternType.Contains)
                return text.Contains(this.Pattern, this.IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture);

            if (this.PatternType == MentionPatternType.Regex)
                return this.GetRegex().IsMatch(text);

            return false;
        }

        public Regex GetRegex()
        {
            if (this._regex == null)
            {
                RegexOptions options = RegexOptions.CultureInvariant;
                if (this.IgnoreCase)
                    options |= RegexOptions.IgnoreCase;
                this._regex = new Regex(this.Pattern, options);
            }
            return this._regex;
        }
    }

    public enum MentionPatternType
    {
        Contains = 0,
        Regex = 1
    }
}
