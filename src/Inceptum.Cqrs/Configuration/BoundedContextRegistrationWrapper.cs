using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class BoundedContextRegistrationWrapper : IBoundedContextRegistration
    {
        private readonly IBoundedContextRegistration m_Registration;
        public long FailedCommandRetryDelayInternal
        {
            get { return m_Registration.FailedCommandRetryDelayInternal; }
            set { m_Registration.FailedCommandRetryDelayInternal = value; }
        }

        public IBoundedContextRegistration FailedCommandRetryDelay(long delay)
        {
            return m_Registration.FailedCommandRetryDelay(delay);
        }

        protected BoundedContextRegistrationWrapper(IBoundedContextRegistration registration)
        {
            m_Registration = registration;
        }
        public string BoundedContextName
        {
            get { return m_Registration.BoundedContextName; }
        }

        public PublishingCommandsDescriptor PublishingCommands(params Type[] commandsTypes)
        {
            return m_Registration.PublishingCommands(commandsTypes);
        }

        public ListeningEventsDescriptor ListeningEvents(params Type[] types)
        {
            return m_Registration.ListeningEvents(types);
        }

        public IListeningRouteDescriptor<ListeningCommandsDescriptor> ListeningCommands(params Type[] types)
        {
            return m_Registration.ListeningCommands(types);
        }

        public IPublishingRouteDescriptor<PublishingEventsDescriptor> PublishingEvents(params Type[] types)
        {
            return m_Registration.PublishingEvents(types);
        }

        public ProcessingOptionsDescriptor ProcessingOptions(string route)
        {
            return m_Registration.ProcessingOptions(route);
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Create(cqrsEngine);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            (m_Registration as IRegistration).Process(cqrsEngine);
        }

        public IBoundedContextRegistration WithCommandsHandler(object handler)
        {
            return m_Registration.WithCommandsHandler(handler);
        }

        public IBoundedContextRegistration WithCommandsHandler<T>()
        {
            return m_Registration.WithCommandsHandler<T>();
        }

        public IBoundedContextRegistration WithCommandsHandlers(params Type[] handlers)
        {
            return m_Registration.WithCommandsHandlers(handlers);
        }

        public IBoundedContextRegistration WithCommandsHandler(Type handler)
        {
            return m_Registration.WithCommandsHandler(handler);
        }

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return (m_Registration as IRegistration).Dependencies; }
        }


        public IBoundedContextRegistration WithEventStore(Func<IDispatchCommits, Wireup> configureEventStore)
        {
            return m_Registration.WithEventStore(configureEventStore);
        }

        public IBoundedContextRegistration WithEventStore(IEventStoreConnection eventStoreConnection)
        {
            return m_Registration.WithEventStore(eventStoreConnection);
        }

        public IBoundedContextRegistration WithProjection(object projection, string fromBoundContext)
        {
            return m_Registration.WithProjection(projection, fromBoundContext);
        }

        public IBoundedContextRegistration WithProjection(Type projection, string fromBoundContext)
        {
            return m_Registration.WithProjection(projection, fromBoundContext);
        }

        public IBoundedContextRegistration WithProjection<TListener>(string fromBoundContext)
        {
            return m_Registration.WithProjection<TListener>(fromBoundContext);
        }

        public IBoundedContextRegistration WithProcess(object process)
        {
            return m_Registration.WithProcess(process);
        }

        public IBoundedContextRegistration WithProcess(Type process)
        {
            return m_Registration.WithProcess(process);
        }

        public IBoundedContextRegistration WithProcess<TProcess>() where TProcess : IProcess
        {
            return m_Registration.WithProcess<TProcess>();
        }

        public IBoundedContextRegistration WithSaga(object saga, params string[] listenedBoundContext)
        {
            return m_Registration.WithSaga(saga, listenedBoundContext);
        }

        public IBoundedContextRegistration WithSaga(Type saga, params string[] listenedBoundContext)
        {
            return m_Registration.WithSaga(saga, listenedBoundContext);
        }

        public IBoundedContextRegistration WithSaga<T>(params string[] listenedBoundContext)
        {
            return m_Registration.WithSaga<T>(listenedBoundContext);
        }
    }
}