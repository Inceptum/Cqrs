// ReSharper disable CheckNamespace

using System;

namespace Castle.MicroKernel.Registration
// ReSharper restore CheckNamespace
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<TProjection> AsProjection<TProjection, TBatchContext>(this ComponentRegistration<TProjection> registration, string hostingBoundContext,
            string projectedBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0
            , Func<TProjection, TBatchContext> beforeBatchApply = null, Action<TProjection, TBatchContext> afterBatchApply = null) where TProjection : class
        {
            Func<object, object> before = beforeBatchApply==null
                    ?(Func<object, object>) null
                    : p=> beforeBatchApply((TProjection)p);
            Action<object, object> after =afterBatchApply==null
                ?(Action<object, object>) null
                :(p,c)=> afterBatchApply((TProjection)p,(TBatchContext)c);

            return registration.ExtendedProperties(new
                {
                    IsProjection = true,
                    ProjectedBoundContext = projectedBoundContext,
                    BoundContext = hostingBoundContext,
                    BatchSize = batchSize,
                    ApplyTimeoutInSeconds = applyTimeoutInSeconds,
                    BeforeBatchApply = before,
                    AfterBatchApply = after,
                    BatchContextType= typeof(TBatchContext)

                });
        }        
        
        public static ComponentRegistration<TProjection> AsProjection<TProjection>(this ComponentRegistration<TProjection> registration, string hostingBoundContext,
            string projectedBoundContext, int batchSize = 0, int applyTimeoutInSeconds = 0) where TProjection : class
        {
          

            return registration.ExtendedProperties(new
                {
                    IsProjection = true,
                    ProjectedBoundContext = projectedBoundContext,
                    BoundContext = hostingBoundContext,
                    BatchSize = batchSize,
                    ApplyTimeoutInSeconds = applyTimeoutInSeconds,
                    BeforeBatchApply = ( Func<object, object>)null,
                    AfterBatchApply = (Action<object, object>)null,
                    BatchContextType= (Type)null

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