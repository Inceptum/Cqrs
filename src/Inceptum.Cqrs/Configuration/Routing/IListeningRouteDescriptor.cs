namespace Inceptum.Cqrs.Configuration.Routing
{
    public interface IListeningRouteDescriptor<out T> : IDescriptor<IRouteMap>
    {
        T On(string route);
    }
}