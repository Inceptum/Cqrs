using System;
using System.Collections.Generic;
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
        private readonly Context m_Context;

        public InfrastructureCommandsHandler(CqrsEngine cqrsEngine, Context context)
        {
            m_Context = context;
            m_CqrsEngine = cqrsEngine;
        }

        public void Handle(RoutedCommand<ReplayEventsCommand> routedCommand)
        {
            string serialization = routedCommand.Command.SerializationFormat;
            if (string.IsNullOrEmpty(serialization))
                serialization = routedCommand.OriginEndpoint.SerializationFormat;
            var endpoint = new Endpoint(routedCommand.OriginEndpoint.TransportId, routedCommand.Command.Destination, true, serialization);

            var eventsFrom = m_Context.EventStore.GetEventsFrom(routedCommand.Command.From, routedCommand.Command.AggregateId, routedCommand.Command.Types);
            var processingGroupName = m_Context.First(r=>r.Name==routedCommand.OriginRoute).ProcessingGroupName;
            long count = 0;
            var headers = new Dictionary<string, string>() { { "CommandId", routedCommand.Command.Id.ToString() } };
            foreach (var @event in eventsFrom)
            {
                m_CqrsEngine.PublishEvent(@event, endpoint, processingGroupName,headers);
                count++;
            }
            m_CqrsEngine.PublishEvent(new ReplayFinishedEvent()
            {
                CommandId = routedCommand.Command.Id,
                EventsCount = count,
                Id = Guid.NewGuid()
            }, endpoint, processingGroupName,headers);

        }
    }
}