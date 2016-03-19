using System;
using Inceptum.Cqrs.Configuration.Routing;
using NEventStore;
using NEventStore.Dispatcher;
using NEventStore.Persistence.SqlPersistence;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    public class BoundedContextRegistration : ContextRegistrationBase<IBoundedContextRegistration>, IBoundedContextRegistration
    {
        public bool HasEventStore { get; set; }  
        public BoundedContextRegistration(string name):base(name)
        {
            FailedCommandRetryDelayInternal = 60000;
            AddDescriptor(new InfrastructureCommandsHandlerDescriptor());
        }

        public long FailedCommandRetryDelayInternal { get; set; }
        protected override Context CreateContext(CqrsEngine cqrsEngine)
        {
            return new Context(cqrsEngine, Name, FailedCommandRetryDelayInternal);
        }
 
       

        public IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(params Type[] types)
        {
            return AddDescriptor(new ListeningCommandsDescriptor<IBoundedContextRegistration>(this, types));
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(params Type[] types)
        {
            return AddDescriptor(new PublishingEventsDescriptor<IBoundedContextRegistration>(this, types));
        }
 

       public IBoundedContextRegistration WithEventStore<T>()
            where T:IEventStoreAdapter
        {
            HasEventStore = true;
            AddDescriptor(new EventStoreDescriptor<T>());
            return this;
        }

       public IBoundedContextRegistration WithEventStore(IEventStoreAdapter eventStoreAdapter)
        {
            HasEventStore = true;
            AddDescriptor(new EventStoreDescriptor(eventStoreAdapter));
            return this;
        }

       public IBoundedContextRegistration WithEventStore(Func<Context, IDependencyResolver, IEventStoreAdapter> eventStoreAdapterFactory)
        {
            HasEventStore = true;
            AddDescriptor(new EventStoreDescriptor(eventStoreAdapterFactory));
            return this;
        }

        [Obsolete]
        public IBoundedContextRegistration WithNEventStore(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            HasEventStore = true;
            AddDescriptor(new NEventStoreDescriptor((commits, factory) => configureEventStore(commits)));
            return this;
        }

        [Obsolete]
        public IBoundedContextRegistration WithNEventStore(Func<IDispatchCommits, IConnectionFactory, Wireup> configureEventStore)
        {
            HasEventStore = true;
            AddDescriptor(new NEventStoreDescriptor(configureEventStore));
            return this;
        }
         
        public IBoundedContextRegistration WithNEventStore(Func< Wireup> configureEventStore)
        {
            HasEventStore = true;
            AddDescriptor(new NEventStoreDescriptor((factory) => configureEventStore()));
            return this;
        }

        public IBoundedContextRegistration WithNEventStore(Func<IConnectionFactory, Wireup> configureEventStore)
        {
            HasEventStore = true;
            AddDescriptor(new NEventStoreDescriptor(configureEventStore));
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

     

        public IBoundedContextRegistration WithProjection<TProjection>(TProjection projection, string fromBoundContext
            , int batchSize = 0, int applyTimeoutInSeconds = 0
            , Action<TProjection> beforeBatchApply=null, Action<TProjection> afterBatchApply=null)
        {
            RegisterProjections(projection, fromBoundContext, batchSize, applyTimeoutInSeconds,beforeBatchApply,afterBatchApply);
            return this;
        }

        public IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Action<object> beforeBatchApply = null, Action<object> afterBatchApply = null)
        {
            RegisterProjections(projection, fromBoundContext,   batchSize = 0,   applyTimeoutInSeconds = 0);
            return this;
        }

        public IBoundedContextRegistration WithProjection<TProjection>(string fromBoundContext 
            , int batchSize = 0, int applyTimeoutInSeconds = 0
            , Action<TProjection> beforeBatchApply = null, Action<TProjection> afterBatchApply = null)
        {
            RegisterProjections(typeof(TProjection), fromBoundContext);
            return this;
        }

        protected void RegisterProjections<TProjection>(TProjection projection, string fromBoundContext, int batchSize, int applyTimeoutInSeconds, Action<TProjection> beforeBatchApply, Action<TProjection> afterBatchApply)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext, batchSize, applyTimeoutInSeconds, o => beforeBatchApply((TProjection)o), o => afterBatchApply((TProjection)o)));
        }

        protected void RegisterProjections(Type projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Action<object> beforeBatchApply = null, Action<object> afterBatchApply = null)
        {
            if (projection == null) throw new ArgumentNullException("projection");
            AddDescriptor(new ProjectionDescriptor(projection, fromBoundContext, batchSize, applyTimeoutInSeconds,beforeBatchApply,afterBatchApply));
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