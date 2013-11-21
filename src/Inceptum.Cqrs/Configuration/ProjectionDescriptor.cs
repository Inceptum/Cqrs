using System;
using System.Configuration;
using System.Linq;

namespace Inceptum.Cqrs.Configuration
{
    internal class ProjectionDescriptor : DescriptorWithDependencies
    {
        private readonly string m_FromBoundContext;

        public ProjectionDescriptor(object projection, string fromBoundContext):base(projection)
        {
            m_FromBoundContext = fromBoundContext;
        }
        public ProjectionDescriptor(Type projection, string fromBoundContext):base(projection)
        {
            m_FromBoundContext = fromBoundContext;
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            var lestenedBoundContext = cqrsEngine.BoundedContexts.FirstOrDefault(bc => bc.Name == m_FromBoundContext);
            if (lestenedBoundContext == null)
                throw new ConfigurationErrorsException(string.Format("Bounded context '{0}' not registered",m_FromBoundContext));
            foreach (var projection in ResolvedDependencies)
            {
                //TODO: pass bounded context ReadModel
                lestenedBoundContext.EventDispatcher.Wire(projection);
            }
        }
    }
}