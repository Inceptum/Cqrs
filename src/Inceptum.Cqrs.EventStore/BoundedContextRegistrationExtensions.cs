using CommonDomain.Persistence;
using EventStore.ClientAPI;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Configuration.BoundedContext;

namespace Inceptum.Cqrs.EventStore
{
    public static class BoundedContextRegistrationExtensions
    {

        public static IBoundedContextRegistration WithGetEventStore(this IRegistrationWrapper<IBoundedContextRegistration> wrapper, IEventStoreConnection eventStoreConnection)
        {
            return wrapper.Registration.WithGetEventStore(eventStoreConnection);
        }


        public static IBoundedContextRegistration WithGetEventStore(this IBoundedContextRegistration registration, IEventStoreConnection eventStoreConnection)
        {
            
            registration.WithEventStore((boundedContext, resolver) =>
            {
                var aggregateConstructor = resolver.HasService(typeof(IConstructAggregates))
                                       ? (IConstructAggregates)resolver.GetService(typeof(IConstructAggregates))
                                       : null;
                return new GetEventStoreAdapter(eventStoreConnection, boundedContext.EventsPublisher, aggregateConstructor);
            });
            return registration;
 
        }
 
    }
}