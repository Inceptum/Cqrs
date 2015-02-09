using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonDomain;
using CommonDomain.Persistence;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Inceptum.Cqrs.EventStore
{
    public class AggregateNotFoundException : Exception
    {
        public readonly Guid Id;
        public readonly Type Type;

        public AggregateNotFoundException(Guid id, Type type)
            : base(string.Format("Aggregate '{0}' (type {1}) was not found.", id, type.Name))
        {
            Id = id;
            Type = type;
        }
    }
    public class AggregateDeletedException : Exception
    {
        public readonly Guid Id;
        public readonly Type Type;

        public AggregateDeletedException(Guid id, Type type)
            : base(string.Format("Aggregate '{0}' (type {1}) was deleted.", id, type.Name))
        {
            Id = id;
            Type = type;
        }
    }
    public class AggregateVersionException : Exception
    {
        public readonly Guid Id;
        public readonly Type Type;
        public readonly int AggregateVersion;
        public readonly int RequestedVersion;

        public AggregateVersionException(Guid id, Type type, int aggregateVersion, int requestedVersion)
            : base(string.Format("Requested version {2} of aggregate '{0}' (type {1}) - aggregate version is {3}", id, type.Name, requestedVersion, aggregateVersion))
        {
            Id = id;
            Type = type;
            AggregateVersion = aggregateVersion;
            RequestedVersion = requestedVersion;
        }
    }

    public class GetEventStoreAdapter : IRepository,IEventStoreAdapter
    {
        private const string EVENT_CLR_TYPE_HEADER = "EventClrTypeName";
        private const string AGGREGATE_CLR_TYPE_HEADER = "AggregateClrTypeName";
        private const string COMMIT_ID_HEADER = "CommitId";
        private const int WRITE_PAGE_SIZE = 500;
        private const int READ_PAGE_SIZE = 500;

        private readonly Func<Type, Guid, string> m_AggregateIdToStreamName;
        private readonly IConstructAggregates m_ConstructAggregates;

        private readonly IEventStoreConnection m_EventStoreConnection;
        private static readonly JsonSerializerSettings m_SerializerSettings;
        private IEventPublisher m_EventsPublisher;

        static GetEventStoreAdapter()
        {

            m_SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        }

        public GetEventStoreAdapter(IEventStoreConnection eventStoreConnection, IEventPublisher eventsPublisher, IConstructAggregates constructAggregates)
            : this(eventStoreConnection, eventsPublisher, (t, g) => string.Format("{0}-{1}", char.ToLower(t.Name[0]) + t.Name.Substring(1), g.ToString("N")), constructAggregates)
        {
        }

        public GetEventStoreAdapter(IEventStoreConnection eventStoreConnection, IEventPublisher eventsPublisher, Func<Type, Guid, string> aggregateIdToStreamName, IConstructAggregates constructAggregates)
        {
            m_EventsPublisher = eventsPublisher;
            m_EventStoreConnection = eventStoreConnection;
            m_AggregateIdToStreamName = aggregateIdToStreamName;
            m_ConstructAggregates = constructAggregates;
        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate
        {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = m_AggregateIdToStreamName(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>(id);

            var sliceStart = 0;
            StreamEventsSlice currentSlice;
            do
            {
                var sliceCount = sliceStart + READ_PAGE_SIZE <= version
                                    ? READ_PAGE_SIZE
                                    : version - sliceStart + 1;

                currentSlice = m_EventStoreConnection.ReadStreamEventsForward(streamName, sliceStart, sliceCount, false);

                if (currentSlice.Status == SliceReadStatus.StreamNotFound)
                    throw new AggregateNotFoundException(id, typeof (TAggregate));
                
                if (currentSlice.Status == SliceReadStatus.StreamDeleted)
                    throw new AggregateDeletedException(id, typeof (TAggregate));
                
                sliceStart = currentSlice.NextEventNumber;

                foreach (var evnt in currentSlice.Events)
                    aggregate.ApplyEvent(DeserializeEvent(evnt.OriginalEvent.Metadata, evnt.OriginalEvent.Data));
            } while (version >= currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);

            if (aggregate.Version != version && version < Int32.MaxValue)
                throw new AggregateVersionException(id, typeof (TAggregate), aggregate.Version, version);                

            return aggregate;
        }
        
        private TAggregate ConstructAggregate<TAggregate>(Guid id)
        {
            return (TAggregate)m_ConstructAggregates.Build(typeof(TAggregate), id, null);
            //return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        private static object DeserializeEvent(byte[] metadata, byte[] data)
        {
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EVENT_CLR_TYPE_HEADER).Value;
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName));
        }

        public void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            var commitHeaders = new Dictionary<string, object>
            {
                {COMMIT_ID_HEADER, commitId},
                {AGGREGATE_CLR_TYPE_HEADER, aggregate.GetType().AssemblyQualifiedName}
            };
            updateHeaders(commitHeaders);

            var streamName = m_AggregateIdToStreamName(aggregate.GetType(), aggregate.Id);
            var newEvents = aggregate.GetUncommittedEvents().Cast<object>().ToList();
            var originalVersion = aggregate.Version - newEvents.Count;
            var expectedVersion = originalVersion == 0 ? ExpectedVersion.NoStream : originalVersion;
            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();


            var transaction = m_EventStoreConnection.StartTransaction(streamName, expectedVersion);
            try
            {
                if (eventsToSave.Count < WRITE_PAGE_SIZE)
                {
                    transaction.Write(eventsToSave);
                }
                else
                {


                    var position = 0;
                    while (position < eventsToSave.Count)
                    {
                        var pageEvents = eventsToSave.Skip(position).Take(WRITE_PAGE_SIZE);
                        transaction.Write(pageEvents);
                        position += WRITE_PAGE_SIZE;
                    }

              
                }
                //TODO: not prod code. Need to arrange serialization, data saved to ES should be same as sent to queue
                foreach (var @event in newEvents)
                {
                    m_EventsPublisher.PublishEvent(@event);
                }
                transaction.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                //TODO:logging
                transaction.Rollback();
            }
            aggregate.ClearUncommittedEvents();
        }

        private static EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(evnt, m_SerializerSettings));

            var eventHeaders = new Dictionary<string, object>(headers)
            {
                {
                    EVENT_CLR_TYPE_HEADER, evnt.GetType().AssemblyQualifiedName
                }
            };
            var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventHeaders, m_SerializerSettings));
            var typeName = evnt.GetType().Name;

            return new EventData(eventId, typeName, true, data, metadata);
        }

        public void Bootstrap()
        {
            
        }

        public Func<IRepository>  Repository
        {
            get { return () => this; }
        }

        public IEnumerable<object> GetEventsFrom(DateTime @from, params Type[] types)
        {
            throw new NotImplementedException();
        }
    }
}