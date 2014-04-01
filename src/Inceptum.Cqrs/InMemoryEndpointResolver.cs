using System;
using Inceptum.Cqrs.Configuration;
using Inceptum.Cqrs.Routing;
using Inceptum.Messaging.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class InMemoryEndpointResolver:IEndpointResolver
    {
        
        public Endpoint Resolve(string route, RoutingKey key, IEndpointProvider endpointProvider)
        {
            if(key.Priority==0)
                return new Endpoint("InMemory",/*key.LocalBoundedContext + "." + */route, true, "json");
            return new Endpoint("InMemory",/*key.LocalBoundedContext + "." + */route+"."+key.Priority, true, "json");

        }
    }
}