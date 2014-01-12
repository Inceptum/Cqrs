using System;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    public interface ICommandSender : IDisposable
    {
        void SendCommand<T>(T command, string boundedContext, CommandPriority priority=CommandPriority.Normal);
        void ReplayEvents(string boundedContext, params Type[] types);
    }

/*

    public interface ICommandSender : IDisposable
    {
        void SendCommand<T>(T command, string boundedContext, RoutingKey key);
        void ReplayEvents(string boundedContext, params Type[] types);
    }

    public static class CommandSenderExtensions
    {
        public static void SendCommand<T>(this ICommandSender sender, T command, string boundedContext, CommandPriority priority = CommandPriority.Normal)
        {
            sender.SendCommand(command, boundedContext, new RoutingKey() { Priority = priority });
        }

    }*/
}