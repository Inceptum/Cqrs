using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NEventStore;
using NEventStore.Conversion;
using NEventStore.Logging;

namespace Inceptum.Cqrs.NEventStore
{
    public static class EventUpgradeWireupExtensions
    {
        public static EventUpgradeWireup UsingEventUpgrading(this Wireup wireup)
        {
            return new EventUpgradeWireup(wireup);
        }
    }

    public interface IUpgradeEvents<TSource>
        where TSource : class
    {
        IEnumerable<object> Upgrade(TSource sourceEvent);
    }

    public class EventUpgradePipelineHook : IPipelineHook
    {

        private static readonly ILog m_Logger = LogFactory.BuildLogger(typeof(EventUpgradePipelineHook));
        private readonly IDictionary<Type, Func<object, IEnumerable<object>>> m_Converters;

        public EventUpgradePipelineHook(IDictionary<Type, Func<object, IEnumerable<object>>> converters)
        {
            if (converters == null)
                throw new ArgumentNullException("converters");
            this.m_Converters = converters;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Commit Select(Commit committed)
        {
            var events=committed.Events.SelectMany(e =>upgrade(e.Body).Select(x =>
            {
                var m=new EventMessage{Body = x};
                foreach (var header in e.Headers)
                {
                    m.Headers.Add(header.Key,header.Value);    
                }
                return m;
            })).ToArray();
            committed.Events.Clear();
            committed.Events.AddRange(events);
            return committed;
        }

        public bool PreCommit(Commit attempt)
        {
            return true;
        }

        public void PostCommit(Commit committed)
        {
            
        }

        protected virtual void Dispose(bool disposing)
        {
            m_Converters.Clear();
        }

        private IEnumerable<object> upgrade(object source)
        {
            Func<object, IEnumerable<object>> func;
            if (!m_Converters.TryGetValue(source.GetType(), out func))
                return new []{source};
            return func(source);
        }
    }


    public class EventUpgradeWireup : Wireup
    {
        private static readonly ILog m_Logger = LogFactory.BuildLogger(typeof (EventUpconverterWireup));
        private readonly List<Assembly> m_AssembliesToScan = new List<Assembly>();
        private readonly IDictionary<Type, Func<object, IEnumerable<object>>> m_Registered = new Dictionary<Type, Func<object, IEnumerable<object>>>();

        public EventUpgradeWireup(Wireup wireup)
            : base(wireup)
        {
                
        }

        private static IEnumerable<Assembly> getAllAssemblies()
        {
            return Assembly.GetCallingAssembly()
                           .GetReferencedAssemblies()
                           .Select(Assembly.Load)
                           .Concat(new[] {Assembly.GetCallingAssembly()});
        }

        private static IDictionary<Type, Func<object, IEnumerable<object>>> getConverters(IEnumerable<Assembly> toScan)
        {
            IEnumerable<KeyValuePair<Type, Func<object, IEnumerable<object>>>> c = from a in toScan
                                                                      from t in a.GetTypes()
                                                                      where !t.IsAbstract
                                                                      let i = t.GetInterface(typeof(IUpgradeEvents<>).FullName)
                                                                      where i != null
                                                                      let sourceType = i.GetGenericArguments().First()
                                                                      let convertMethod = i.GetMethods(BindingFlags.Public | BindingFlags.Instance).First()
                                                                      let instance = Activator.CreateInstance(t)
                                                                      select new KeyValuePair<Type, Func<object, IEnumerable<object>>>(
                                                                          sourceType, e => (IEnumerable<object>) convertMethod.Invoke(instance, new[] {e}));
            try
            {
                return c.ToDictionary(x => x.Key, x => x.Value);
            }
            catch (ArgumentException e)
            {
                throw new MultipleConvertersFoundException(e.Message, e);
            }
        }

        public virtual EventUpgradeWireup WithConvertersFrom(params Assembly[] assemblies)
        {
            m_Logger.Debug("Event upconverters loaded from", string.Concat(", ", assemblies));
            m_AssembliesToScan.AddRange(assemblies);
            return this;
        }

        public virtual EventUpgradeWireup WithConvertersFromAssemblyContaining(params Type[] converters)
        {
            IEnumerable<Assembly> assemblies = converters.Select(c => c.Assembly).Distinct();
            m_Logger.Debug("Event upconverters loaded from", string.Concat(", ", assemblies));
            m_AssembliesToScan.AddRange(assemblies);
            return this;
        }

        public virtual EventUpgradeWireup AddUpgrade<TSource>(
            IUpgradeEvents<TSource> upgrader)
            where TSource : class
        {
            if (upgrader == null)
            {
                throw new ArgumentNullException("upgrader");
            }

            m_Registered[typeof(TSource)] = @event => upgrader.Upgrade(@event as TSource);

            return this;
        }

        public override IStoreEvents Build()
        {
            m_Logger.Debug("Event upgrade registered");

            Container.Register(c =>
            {
                if (m_Registered.Count > 0)
                {
                    return new EventUpgradePipelineHook(m_Registered);
                }

                if (!m_AssembliesToScan.Any())
                {
                    m_AssembliesToScan.AddRange(getAllAssemblies());
                }

                IDictionary<Type, Func<object, IEnumerable<object>>> converters = getConverters(m_AssembliesToScan);
                return new EventUpgradePipelineHook(converters);
            });
            var hook = Container.Resolve<EventUpgradePipelineHook>();
            HookIntoPipelineUsing(hook);
            return base.Build();
        }

      
    }
}