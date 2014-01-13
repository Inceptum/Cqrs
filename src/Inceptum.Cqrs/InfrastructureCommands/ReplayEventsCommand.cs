using System;
using ProtoBuf;

namespace Inceptum.Cqrs.InfrastructureCommands
{
    [ProtoContract]
    public class ReplayEventsCommand
    {
        [ProtoMember(1)]
        public string Destination { get; set; }
        [ProtoMember(2)]
        public string SerializationFormat { get; set; }
        [ProtoMember(3)]
        public Type[] Types { get; set; }
        [ProtoMember(4)]
        public DateTime From { get; set; } 
    }
}