using MongoDB.Bson.Serialization.Attributes;
using System.Text.RegularExpressions;

namespace TehGM.WolfBots.PicSizeCheckBot.Mentions
{
    public class MentionPattern
    {
        [BsonElement("Pattern")]
        private string _pattern;

        public bool IgnoreCase { get; set; } = true;

        // lazy created Regex
        [BsonIgnore]
        private Regex _regex;
        [BsonIgnore]
        public Regex Regex
        {
            get
            {
                if (_regex == null)
                {
                    RegexOptions options = RegexOptions.CultureInvariant;
                    if (this.IgnoreCase)
                        options |= RegexOptions.IgnoreCase;
                    this._regex = new Regex(_pattern, options);
                }
                return _regex;
            }
        }

        [BsonConstructor]
        private MentionPattern() { }

        public MentionPattern(string pattern, bool ignoreCase = true)
        {
            this._pattern = pattern;
            this.IgnoreCase = ignoreCase;
        }

        public static implicit operator Regex(MentionPattern pattern)
            => pattern.Regex;
    }
}
