using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
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

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
           
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var eventType in Types)
            {
                var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
                boundedContext.Routes[Route].AddPublishedEvent(eventType, 0, endpointResolver);
            }
        }
    }
}