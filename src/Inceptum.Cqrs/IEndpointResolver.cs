using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public interface IEndpointResolver
    {
        Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type);
    }
}