using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TehGM.WolfBots.PicSizeCheckBot
{
    public class IdQueue : IEntity<Guid>
    {
        [BsonId]
        public Guid ID { get; }

        public string Name { get; set; }
        public uint? OwnerID { get; set; }
        public Queue<uint> QueuedIDs { get; set; }

        [BsonConstructor(nameof(ID))]
        private IdQueue(Guid id)
        {
            this.ID = id;
        }

        public IdQueue(string name) : this(Guid.NewGuid())
        {
            this.Name = name.Trim();
            this.QueuedIDs = new Queue<uint>();
        }

        public IdQueue(string name, uint ownerID) : this(name)
        {
            this.OwnerID = ownerID;
        }
    }
}
