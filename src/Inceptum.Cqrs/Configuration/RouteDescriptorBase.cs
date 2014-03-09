using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class RouteDescriptorBase : BoundedContextRegistrationWrapper
    {
        private readonly Dictionary<Func<RoutingKey, bool>, string> m_ExplicitEndpointSelectors = new Dictionary<Func<RoutingKey, bool>, string>();

        protected Dictionary<Func<RoutingKey, bool>, string> ExplicitEndpointSelectors
        {
            get { return m_ExplicitEndpointSelectors; }
        }

        protected uint LowestPriority { get; set; }

        protected RouteDescriptorBase(BoundedContextRegistration registration)
            : base(registration)
        {
        }

        internal void AddExplicitEndpoint(Func<RoutingKey, bool> criteria, string endpoint)
        {
            ExplicitEndpointSelectors.Add(criteria, endpoint);
        }
    }

    public abstract class RouteDescriptorBase<T> : RouteDescriptorBase where T :  RouteDescriptorBase
    {

        protected RouteDescriptorBase(BoundedContextRegistration registration) : base(registration)
        {
        }

        public T Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this as T;
        }


        public ExplicitEndpointDescriptor<T> WithEndpoint(string endpoint)
        {
            return new ExplicitEndpointDescriptor<T>(endpoint, this as T);
        }
    }
}