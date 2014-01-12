using System;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string route, Type type, RouteType routeType)
        {
            return new Endpoint("InMemory", localBoundedContext+"."+route, true, "json");
        }
    }
}