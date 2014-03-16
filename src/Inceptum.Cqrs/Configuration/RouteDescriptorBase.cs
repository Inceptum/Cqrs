using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration
{
    public abstract class RouteDescriptorBase<TRegistration> : RegistrationWrapper<TRegistration> where TRegistration : IRegistration
    {
        private readonly Dictionary<Func<RoutingKey, bool>, string> m_ExplicitEndpointSelectors = new Dictionary<Func<RoutingKey, bool>, string>();

        protected Dictionary<Func<RoutingKey, bool>, string> ExplicitEndpointSelectors
        {
            get { return m_ExplicitEndpointSelectors; }
        }

        protected uint LowestPriority { get; set; }

        protected RouteDescriptorBase(TRegistration registration)
            : base(registration)
        {
        }

        internal void AddExplicitEndpoint(Func<RoutingKey, bool> criteria, string endpoint)
        {
            ExplicitEndpointSelectors.Add(criteria, endpoint);
        }
    }

    public abstract class RouteDescriptorBase<TDescriptor, TRegistration> : RouteDescriptorBase<TRegistration> 
        where TDescriptor : RouteDescriptorBase<TRegistration> 
        where TRegistration : IRegistration
    {

        protected RouteDescriptorBase(TRegistration registration)
            : base(registration)
        {
        }


        public ExplicitEndpointDescriptor<TDescriptor, TRegistration> WithEndpoint(string endpoint)
        {
            return new ExplicitEndpointDescriptor<TDescriptor, TRegistration>(endpoint, this as TDescriptor);
        }
    }
}