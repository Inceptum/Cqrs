using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public class BoundedContextRegistration : IBoundedContextRegistration
    {
        public string BoundedContextName { get; private set; }
        private readonly List<IBoundedContextDescriptor> m_Descriptors = new List<IBoundedContextDescriptor>();
        private Type[] m_Dependencies=new Type[0];

        public BoundedContextRegistration(string boundedContextName)
        {
            BoundedContextName = boundedContextName;
            AddDescriptor(new InfrastructureCommandsHandlerDescriptor());
        }

        public long FailedCommandRetryDelayInternal { get; set; }

        protected T AddDescriptor<T>(T descriptor) where T : IBoundedContextDescriptor
        {
            m_Dependencies = m_Dependencies.Concat(descriptor.GetDependencies()).Distinct().ToArray();
            m_Descriptors.Add(descriptor);
            return descriptor;
        }

        public PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes)
        {
            return AddDescriptor(new PublishingCommandsDescriptor(this, commandsTypes));
        }

        public ListeningEventsDescriptor ListeningEvents(params Type[] types)
        {
            return AddDescriptor(new ListeningEventsDescriptor(this, types));
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] types)
        {
            return AddDescriptor(new ListeningCommandsDescriptor(this, types));
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] types)
        {
            return AddDescriptor(new PublishingEventsDescriptor(this, types));
        }

        public ProcessingOptionsDescriptor ProcessingOptions(string route)
        {
            return AddDescriptor(new ProcessingOptionsDescriptor(this, route));
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


        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            var boundedContext = new BoundedContext(cqrsEngine, BoundedContextName,FailedCommandRetryDelayInternal);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Create(boundedContext, cqrsEngine.DependencyResolver);
            }
            cqrsEngine.BoundedContexts.Add(boundedContext);

        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            var boundedContext = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == BoundedContextName);
            foreach (var descriptor in m_Descriptors)
            {
                descriptor.Process(boundedContext, cqrsEngine);
            }
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




        public IBoundedContextRegistration WithSaga(object saga,params string[] listenedBoundContext)
        {
            registerSaga(saga, listenedBoundContext);
            return this;
        }

        public IBoundedContextRegistration WithSaga(Type saga,params string[] listenedBoundContext)
        {
            registerSaga(saga, listenedBoundContext);
            return this;
        }

        public IBoundedContextRegistration WithSaga<T>(params string[] listenedBoundContext)
        {
            registerSaga(typeof(T), listenedBoundContext);
            return this;
        }

        private void registerSaga(object saga, string[] listenedBoundContext)
        {
            if (saga == null) throw new ArgumentNullException("saga");
            AddDescriptor(new SagaDescriptor(saga, listenedBoundContext));
        }

        private void registerSaga(Type saga, string[] listenedBoundContext)
        {
            if (saga == null) throw new ArgumentNullException("saga");
            AddDescriptor(new SagaDescriptor(saga, listenedBoundContext));
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

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Dependencies; }
        }
    }
}