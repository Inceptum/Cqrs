using System;

namespace Inceptum.Cqrs
{
    public interface IProcess : IDisposable
    {
        void Start(ICommandSender commandSender, IEventPublisher eventPublisher);
    }
}
