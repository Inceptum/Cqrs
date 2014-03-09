using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public interface IRouteMap:IEnumerable<Route>
    {
        Route this[string name] { get; }
       
    }

    public class BoundedContext : IDisposable, IRouteMap
    {
        public static IBoundedContextRegistration Named(string name)
        {
            return new BoundedContextRegistration1(name);
        }

        private readonly Dictionary<string,Route> m_RouteMap =new Dictionary<string, Route>();

        internal EventsPublisher EventsPublisher { get; private set; }
        internal CommandDispatcher CommandDispatcher { get; private set; }
        internal EventDispatcher EventDispatcher { get; private set; }
        internal List<IProcess> Processes { get; private set; }
        internal IEventStoreAdapter EventStore { get; set; }
        readonly Dictionary<string,Destination> m_TempDestinations=new Dictionary<string, Destination>();
        internal string Name { get; set; }
        internal int ThreadCount { get; set; }
        internal long FailedCommandRetryDelay { get; set; }
        internal bool IsLocal { get; private set; }
        internal string LocalBoundedContext { get; private set; }

        internal BoundedContext(CqrsEngine cqrsEngine, string name, int threadCount, long failedCommandRetryDelay, bool isLocal, string localBoundedContext)
        {
       
            ThreadCount = threadCount;
            FailedCommandRetryDelay = failedCommandRetryDelay;
            IsLocal = isLocal;
            LocalBoundedContext = localBoundedContext;
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

        internal Route[] ResolveRoutes(IEndpointProvider endpointProvider)
        {
            foreach (var route in m_RouteMap.Values)
            {
                route.Resolve(endpointProvider);
            }
            return m_RouteMap.Values.Where(route => route.Type!=null).ToArray();
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
    }
}