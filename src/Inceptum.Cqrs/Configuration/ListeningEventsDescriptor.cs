using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public interface IListeningEventsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        IListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>> From(string boundedContext);
    }

    public class ListeningEventsDescriptor<TRegistration> 
        : ListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>,TRegistration>, IListeningEventsDescriptor<TRegistration>
        where TRegistration : IRegistration
    {
        private string m_BoundedContext;
        private readonly Type[] m_Types;

        public ListeningEventsDescriptor(TRegistration registration, Type[] types)
            : base(registration)
        {
            m_Types = types;
            Descriptor = this;
        }

        IListeningRouteDescriptor<ListeningEventsDescriptor<TRegistration>> IListeningEventsDescriptor<TRegistration>.From(string boundedContext)
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