using System;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    public interface ICommandSender : IDisposable
    {
        void SendCommand<T>(T command, string remoteBoundedContext,uint  priority=0);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, Guid? aggregateId, params Type[] types);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, Guid? aggregateId, Action<long> callback, params Type[] types);
        void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Guid? aggregateId, params Type[] types);
        void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Guid? aggregateId, Action<long> callback, params Type[] types);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, Guid? aggregateId, int batchSize, params Type[] types);
        void ReplayEvents(string remoteBoundedContext, DateTime @from, Guid? aggregateId, Action<long> callback, int batchSize, params Type[] types);
        void ReplayEvents(string boundedContext, string remoteBoundedContext, DateTime @from, Guid? aggregateId, Action<long> callback, int batchSize, params Type[] types);

    }
}