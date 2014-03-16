using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class PublishingEventsDescriptor<TRegistration> : PublishingRouteDescriptor<PublishingEventsDescriptor<TRegistration>, TRegistration> 
        where TRegistration : IRegistration
    {
        private MapEndpointResolver m_EndpointResolver;
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
            m_EndpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors);
            foreach (var eventType in Types)
            {
                boundedContext.Routes[Route].AddPublishedEvent(eventType, 0, m_EndpointResolver);
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            m_EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}