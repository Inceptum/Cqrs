using System;
using System.Collections.Generic;

namespace Inceptum.Cqrs.Configuration.Routing
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

        public PublishingCommandsDescriptor<TRegistration> Prioritized(uint lowestPriority)
        {
            LowestPriority = lowestPriority;
            return this;
        }

        IPublishingRouteDescriptor<PublishingCommandsDescriptor<TRegistration>> IPublishingCommandsDescriptor<TRegistration>.To(string boundedContext)
        {
            m_BoundedContext = boundedContext;
            return this;
        }

        public override void Create(IRouteMap routeMap, IDependencyResolver resolver)
        {
                 foreach (var type in m_CommandsTypes)
            {
                if (LowestPriority > 0)
                {
                    for (uint priority = 1; priority <= LowestPriority; priority++)
                    {
                        routeMap[Route].AddPublishedCommand(type, priority, m_BoundedContext, EndpointResolver);
                    }
                }
                else
                {
                    routeMap[Route].AddPublishedCommand(type, 0, m_BoundedContext, EndpointResolver);

                }
            }
        }


        public override void Process(IRouteMap routeMap, CqrsEngine cqrsEngine)
        {
            EndpointResolver.SetFallbackResolver(cqrsEngine.EndpointResolver);
        }
    }
}