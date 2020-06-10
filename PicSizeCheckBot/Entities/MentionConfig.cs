using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public class MentionConfig : IEntity<uint>
    {
        /// <summary>ID of the user<./summary>
        [BsonId]
        public uint ID { get; }

        public string MessageTemplate { get; set; }
        public bool IgnoreSelf { get; set; } = true;

        public ICollection<MentionPattern> Patterns { get; }

        [BsonConstructor(nameof(ID))]
        public MentionConfig(uint userID)
        {
            this.ID = userID;
            this.Patterns ??= new List<MentionPattern>();
        }
    }

    public class MentionPattern
    {
        [BsonElement("Pattern")]
        private string _pattern;

        public bool IgnoreCase { get; set; }

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
