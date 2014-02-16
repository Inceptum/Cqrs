using System;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type, RouteType routeType)
        {
            return new Endpoint("InMemory", localBoundedContext+"."+endpoint, true, "json");
        }
    }
}