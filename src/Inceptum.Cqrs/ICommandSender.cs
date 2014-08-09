using System;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    public interface ICommandSender : IDisposable
    {
        void SendCommand<T>(T command, string remoteBoundedContext,uint  priority=0);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, params Type[] types);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, Action<long> callback, params Type[] types);
        void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, params Type[] types);
        void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Action<long> callback, params Type[] types);
    }
}