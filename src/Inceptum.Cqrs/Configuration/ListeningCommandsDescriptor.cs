using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public class ListeningCommandsDescriptor : ListeningRouteDescriptor<ListeningCommandsDescriptor>
    {
        public Type[] Types { get; private set; }

        public ListeningCommandsDescriptor(BoundedContextRegistration registration, Type[] types) : base(registration)
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

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var type in Types)
            {
                for (uint priority = 0; priority <= LowestPriority; priority++)
                {
                    var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
                    boundedContext.Routes[Route].AddSubscribedCommand(type, priority, endpointResolver);
                }
            }
        }
 

    }
}