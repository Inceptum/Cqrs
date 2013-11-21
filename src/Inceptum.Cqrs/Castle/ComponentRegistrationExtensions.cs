// ReSharper disable CheckNamespace
namespace Castle.MicroKernel.Registration
// ReSharper restore CheckNamespace
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsProjection<T>(this ComponentRegistration<T> registration, string boundContext,string projectedBoundContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    IsProjection = true,
                    ProjectedBoundContext = projectedBoundContext,
                    BoundContext = boundContext
                });
        }  
        
        public static ComponentRegistration<T> AsCommandsHandler<T>(this ComponentRegistration<T> registration, string localBoundedContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    CommandsHandlerFor = localBoundedContext,
                    IsCommandsHandler = true
                });
        }  
        public static ComponentRegistration<T> AsSaga<T>(this ComponentRegistration<T> registration, params string[] boundedContexts) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    ListenedBoundContexts = boundedContexts,
                    IsSaga = true
                });
        }

        public static ComponentRegistration<T> AsProcess<T>(this ComponentRegistration<T> registration, string localBoundedContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    ProcessFor = localBoundedContext,
                    IsProcess = true
                });
        }
    }
}