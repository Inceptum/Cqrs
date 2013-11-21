using Inceptum.Cqrs.Configuration;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    internal class InMemoryEndpointResolver:IEndpointResolver
    {
        public Endpoint Resolve(string endpoint)
        {
            return new Endpoint("InMemory", endpoint, true, "json");
        }
    }
}