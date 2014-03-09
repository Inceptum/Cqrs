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
            foreach (var projection in ResolvedDependencies)
            {
                //TODO: pass bounded context ReadModel
                boundedContext.EventDispatcher.Wire(m_FromBoundContext, projection);
            }
        }
    }



    internal class SagaDescriptor : DescriptorWithDependencies
    {
        private readonly string[] m_ListenedBoundContext;

        public SagaDescriptor(object saga, params string[] listenedBoundContext):base(saga)
        {
            m_ListenedBoundContext = listenedBoundContext;
        }
        public SagaDescriptor(Type saga, params string[] listenedBoundContext)
            : base(saga)
        {
            m_ListenedBoundContext = listenedBoundContext;
        }

        public override void Process(BoundedContext boundedContext, CqrsEngine cqrsEngine)
        {
            foreach (var saga in ResolvedDependencies)
            {
                foreach (var listenedBoundedContext in m_ListenedBoundContext)
                {
                    boundedContext.EventDispatcher.Wire(listenedBoundedContext, saga, new OptionalParameter<ICommandSender>(cqrsEngine));    
                }
                
            }
 
        }
    }
}