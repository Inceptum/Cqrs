using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string boundedContext, string endpoint)
        {
            return new Endpoint("InMemory", boundedContext+"."+endpoint, true, "json");
        }
    }
}