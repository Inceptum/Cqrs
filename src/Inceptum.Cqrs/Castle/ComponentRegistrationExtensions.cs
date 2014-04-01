// ReSharper disable CheckNamespace
namespace Castle.MicroKernel.Registration
// ReSharper restore CheckNamespace
{
    public static class ComponentRegistrationExtensions
    {
        public static ComponentRegistration<T> AsProjection<T>(this ComponentRegistration<T> registration, string hostingBoundContext,string projectedBoundContext) where T : class
        {
            return registration.ExtendedProperties(new
                {
                    IsProjection = true,
                    ProjectedBoundContext = projectedBoundContext,
                    BoundContext = hostingBoundContext
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