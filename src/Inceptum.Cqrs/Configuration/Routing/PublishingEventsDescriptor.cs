using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public class PublishingEventsDescriptor<TRegistration> : PublishingRouteDescriptor<PublishingEventsDescriptor<TRegistration>, TRegistration> 
        where TRegistration : IRegistration
    {
        public Type[] Types { get; private set; }

        public PublishingEventsDescriptor(TRegistration registration, Type[] types)
            : base(registration)
        {
            Descriptor = this;
            Types = types;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
            foreach (var eventType in Types)
            {
                routeMap[Route].AddPublishedEvent(eventType, 0, EndpointResolver);
            }
        }

        public override void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
            EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}