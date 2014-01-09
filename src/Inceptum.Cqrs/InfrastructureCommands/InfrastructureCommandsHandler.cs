using System;
using System.Linq;
using CommonDomain.Persistence;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.InfrastructureCommands
{
   

    class RoutedCommand<T>
    {
        public RoutedCommand(T command, Endpoint originEndpoint,string originRoute)
        {
            Command = command;
            OriginEndpoint = originEndpoint;
            OriginRoute = originRoute;
        }

        public T Command { get; set; }
        public Endpoint OriginEndpoint { get; set; }
        public string OriginRoute { get; set; }
    }
    internal class InfrastructureCommandsHandler
    {
        private readonly CqrsEngine m_CqrsEngine;
        private readonly BoundedContext m_BoundedContext;

        public InfrastructureCommandsHandler(CqrsEngine cqrsEngine, BoundedContext boundedContext)
        {
            m_BoundedContext = boundedContext;
            m_CqrsEngine = cqrsEngine;
        }

        public void Handle(RoutedCommand<ReplayEventsCommand> routedCommand)
        {
            string serialization = routedCommand.Command.SerializationFormat;
            if (string.IsNullOrEmpty(serialization))
                serialization = routedCommand.OriginEndpoint.SerializationFormat;
            var endpoint = new Endpoint(routedCommand.OriginEndpoint.TransportId, routedCommand.Command.Destination, true, serialization);

            var eventsFrom = m_BoundedContext.EventStore.GetEventsFrom(routedCommand.Command.From,routedCommand.Command.Types).ToArray();
            foreach (var @event in eventsFrom)
            {
                m_CqrsEngine.PublishEvent(@event,endpoint,routedCommand.OriginRoute);
            }
        }
    }
}