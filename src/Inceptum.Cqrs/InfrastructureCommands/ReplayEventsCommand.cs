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
        [ProtoMember(5)]
        public Guid Id { get; set; }
    }
    
    
    [ProtoContract]
    public class ReplayFinishedEvent
    {
        [ProtoMember(1)]
        public Guid Id { get; set; }
        [ProtoMember(2)]
        public Guid CommandId { get; set; }
        [ProtoMember(6)]
        public long EventsCount { get; set; }
    }
}