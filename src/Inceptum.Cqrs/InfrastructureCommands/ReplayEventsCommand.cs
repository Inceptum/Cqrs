using System;

namespace Inceptum.Cqrs.InfrastructureCommands
{
    public class ReplayEventsCommand
    {
        public string Destination { get; set; } 
        public string SerializationFormat { get; set; } 
        public Type[] Types { get; set; } 
        public DateTime From { get; set; } 
    }
}