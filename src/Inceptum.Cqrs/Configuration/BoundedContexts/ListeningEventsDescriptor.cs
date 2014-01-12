using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningEventsDescriptor : ListeningRouteDescriptor<ListeningEventsDescriptor>
    {
        private string m_BoundedContext;
        private readonly Type[] m_Types;

        public ListeningEventsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
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
            foreach (var type in m_Types)
            {
                boundedContext.RouteMap[Route].AddRoute(new RoutingKey
                {
                    LocalBoundedContext = boundedContext.Name,
                    RemoteBoundContext = m_BoundedContext,
                    MessageType = type,
                    RouteType = RouteType.Events,
                    Priority = 0
                }, (IEndpointResolver) resolver.GetService(typeof (IEndpointResolver)));
            }
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
        }
    }
}