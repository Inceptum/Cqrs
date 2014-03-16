using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration
{
    public interface IPublishingCommandsDescriptor<TRegistration> where TRegistration : IRegistration
    {
        IPublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>> To(string boundedContext);
    }

    public class PublishingCommandsDescriptor<TRegistration>
        : PublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>, TRegistration>, IPublishingCommandsDescriptor<TRegistration> 
        where TRegistration : IRegistration
    {
        private string m_BoundedContext;
        private readonly Type[] m_CommandsTypes;

        public PublishingCommandsDescriptor(TRegistration registration, Type[] commandsTypes):base(registration)
        {
            m_CommandsTypes = commandsTypes;
            Descriptor = this;
        }

        public override IEnumerable<Type> GetDependencies()
        {
            return new Type[0];
        }

        public override void Create(BoundedContext boundedContext, IDependencyResolver resolver)
        {
             
        }


        public PublishingCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this;
        }


        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            var endpointResolver = new MapEndpointResolver(ExplicitEndpointSelectors, cqrsEngine.EndpointResolver);
            foreach (var type in m_CommandsTypes)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        boundedContext.Routes[Route].AddPublishedCommand(type, priority, m_BoundedContext, endpointResolver);
                    }
                }
                else
                {
                    boundedContext.Routes[Route].AddPublishedCommand(type, 0, m_BoundedContext, endpointResolver);
                    
                }
            }

        }

        IPublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>> IPublishingCommandsDescriptor<TRegistration>.To(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

       
    }
}