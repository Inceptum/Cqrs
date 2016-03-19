using System;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    internal class ProjectionDescriptor : DescriptorWithDependencies<Context>
    {
        private readonly string m_FromBoundContext;
        private readonly int m_BatchSize;
        private readonly int m_ApplyTimeoutInSeconds;

        public ProjectionDescriptor(object projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0)
            : base(projection)
        {
            m_ApplyTimeoutInSeconds = applyTimeoutInSeconds;
            m_BatchSize = batchSize;
            m_FromBoundContext = fromBoundContext;
        }

        public ProjectionDescriptor(Type projection, string fromBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0)
            : base(projection)
        {
            m_ApplyTimeoutInSeconds = applyTimeoutInSeconds;
            m_BatchSize = batchSize;
            m_FromBoundContext = fromBoundContext;
        }

        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            foreach (var projection in ResolvedDependencies)
            {
                //TODO: pass bounded context ReadModel
                context.EventDispatcher.Wire(m_FromBoundContext, projection, m_BatchSize, m_ApplyTimeoutInSeconds);
            }
        }
    }



    
}