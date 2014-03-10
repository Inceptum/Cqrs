using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningEventsDescriptor : ListeningRouteDescriptor<ListeningEventsDescriptor>
    {
        private string m_BoundedContext;
        private readonly Type[] m_Types;

        public ListeningEventsDescriptor(BoundedContextRegistration registration, Type[] types) : base(registration)
        {
            m_Types = types;
            Descriptor = this;
        }

        public IListeningRouteDescriptor<ListeningEventsDescriptor> From(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
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
            foreach (var type in m_Types)
            {
                var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
                boundedContext.Routes[Route].AddSubscribedEvent(type, 0,m_BoundedContext,endpointResolver);
            }
        }
    }
}