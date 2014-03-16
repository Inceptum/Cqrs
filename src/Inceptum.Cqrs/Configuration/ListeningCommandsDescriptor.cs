using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningCommandsDescriptor<TRegistration> : ListeningRouteDescriptor<ListeningCommandsDescriptor<TRegistration>, TRegistration>
        where TRegistration : IRegistration
    {
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

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
        }



        public ListeningCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this;
        }


        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);

            foreach (var type in Types)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        boundedContext.Routes[Route].AddSubscribedCommand(type, priority, endpointResolver);
                    }
                }
                else
                {
                    boundedContext.Routes[Route].AddSubscribedCommand(type, 0, endpointResolver);
                }
            }
        }
 

    }
}