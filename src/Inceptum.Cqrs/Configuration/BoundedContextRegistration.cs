using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Inceptum.Cqrs.InfrastructureCommands;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public interface IRegistration
    {
        void Create(CqrsEngine cqrsEngine);
        void Process(CqrsEngine cqrsEngine);
        IEnumerable<Type> Dependencies { get; }
    }

    class CommandSubscription
    {
        private readonly Dictionary<Type,CommandPriority> m_Types=new Dictionary<Type, CommandPriority>();
        public Dictionary<Type, CommandPriority> Types
        {
            get { return m_Types; }
        }
        public string Endpoint { get; set; } 
    }

    public class BoundedContextRegistration : IRegistration
    {
        readonly List<IBoundedContextDescriptor> m_Configurators = new List<IBoundedContextDescriptor>();
        readonly Dictionary<Tuple<Type, CommandPriority>, string> m_CommandRoutes = new Dictionary<Tuple<Type, CommandPriority>, string>();
        readonly Dictionary<Type, string> m_EventRoutes=new Dictionary<Type, string>();

        List<MessageRoute> m_MessageRoutes = new List<MessageRoute>();


        Type[] m_Dependencies=new Type[0];
        private readonly string m_Name;

        public IEnumerable<Type> Dependencies
        {
            get { return m_Dependencies; }
        }


        public string Name
        {
            get { return m_Name; }
        }

        protected int ThreadCount
        {
            get; set;
        }
        protected long FailedCommandRetryDelayInternal { get; set; }

        protected BoundedContextRegistration(string name)
        {
            ThreadCount = 4;
            FailedCommandRetryDelayInternal = 60000;
            m_Name = name;
            AddDescriptor(new SubscriptionDescriptor( m_MessageRoutes));
        }

        protected void AddDescriptor(IBoundedContextDescriptor descriptor)
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependencies()).Distinct().ToArray();
            m_Configurators.Add(descriptor);
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            var boundedContext=new BoundedContext(cqrsEngine,Name, ThreadCount,FailedCommandRetryDelayInternal);
            foreach (var descriptor in m_Configurators)
            {
                descriptor.Create(boundedContext, cqrsEngine.DependencyResolver);
            }
            
            cqrsEngine.BoundedContexts.Add(boundedContext);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            var boundedContext = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == Name);
            foreach (var descriptor in m_Configurators)
            {
                descriptor.Process(boundedContext, cqrsEngine);
            }

            boundedContext.CommandDispatcher.Wire(boundedContext.InfrastructureCommandsHandler);
        }

        internal void AddSubscribedEvents(IEnumerable<Type> types, string endpoint)
        {
            m_MessageRoutes.AddRange(types.Select(type =>
            {
                if (m_MessageRoutes.Any(r => r.Type == type && r.Direction == EndpointUsage.Subscribe && r.MessageType == MessageType.Command))
                    throw new ConfigurationErrorsException(
                        string.Format("Can not register {0} as event in bound context {1}, it is already registered as command", type, m_Name));

                return new MessageRoute
                {
                    Type = type,
                    Route = endpoint,
                    BoundedContext = Name,
                    MessageType = MessageType.Event,
                    Direction = EndpointUsage.Subscribe
                };
            }));

        }

        public void AddSubscribedCommands(IEnumerable<Type> types, string endpoint, CommandPriority priority)
        {
            //todo[kn]: process priority
            m_MessageRoutes.AddRange(types.Select(type =>
            {
                if (m_MessageRoutes.Any(r => r.Type == type && r.Direction == EndpointUsage.Subscribe && r.MessageType == MessageType.Event))
                    throw new ConfigurationErrorsException(
                        string.Format("Can not register {0} as command in bound context {1}, it is already registered as event", type, m_Name));
                return new MessageRoute
                {
                    Type = type,
                    Route = endpoint,
                    BoundedContext = Name,
                    MessageType = MessageType.Command,
                    Direction = EndpointUsage.Subscribe
                };
            }));

 
        }

        public void AddCommandsRoute(IEnumerable<Type> types, string endpoint, CommandPriority priority)
        {
            //todo[kn]: process priority
            m_MessageRoutes.AddRange(types.Select(e => new MessageRoute
            {
                Type = e,
                Route = endpoint,
                BoundedContext = Name,
                MessageType = MessageType.Command,
                Direction = EndpointUsage.Publish
            }));
 
        }
  
        
        public void AddEventsRoute(IEnumerable<Type> types, string endpoint)
        {
            m_MessageRoutes.AddRange(types.Select(e => new MessageRoute
            {
                Type = e,
                Route = endpoint,
                BoundedContext = Name,
                MessageType = MessageType.Event,
                Direction = EndpointUsage.Publish
            }));

      
        }


        protected void RegisterProjections(object projection, string fromBoundContext)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext));
        }  
        
        protected void RegisterProjections(Type projection, string fromBoundContext)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext));
        }
 
    }

}