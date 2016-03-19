// ReSharper disable CheckNamespace

using System;

namespace Castle.MicroKernel.Registration
// ReSharper restore CheckNamespace
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsProjection<T>(this ComponentRegistration<T> registration, string hostingBoundContext,
            string projectedBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0, Action<T> beforeBatchApply = null, Action<T> afterBatchApply = null) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    IsProjection = true,
                    ProjectedBoundContext = projectedBoundContext,
                    BoundContext = hostingBoundContext,
                    BatchSize = batchSize,
                    ApplyTimeoutInSeconds = applyTimeoutInSeconds,
                    BeforeBatchApply = beforeBatchApply,
                    AfterBatchApply = afterBatchApply

                });
        }  
        
        public static ComponentRegistration<T> AsCommandsHandler<T>(this ComponentRegistration<T> registration, string boundedContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    CommandsHandlerFor = boundedContext,
                    IsCommandsHandler = true
                });
        }
     

        public static ComponentRegistration<T> AsProcess<T>(this ComponentRegistration<T> registration, string hostingBoundContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    ProcessFor = hostingBoundContext,
                    IsProcess = true
                });
        }


        public static ComponentRegistration<T> WithRepositoryAccess<T>(this ComponentRegistration<T> registration, string localBoundedContext) where T : class
        {
            return registration.ExtendedProperties(new
            {
                DependsOnBoundedContextRepository = localBoundedContext,
            });
        }  
    }
}