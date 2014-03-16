using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningCommandsDescriptor<TRegistration> : ListeningRouteDescriptor<ListeningCommandsDescriptor<TRegistration>, TRegistration>
        where TRegistration : IRegistration
    {
        private MapEndpointResolver m_EndpointResolver;
        public Type[] Types { get; private set; }

        public ListeningCommandsDescriptor(TRegistration registration, Type[] types)
            : base(registration)
        {
            Types = types;
            Descriptor = this;
        }


        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public ListeningCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;

            return this;
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
            m_EndpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors);

            foreach (var type in Types)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        boundedContext.Routes[Route].AddSubscribedCommand(type, priority, m_EndpointResolver);
                    }
                }
                else
                {
                    boundedContext.Routes[Route].AddSubscribedCommand(type, 0, m_EndpointResolver);
                }
            }
        }


        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            m_EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}