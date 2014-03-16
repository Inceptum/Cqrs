using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class ContextRegistrationBase<TRegistration> : IRegistration where TRegistration : class, IRegistration
    {
        public string Name { get; private set; }
        private readonly List<IBoundedContextDescriptor> m_Descriptors = new List<IBoundedContextDescriptor>();
        private Type[] m_Dependencies = new Type[0];

        protected ContextRegistrationBase(string name)
        {
            Name = name;
        }


        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            var context = CreateContext(cqrsEngine);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Create(context, cqrsEngine.DependencyResolver);
            }
            cqrsEngine.BoundedContexts.Add(context);

        }

        protected abstract BoundedContext CreateContext(CqrsEngine cqrsEngine);

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            var context = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == Name);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Process(context, cqrsEngine);
            }
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Dependencies; }
        }


        protected T AddDescriptor<T>(T descriptor) where T : IBoundedContextDescriptor
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependencies()).Distinct().ToArray();
            m_Descriptors.Add(descriptor);
            return descriptor;
        }


        public IListeningEventsDescriptor<TRegistration> ListeningEvents(params Type[] types)
        {
            return AddDescriptor(new ListeningEventsDescriptor<TRegistration>(this as TRegistration, types));
        }

        public IPublishingCommandsDescriptor<TRegistration> PublishingCommands(params Type[] commandsTypes)
        {
            return AddDescriptor(new PublishingCommandsDescriptor<TRegistration>(this as TRegistration, commandsTypes));
        }

        public ProcessingOptionsDescriptor<TRegistration> ProcessingOptions(string route)
        {
            return AddDescriptor(new ProcessingOptionsDescriptor<TRegistration>(this as TRegistration, route));
        }
    }

    internal class SagaDescriptor : DescriptorWithDependencies
    {
        public SagaDescriptor(object saga)
            : base(saga)
        {

        }
        public SagaDescriptor(Type saga)
            : base(saga)
        {
           
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            //Wire saga to handle events from all subscribed bounded contexts
            IEnumerable<string> listenedBoundedContexts = boundedContext
                                                                        .SelectMany(r=>r.RoutingKeys)
                                                                        .Where(k=>k.CommunicationType==CommunicationType.Subscribe && k.RouteType==RouteType.Events)
                                                                        .Select(k=>k.RemoteBoundedContext)
                                                                        .Distinct()
                                                                        .ToArray();
            foreach (var saga in ResolvedDependencies)
            {
                foreach (var listenedBoundedContext in listenedBoundedContexts)
                {
                    boundedContext.EventDispatcher.Wire(listenedBoundedContext, saga, new OptionalParameter<ICommandSender>(boundedContext));
                }
            }

        }
    }

    public class SagaRegistration : ContextRegistrationBase<ISagaRegistration>, ISagaRegistration
    {
        public SagaRegistration(string name, Type type) : base(name)
        {
            AddDescriptor(new SagaDescriptor(type));
        }

        protected override BoundedContext CreateContext(CqrsEngine cqrsEngine)
        {
            //TODO: introduce saga class
            return new BoundedContext(cqrsEngine, Name, 0);
        }

    }

    public class BoundedContextRegistration : ContextRegistrationBase<IBoundedContextRegistration>, IBoundedContextRegistration
    {
         
        public BoundedContextRegistration(string name):base(name)
        {
            
            AddDescriptor(new InfrastructureCommandsHandlerDescriptor());
        }

        public long FailedCommandRetryDelayInternal { get; set; }
        protected override BoundedContext CreateContext(CqrsEngine cqrsEngine)
        {
            return new BoundedContext(cqrsEngine, Name, FailedCommandRetryDelayInternal);
        }
 
       

        public IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] types)
        {
            return AddDescriptor(new ListeningCommandsDescriptor<IBoundedContextRegistration>(this, types));
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] types)
        {
            return AddDescriptor(new PublishingEventsDescriptor<IBoundedContextRegistration>(this, types));
        }
 

        public IBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            AddDescriptor(new EventStoreDescriptor(configureEventStore));
            return this;
        }
        public IBoundedContextRegistration WithEventStore(IEventStoreConnection eventStoreConnection)
        {
            AddDescriptor(new GetEventStoreDescriptor(eventStoreConnection));
            return this;
        }

        public IBoundedContextRegistration FailedCommandRetryDelay(long delay)
        {
            if (delay < 0) throw new ArgumentException("threadCount should be greater or equal to 0", "delay");
            FailedCommandRetryDelayInternal = delay;
            return this;
        }


        public IBoundedContextRegistration WithCommandsHandler(object handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }
        public IBoundedContextRegistration WithCommandsHandler<T>()
        {
            AddDescriptor(new CommandsHandlerDescriptor(typeof(T)));
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers)
        {
            AddDescriptor(new CommandsHandlerDescriptor(handlers));
            return this;
        }

        public IBoundedContextRegistration WithCommandsHandler(Type handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AddDescriptor(new CommandsHandlerDescriptor(handler));
            return this;
        }


         



        public IBoundedContextRegistration WithProjection(object projection, string fromBoundContext)
        {
            RegisterProjections(projection, fromBoundContext);
            return this;
        }

        public IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext)
        {
            RegisterProjections(projection, fromBoundContext);
            return this;
        }

        public IBoundedContextRegistration WithProjection<TListener>(string fromBoundContext)
        {
            RegisterProjections(typeof(TListener), fromBoundContext);
            return this;
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

        public IBoundedContextRegistration WithProcess(object process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public IBoundedContextRegistration WithProcess(Type process)
        {
            AddDescriptor(new LocalProcessDescriptor(process));
            return this;
        }

        public IBoundedContextRegistration WithProcess<TProcess>()
            where TProcess : IProcess
        {
            return WithProcess(typeof(TProcess));
        }

    }
}