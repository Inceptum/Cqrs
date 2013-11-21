namespace Inceptum.Cqrs
{
    public interface IEventPublisher
    {
        void PublishEvent(object @event);
    }
}