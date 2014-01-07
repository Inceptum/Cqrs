using System;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs
{
    public enum RouteType
    {
        Commands,
        Events
    }
    public interface IEndpointResolver
    {
        Endpoint Resolve(string localBoundedContext, string remoteBoundedContext, string endpoint, Type type, RouteType routeType);
    }
}