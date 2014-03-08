using System;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class InMemoryEndpointResolver:IEndpointResolver
    {
        
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            return new Endpoint("InMemory",key.LocalBoundedContext + "." + route, true, "json");
        }
    }
}