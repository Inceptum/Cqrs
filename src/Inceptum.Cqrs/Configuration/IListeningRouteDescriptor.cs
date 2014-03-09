namespace Inceptum.Cqrs.Configuration
{
    public interface IListeningRouteDescriptor<out T> : IBoundedContextDescriptor
    {
        T On(string route);
    }
}