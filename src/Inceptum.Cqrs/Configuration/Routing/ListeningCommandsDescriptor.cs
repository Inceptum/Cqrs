using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration.Routing
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

        public ListeningCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;

            return this;
        }

        public override void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
          

            foreach (var type in Types)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        routeMap[Route].AddSubscribedCommand(type, priority, EndpointResolver);
                    }
                }
                else
                {
                    routeMap[Route].AddSubscribedCommand(type, 0, EndpointResolver);
                }
            }
        }


        public override void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
            EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}