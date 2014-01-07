using System;
using System.Collections.Generic;
using System.Linq;
using EventStore;
using EventStore.ClientAPI;
using NEventStore.Dispatcher;
using Inceptum.Cqrs.InfrastructureCommands;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public enum CommandPriority
    {
        Normal=0,
        Low=2,
        High=1
    }

    public class LocalBoundedContextRegistration : BoundedContextRegistration 
    {
        public LocalBoundedContextRegistration(string name)
            : base(name,true,name)
        {
        }
        public LocalBoundedContextRegistration ConcurrencyLevel(int threadCount)
        {
            if (threadCount < 1) throw new ArgumentException("threadCount should be greater then 0", "threadCount");
            ThreadCount = threadCount;
            return this;
        }
        public LocalBoundedContextRegistration FailedCommandRetryDelay(long delay)
        {
            if (delay < 0) throw new ArgumentException("threadCount should be greater or equal to 0", "delay");
            FailedCommandRetryDelayInternal = delay;
            return this;
        }
        public LocalBoundedContextRegistration WithCommandsHandler(object handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }
        public LocalBoundedContextRegistration WithCommandsHandler<T>()
        {
            AddDescriptor(new CommandsHandlerDescriptor(typeof(T)));
            return this;
        }  
        
        public LocalBoundedContextRegistration WithCommandsHandlers(params Type[] handlers)
        {
            AddDescriptor(new CommandsHandlerDescriptor(handlers));
            return this;
        }
      
        public LocalBoundedContextRegistration WithCommandsHandler(Type handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }

       public LocalBoundedContextRegistration WithProjection(object projection,string fromBoundContext)
       {
           RegisterProjections(projection, fromBoundContext);
           return this;
       }
 
        public LocalBoundedContextRegistration WithProjection(Type projection,string fromBoundContext)
       {
           RegisterProjections(projection, fromBoundContext);
           return this;
       }

        public LocalBoundedContextRegistration WithProjection<TListener>(string fromBoundContext)
       {
           RegisterProjections(typeof(TListener), fromBoundContext);
           return this;
       }

      

        public LocalListeningCommandsDescriptor ListeningCommands(params Type[] types)
        {
            return new LocalListeningCommandsDescriptor(types, this);
        }

        public LocalListeningCommandsDescriptor ListeningInfrastructureCommands()
        {
            return ListeningCommands(typeof(ReplayEventsCommand));
        }

        public LocalPublishingEventsDescriptor PublishingEvents(params Type[] types)
        {
            return new LocalPublishingEventsDescriptor(types, this);
        }

        public LocalBoundedContextRegistration WithProcess(object process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public LocalBoundedContextRegistration WithProcess(Type process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public LocalBoundedContextRegistration WithProcess<TProcess>()
            where TProcess : IProcess
        {
            return WithProcess(typeof(TProcess));
        }

        public LocalBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            AddDescriptor(new EventStoreDescriptor(configureEventStore));
            return this;
        }
        public LocalBoundedContextRegistration WithEventStore(IEventStoreConnection eventStoreConnection)
        {
            AddDescriptor(new GetEventStoreDescriptor(eventStoreConnection));
            return this;
        }
    }

    public class LocalListeningCommandsDescriptor
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;

        public LocalListeningCommandsDescriptor(Type[] types, LocalBoundedContextRegistration registration)
        {
            m_Types = types;
            m_Registration = registration;
        }

        public RoutedFromDescriptor On(string listenEndpoint,CommandPriority priority=CommandPriority.Normal)
        {
            if(listenEndpoint.Length==0)
                throw new ArgumentException("Endpoint list is empty","listenEndpoint");
            return new RoutedFromDescriptor(m_Registration, m_Types, listenEndpoint, priority);
        }

        
    }

    public class RoutedFromDescriptor
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly Dictionary<string,CommandPriority> m_ListenEndpoints=new Dictionary<string, CommandPriority>();

        public RoutedFromDescriptor(LocalBoundedContextRegistration registration, Type[] types, string listenEndpoint, CommandPriority priority)
        {
            m_ListenEndpoints[listenEndpoint]=priority;
            m_Types = types;
            m_Registration = registration;
        }
 
        public LocalBoundedContextRegistration RoutedFrom(string publishEndpoint,CommandPriority priority=CommandPriority.Normal)
        {
            m_Registration.AddCommandsRoute(m_Types, publishEndpoint, priority);
            foreach (var endpoint in m_ListenEndpoints)
            {
                m_Registration.AddSubscribedCommands(m_Types, endpoint.Key,endpoint.Value);
            }
            return m_Registration;
        }

        public LocalBoundedContextRegistration RoutedFromSameEndpoint( )
        {
            LocalBoundedContextRegistration registration=null;
            foreach (var endpoint in m_ListenEndpoints)
            {
                registration = RoutedFrom(endpoint.Key,endpoint.Value);
            }
            return registration;
        }

        public LocalBoundedContextRegistration NotRouted()
        {
            foreach (var endpoint in m_ListenEndpoints)
            {
                m_Registration.AddSubscribedCommands(m_Types, endpoint.Key,endpoint.Value);
            }
            return m_Registration;
        }

        public RoutedFromDescriptor On(string listenEndpoint, CommandPriority priority=CommandPriority.Normal)
        {
            if (string.IsNullOrEmpty(listenEndpoint))
                throw new ArgumentException("Endpoint is empty", "listenEndpoint");
            m_ListenEndpoints[listenEndpoint] = priority;
            return this;
        }

    }

    public class LocalPublishingEventsDescriptor
    {
        private readonly Type[] m_Types;
        private readonly LocalBoundedContextRegistration m_Registration;

        public LocalPublishingEventsDescriptor(Type[] types, LocalBoundedContextRegistration registration)
        {
            m_Registration = registration;
            m_Types = types;
        }

        public RoutedToDescriptor To(string publishEndpoint)
        {
            return new RoutedToDescriptor(m_Registration, m_Types, publishEndpoint);
        }
    }

    public class RoutedToDescriptor  
    {
        private readonly LocalBoundedContextRegistration m_Registration;
        private readonly Type[] m_Types;
        private readonly string m_PublishEndpoint;

        public RoutedToDescriptor(LocalBoundedContextRegistration registration, Type[] types, string publishEndpoint)
        {
            m_PublishEndpoint = publishEndpoint;
            m_Types = types;
            m_Registration = registration;
        }

        public LocalBoundedContextRegistration RoutedTo(string listenEndpoint)
        {
            m_Registration.AddEventsRoute(m_Types, m_PublishEndpoint);
            m_Registration.AddSubscribedEvents(m_Types, listenEndpoint);
            return m_Registration;
        }   
        
        public LocalBoundedContextRegistration RoutedToSameEndpoint()
        {
            return RoutedTo(m_PublishEndpoint);
        }  

        public LocalBoundedContextRegistration NotRouted()
        {
            m_Registration.AddEventsRoute(m_Types, m_PublishEndpoint);
            return m_Registration;
        }
    }
}