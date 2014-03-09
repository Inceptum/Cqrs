namespace Inceptum.Cqrs.Configuration
{
    public interface IPublishingRouteDescriptor<out T> : IBoundedContextDescriptor 
    {
        T  With(string route);
    }
}