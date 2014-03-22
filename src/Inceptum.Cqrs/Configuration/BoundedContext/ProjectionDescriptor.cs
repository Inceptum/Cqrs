using System;

namespace Inceptum.Cqrs.Configuration.BoundedContext
{
    internal class ProjectionDescriptor : DescriptorWithDependencies<Context>
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

        public override void Process(Context context, CqrsEngine cqrsEngine)
        {
            foreach (var projection in ResolvedDependencies)
            {
                //TODO: pass bounded context ReadModel
                context.EventDispatcher.Wire(m_FromBoundContext, projection);
            }
        }
    }



    
}