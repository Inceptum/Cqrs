using System;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public class ExplicitEndpointDescriptor<T> where T : RouteDescriptorBase
    {
        private readonly string m_Endpoint;
        private readonly T m_Descriptor;

        public ExplicitEndpointDescriptor(string endpoint, T descriptor)
        {
            m_Descriptor = descriptor;
            m_Endpoint = endpoint;
        }

        public T For(Func<RoutingKey, bool> criteria)
        {
            m_Descriptor.AddExplicitEndpoint(criteria, m_Endpoint);
            return m_Descriptor;
        }
    }
}