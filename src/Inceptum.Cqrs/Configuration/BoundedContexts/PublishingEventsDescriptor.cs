using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public class PublishingEventsDescriptor : PublishingRouteDescriptor<PublishingEventsDescriptor>
    {
        public Type[] Types { get; private set; }

        public PublishingEventsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
        {
            Types = types;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            foreach (var eventType in Types)
            {
                boundedContext.RouteMap[Route].AddRoute(new RoutingKey
                {
                    LocalBoundedContext = boundedContext.Name,
                    RemoteBoundContext = null,
                    MessageType = eventType,
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