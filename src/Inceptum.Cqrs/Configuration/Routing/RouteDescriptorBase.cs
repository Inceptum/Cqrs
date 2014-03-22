using System;
using System.Collections.Generic;
using Inceptum.Cqrs.Routing;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public abstract class RouteDescriptorBase<TRegistration> : RegistrationWrapper<TRegistration> where TRegistration : IRegistration
    {
        private readonly Dictionary<Func<RoutingKey, bool>, string> m_ExplicitEndpointSelectors = new Dictionary<Func<RoutingKey, bool>, string>();
        protected IEndpointResolver FallbackResolverResolver { get; private set; }
        protected MapEndpointResolver EndpointResolver { get; private set; }
 

        protected uint LowestPriority { get; set; }

        protected RouteDescriptorBase(TRegistration registration)
            : base(registration)
        {
            EndpointResolver=new MapEndpointResolver();
        }

        internal void AddExplicitEndpoint(Func<RoutingKey, bool> criteria, string endpoint)
        {
            EndpointResolver.AddSelector(criteria, endpoint);
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

        public TDescriptor WithEndpointResolver(IEndpointResolver resolver)
        {
            EndpointResolver.SetFallbackResolver(resolver,true);
            return this as TDescriptor;
        }

        
    }

   
   
}