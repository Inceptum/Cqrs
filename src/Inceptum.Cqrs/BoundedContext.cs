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
    public interface IRouteMap:IEnumerable<Route>
    {
        Route this[string name] { get; }
       
    }

    
    public class BoundedContext : IDisposable, IRouteMap, ICommandSender
    {
        public static IBoundedContextRegistration Named(string name)
        {
            return new BoundedContextRegistration(name);
        }

        private readonly Dictionary<string,Route> m_RouteMap =new Dictionary<string, Route>();

        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal IEventStoreAdapter EventStore { get; set; }
        readonly Dictionary<string,Destination> m_TempDestinations=new Dictionary<string, Destination>();
        private CqrsEngine m_CqrsEngine;
        internal string Name { get; private set; }
        internal long FailedCommandRetryDelay { get; set; }

        internal BoundedContext(CqrsEngine cqrsEngine, string name, long failedCommandRetryDelay)
        {
            m_CqrsEngine = cqrsEngine;

            FailedCommandRetryDelay = failedCommandRetryDelay;
            Name = name;
            EventsPublisher = new EventsPublisher(cqrsEngine, this);
            CommandDispatcher = new CommandDispatcher(Name,  failedCommandRetryDelay);
            EventDispatcher = new EventDispatcher(Name);
            Processes = new List<IProcess>();
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

        public void Dispose()
        {
            CommandDispatcher.Dispose();
        }

        public IRouteMap Routes
        {
            get { return this;}
        }

        internal void ResolveRoutes(IEndpointProvider endpointProvider)
        {
            foreach (var route in m_RouteMap.Values)
            {
                route.Resolve(endpointProvider);
            }
        }
        Route IRouteMap.this[string name]
        {
            get
            {
                if (string.IsNullOrEmpty(name)) throw new ArgumentException("name should be not empty string","name");
                Route route;
                if (!m_RouteMap.TryGetValue(name, out route))
                {
                    route = new Route(name,Name);
                    m_RouteMap.Add(name, route);
                }
                return route;
            }
        }

        public IEnumerator<Route> GetEnumerator()
        {
            return m_RouteMap.Values.Where(route => route.Type != null).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void SendCommand<T>(T command, string remoteBoundedContext, uint priority = 0)
        {
            m_CqrsEngine.SendCommand(command,Name,remoteBoundedContext,priority);
        }

        public void ReplayEvents(string remoteBoundedContext, params Type[] types)
        {
            m_CqrsEngine.ReplayEvents(Name,remoteBoundedContext,types);
        }
    }
}