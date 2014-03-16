using System;
using System.Collections.Generic;
using EventStore.ClientAPI;
using NEventStore;
using NEventStore.Dispatcher;

namespace Inceptum.Cqrs.Configuration
{
    public interface IRegistrationWrapper<out T> : IRegistration
        where T : IRegistration
    {
        T Registration { get; }
    }
    public abstract class RegistrationWrapper<T> : IRegistrationWrapper<T>
        where T : IRegistration
    {
        private T m_Registration;
        T IRegistrationWrapper<T>.Registration { get { return m_Registration; } }

        protected RegistrationWrapper(T registration)
        {
            m_Registration = registration;
        }
   /*     public string BoundedContextName
        {
            get { return m_Registration.BoundedContextName; }
        }

        public long FailedCommandRetryDelayInternal
        {
            get { return m_Registration.FailedCommandRetryDelayInternal; }
            set { m_Registration.FailedCommandRetryDelayInternal = value; }
        }
*/

        IEnumerable<Type> IRegistration.Dependencies
        {
            get { return m_Registration.Dependencies; }
        }

        void IRegistration.Create(CqrsEngine cqrsEngine)
        {
            m_Registration.Create(cqrsEngine);
        }

        void IRegistration.Process(CqrsEngine cqrsEngine)
        {
            m_Registration.Process(cqrsEngine);
        }


    }



    public static class BoundedContextRegistrationWrapperExtensions
    {


        public static IPublishingCommandsDescriptor<IBoundedContextRegistration> PublishingCommands(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] commandsTypes)
        {
            return wrapper.Registration.PublishingCommands(commandsTypes);
        }

        public static IListeningEventsDescriptor<IBoundedContextRegistration> ListeningEvents(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.ListeningEvents(types);
        }

        public static IListeningRouteDescriptor<ListeningCommandsDescriptor<IBoundedContextRegistration>> ListeningCommands(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.ListeningCommands(types);
        }

        public static IPublishingRouteDescriptor<PublishingEventsDescriptor<IBoundedContextRegistration>> PublishingEvents(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] types)
        {
            return wrapper.Registration.PublishingEvents(types);
        }

        public static ProcessingOptionsDescriptor<IBoundedContextRegistration> ProcessingOptions(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, string route)
        {
            return wrapper.Registration.ProcessingOptions(route);
        }

        public static IBoundedContextRegistration WithCommandsHandler(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object handler)
        {
            return wrapper.Registration.WithCommandsHandler(handler);
        }

        public static IBoundedContextRegistration WithCommandsHandler<T>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper)
        {
            return wrapper.Registration.WithCommandsHandler<T>();
        }

        public static IBoundedContextRegistration WithCommandsHandlers(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params Type[] handlers)
        {
            return wrapper.Registration.WithCommandsHandlers(handlers);
        }

        public static IBoundedContextRegistration WithCommandsHandler(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type handler)
        {
            return wrapper.Registration.WithCommandsHandler(handler);
        }

        public static IBoundedContextRegistration WithEventStore(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Func<IDispatchCommits, Wireup> configureEventStore)
        {
            return wrapper.Registration.WithEventStore(configureEventStore);
        }

        public static IBoundedContextRegistration WithEventStore(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, IEventStoreConnection eventStoreConnection)
        {
            return wrapper.Registration.WithEventStore(eventStoreConnection);
        }

        public static IBoundedContextRegistration WithProjection(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object projection, string fromBoundContext)
        {
            return wrapper.Registration.WithProjection(projection, fromBoundContext);
        }

        public static IBoundedContextRegistration WithProjection(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type projection, string fromBoundContext)
        {
            return wrapper.Registration.WithProjection(projection, fromBoundContext);
        }

        public static IBoundedContextRegistration WithProjection<TListener>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, string fromBoundContext)
        {
            return wrapper.Registration.WithProjection<TListener>(fromBoundContext);
        }

        public static IBoundedContextRegistration WithProcess(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object process)
        {
            return wrapper.Registration.WithProcess(process);
        }

        public static IBoundedContextRegistration WithProcess(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type process)
        {
            return wrapper.Registration.WithProcess(process);
        }

        public static IBoundedContextRegistration WithProcess<TProcess>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper) where TProcess : IProcess
        {
            return wrapper.Registration.WithProcess<TProcess>();
        }

        public static IBoundedContextRegistration WithSaga(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, object saga, params string[] listenedBoundContext)
        {
            return wrapper.Registration.WithSaga(saga, listenedBoundContext);
        }

        public static IBoundedContextRegistration WithSaga(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, Type saga, params string[] listenedBoundContext)
        {
            return wrapper.Registration.WithSaga(saga, listenedBoundContext);
        }

        public static IBoundedContextRegistration WithSaga<T>(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, params string[] listenedBoundContext)
        {
            return wrapper.Registration.WithSaga<T>(listenedBoundContext);
        }
    }
}