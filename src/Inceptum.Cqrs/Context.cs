using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public interface IRouteMap : IEnumerable<Route>
    {
        Route this[string name] { get; }
    }


    public class RouteMap : IRouteMap
    {
        private readonly Dictionary<string, Route> m_RouteMap = new Dictionary<string, Route>();
        public string Name { get; private set; }
        
        public RouteMap(string name)
        {
            Name = name;
        }

        public IEnumerator<Route> GetEnumerator()
        {
            return m_RouteMap.Values.Where(route => route.Type != null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        Route IRouteMap.this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("name should be not empty string", "name");
                Route route;
                if (m_RouteMap.TryGetValue(name, out route))
                    return route;

                route = new Route(name, Name);
                m_RouteMap.Add(name, route);
                return route;
            }
        }
        internal void ResolveRoutes(IEndpointProvider endpointProvider)
        {
            foreach (Route route in m_RouteMap.Values)
            {
                route.Resolve(endpointProvider);
            }
        }

        public bool PublishMessage(IMessagingEngine messagingEngine, Type type, object message, RouteType routeType, uint priority, string remoteBoundedContext = null)
        {
            var publishDirections = (from route in this
             from messageRoute in route.MessageRoutes
             where messageRoute.Key.MessageType == type &&
                   messageRoute.Key.RouteType == routeType &&
                   messageRoute.Key.Priority == priority &&
                   messageRoute.Key.RemoteBoundedContext == remoteBoundedContext
                    select new
                    {
                        processingGroup = route.ProcessingGroupName,
                        endpoint = messageRoute.Value
                    }).ToArray();
            if (!publishDirections.Any())
                return false;
            foreach (var direction in publishDirections)
            {
                messagingEngine.Send(message, direction.endpoint, direction.processingGroup);
            }
            return true;
        }

    }



    public class Context : RouteMap, IDisposable, ICommandSender
    {

   
        private readonly CqrsEngine m_CqrsEngine;
        private readonly Dictionary<string, Destination> m_TempDestinations = new Dictionary<string, Destination>();

        internal Context(CqrsEngine cqrsEngine, string name, long failedCommandRetryDelay) : base(name)
        {
            if(name.ToLower()=="default")
                throw new ArgumentException("default is reserved name","name");
            m_CqrsEngine = cqrsEngine;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(Name, failedCommandRetryDelay);
            EventDispatcher = new EventDispatcher(Name);
            Processes = new List<IProcess>();
        }

        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal IEventStoreAdapter EventStore { get; set; }
        internal long FailedCommandRetryDelay { get; set; }

        public IRouteMap Routes
        {
            get { return this; }
        }

      

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            m_CqrsEngine.SendCommand(command, Name, remoteBoundedContext, priority);
        }

        public void ReplayEvents(string remoteBoundedContext, DateTime @from, params Type[] types)
        {
            m_CqrsEngine.ReplayEvents(Name, remoteBoundedContext, @from, types);
        }

        public void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, params Type[] types)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            CommandDispatcher.Dispose();
        }

       

         internal bool GetTempDestination(string transportId, Func<Destination> generate, out Destination destination)
        {
            lock (m_TempDestinations)
            {
                if (!m_TempDestinations.TryGetValue(transportId, out destination))
                {
                    destination = generate();
                    m_TempDestinations[transportId] = destination;
                    return true;
                }
            }
            return false;
        }

    }
}