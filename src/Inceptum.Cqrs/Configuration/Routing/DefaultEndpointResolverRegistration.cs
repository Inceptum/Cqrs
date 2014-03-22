using System;
using System.Collections.Generic;
using System.Linq;

namespace Inceptum.Cqrs.Configuration.Routing
{
    public class DefaultEndpointResolverRegistration : IRegistration, IHideObjectMembers
    {
        private IEndpointResolver m_Resolver;

        public DefaultEndpointResolverRegistration(IEndpointResolver resolver)
        {
            m_Resolver = resolver;
            Dependencies = new Type[0];
        }

        public DefaultEndpointResolverRegistration(Type resolverType)
        {
            Dependencies = new []{resolverType};
        }

        public void Create(CqrsEngine cqrsEngine)
        {
            if (m_Resolver == null)
            {
                m_Resolver = cqrsEngine.DependencyResolver.GetService(Dependencies.First()) as IEndpointResolver;
            }

            cqrsEngine.EndpointResolver = m_Resolver;
            
        }

        public void Process(CqrsEngine cqrsEngine)
        {
           
        }

        public IEnumerable<Type> Dependencies { get; private set; }
    }
}