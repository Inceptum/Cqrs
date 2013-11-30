using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public interface IEndpointResolver
    {
        Endpoint Resolve(string boundedContext, string endpoint);
    }
}