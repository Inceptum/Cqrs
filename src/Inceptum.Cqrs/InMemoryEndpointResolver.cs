using System;
using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type)
        {
            return new Endpoint("InMemory", localBoundedContext+"."+endpoint, true, "json");
        }
    }
}