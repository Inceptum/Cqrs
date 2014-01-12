using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningCommandsDescriptor : ListeningRouteDescriptor<ListeningCommandsDescriptor>
    {
        public Type[] Types { get; private set; }

        public ListeningCommandsDescriptor(BoundedContextRegistration1 registration, Type[] types) : base(registration)
        {
            Types = types;
            Descriptor = this;
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
            foreach (var type in Types)
            {
                for (uint priority = 0; priority <= LowestPriority; priority++)
                {
                    var routingKey = new RoutingKey
                    {
                        LocalBoundedContext = boundedContext.Name,
                        RemoteBoundContext = null,
                        MessageType = type,
                        RouteType = RouteType.Commands,
                        Priority = priority
                    };
                    var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
                    boundedContext.RouteMap[Route].AddRoute(routingKey, endpointResolver);
                }
            }
        }
 

    }
}