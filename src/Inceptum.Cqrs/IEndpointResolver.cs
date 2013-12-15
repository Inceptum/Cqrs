using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public interface IEndpointResolver
    {
        Endpoint Resolve(string boundedContext, string route, Type type, MessageType messageType);
    }

    public enum MessageType
    {
        Command,
        Event
    }
}