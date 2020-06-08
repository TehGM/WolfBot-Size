using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public class IdQueue : IEntity<string>
    {
        [BsonId]
        [BsonElement("_id")]
        public string Name { get; }
        [BsonIgnore]
        string IEntity<string>.ID => Name;

        public uint? OwnerID { get; set; }
        public Queue<uint> QueuedIDs { get; set; }

        [BsonConstructor(nameof(Name))]
        public IdQueue(string name)
        {
            this.Name = name;
            this.QueuedIDs = new Queue<uint>();
        }

        public IdQueue(string name, uint ownerID) : this(name)
        {
            this.OwnerID = ownerID;
        }
    }
}
