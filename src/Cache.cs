using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Org.Infinispan.Protostream;
using Org.Infinispan.Query.Remote.Client;

namespace Infinispan.Hotrod.Core
{

    public class UntypedCache
    {
        public static UntypedCache NullCache = new UntypedCache(null, "");
        protected InfinispanDG Cluster;
        public string Name { get; }
        public byte[] NameAsBytes { get; }
        public byte Version { get; protected set; }
        public Int64 MessageId { get; }
        public byte ClientIntelligence { get; }
        public UInt32 TopologyId { get; set; }
        public bool ForceReturnValue;
        public bool UseCacheDefaultLifespan;
        public bool UseCacheDefaultMaxIdle;

        public Int32 Flags { get { return getFlags(); } }

        private int getFlags()
        {
            int retVal = 0;
            if (ForceReturnValue) retVal += 1;
            if (UseCacheDefaultLifespan) retVal += 2;
            if (UseCacheDefaultMaxIdle) retVal += 4;
            return retVal;
        }

        public MediaType KeyMediaType { get; set; }
        public MediaType ValueMediaType { get; set; }
        public Codec30 codec;
        public UntypedCache(InfinispanDG ispnCluster, string name)
        {
            Cluster = ispnCluster;
            Name = name;
            MessageId = 1;
            NameAsBytes = Encoding.ASCII.GetBytes(Name);
            if (Cluster != null)
            {
                Version = Cluster.Version;
                ClientIntelligence = Cluster.ClientIntelligence;
                TopologyId = Cluster.TopologyId;
                ForceReturnValue = Cluster.ForceReturnValue;
            }
            codec = Codec.getCodec(Version);
        }

    }
    public class Cache<K, V> : UntypedCache
    {
        public Cache(InfinispanDG ispnCluster, Marshaller<K> keyM, Marshaller<V> valM, string name) : base(ispnCluster, name)
        {
            KeyMarshaller = keyM;
            ValueMarshaller = valM;
            Version = ispnCluster.Version;
        }
        Marshaller<K> KeyMarshaller;
        Marshaller<V> ValueMarshaller;

        public async Task<V> Get(K key)
        {
            return await Cluster.Get(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key);
        }
        public async Task<ValueWithVersion<V>> GetWithVersion(K key)
        {
            return await Cluster.GetWithVersion(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key);
        }
        public async Task<ValueWithMetadata<V>> GetWithMetadata(K key)
        {
            return await Cluster.GetWithMetadata(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key);
        }

        public async Task<V> Put(K key, V value, ExpirationTime lifespan = null, ExpirationTime maxidle = null)
        {
            return await Cluster.Put(KeyMarshaller, ValueMarshaller, this, key, value, lifespan, maxidle);
        }
        public async Task<Int32> Size()
        {
            return await Cluster.Size(this);
        }
        public async Task<Boolean> ContainsKey(K key)
        {
            return await Cluster.ContainsKey(KeyMarshaller, (UntypedCache)this, key);
        }
        public async Task<(V PrevValue, Boolean Removed)> Remove(K key)
        {
            return await Cluster.Remove(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key);
        }
        public async Task Clear()
        {
            await Cluster.Clear(this);
        }
        public async Task<Boolean> IsEmpty()
        {
            return await Cluster.Size(this) == 0;
        }
        public async Task<ServerStatistics> Stats()
        {
            return await Cluster.Stats(this);
        }
        public async Task<(V PrevValue, Boolean Replaced)> Replace(K key, V value, ExpirationTime lifespan = null, ExpirationTime maxidle = null)
        {
            return await Cluster.Replace(KeyMarshaller, ValueMarshaller, this, key, value, lifespan, maxidle);
        }
        public async Task<Boolean> ReplaceWithVersion(K key, V value, Int64 version, ExpirationTime lifeSpan = null, ExpirationTime maxIdle = null)
        {
            return await Cluster.ReplaceWithVersion(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key, value, version, lifeSpan, maxIdle);
        }
        public async Task<(V V, Boolean Removed)> RemoveWithVersion(K key, Int64 version)
        {
            return await Cluster.RemoveWithVersion(KeyMarshaller, ValueMarshaller, (UntypedCache)this, key, version);
        }
        public async Task<QueryResponse> Query(QueryRequest query)
        {
            return await Cluster.Query(query, (UntypedCache)this);
        }
        public async Task<List<Object>> Query(String query)
        {
            var qr = new QueryRequest();
            qr.QueryString = query;
            var queryResponse = await Cluster.Query(qr, (UntypedCache)this);
            List<Object> result = new List<Object>();
            if (queryResponse.ProjectionSize > 0)
            {  // Query has select
                return (List<object>)unwrapWithProjection(queryResponse);
            }
            for (int i = 0; i < queryResponse.NumResults; i++)
            {
                WrappedMessage wm = queryResponse.Results[i];

                if (wm.WrappedBytes != null)
                {
                    Object u = ValueMarshaller.unmarshall(wm.WrappedBytes.ToByteArray());
                    result.Add(u);
                }
            }
            return result;
        }
        public async Task<ISet<K>> KeySet()
        {
            return await Cluster.KeySet<K>(KeyMarshaller, (UntypedCache)this);
        }
        private static List<Object> unwrapWithProjection(QueryResponse resp)
        {
            List<Object> result = new List<Object>();
            if (resp.ProjectionSize == 0)
            {
                return result;
            }
            for (int i = 0; i < resp.NumResults; i++)
            {
                Object[] projection = new Object[resp.ProjectionSize];
                for (int j = 0; j < resp.ProjectionSize; j++)
                {
                    WrappedMessage wm = resp.Results[i * resp.ProjectionSize + j];
                    switch (wm.ScalarOrMessageCase)
                    {
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedDouble:
                            projection[j] = wm.WrappedDouble;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedFloat:
                            projection[j] = wm.WrappedFloat;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedInt64:
                            projection[j] = wm.WrappedInt64;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedUInt64:
                            projection[j] = wm.WrappedUInt64;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedInt32:
                            projection[j] = wm.WrappedInt32;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedFixed64:
                            projection[j] = wm.WrappedFixed64;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedFixed32:
                            projection[j] = wm.WrappedFixed32;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedBool:
                            projection[j] = wm.WrappedBool;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedString:
                            projection[j] = wm.WrappedString;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedBytes:
                            projection[j] = wm.WrappedBytes;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedUInt32:
                            projection[j] = wm.WrappedUInt32;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedSFixed32:
                            projection[j] = wm.WrappedSFixed32;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedSFixed64:
                            projection[j] = wm.WrappedSFixed64;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedSInt32:
                            projection[j] = wm.WrappedSInt32;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedSInt64:
                            projection[j] = wm.WrappedSInt64;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedDescriptorFullName:
                            projection[j] = wm.WrappedDescriptorFullName;
                            break;
                        case WrappedMessage.ScalarOrMessageOneofCase.WrappedMessageBytes:
                            projection[j] = wm.WrappedMessageBytes;
                            break;
                    }
                }
                result.Add(projection);
            }
            return result;
        }
    }
    public class ValueWithVersion<V>
    {
        public V Value;
        public Int64 Version;
    }

    public class ValueWithMetadata<V> : ValueWithVersion<V>
    {
        public Int64 Created = -1;
        public Int32 Lifespan = -1;
        public Int64 LastUsed = -1;
        public Int32 MaxIdle = -1;
    }
    public class VersionedResponse<V>
    {
    }

    public class ServerStatistics
    {
        public ServerStatistics(Dictionary<string, string> stats)
        {
            this.stats = stats;
        }
        /// <summary>
        ///   Number of seconds since Hot Rod started.
        /// </summary>
        public const String TIME_SINCE_START = "timeSinceStart";

        /// <summary>
        ///   Number of entries currently in the Hot Rod server.
        /// </summary>
        public const String CURRENT_NR_OF_ENTRIES = "currentNumberOfEntries";

        /// <summary>
        ///   Number of entries stored in Hot Rod server since the server started running.
        /// </summary>
        public const String TOTAL_NR_OF_ENTRIES = "totalNumberOfEntries";

        /// <summary>
        ///   Number of put operations.
        /// </summary>
        public const String STORES = "stores";

        /// <summary>
        ///   Number of get operations.
        /// </summary>
        public const String RETRIEVALS = "retrievals";

        /// <summary>
        ///   Number of get hits.
        /// </summary>
        public const String HITS = "hits";

        /// <summary>
        ///   Number of get misses.
        /// </summary>
        public const String MISSES = "misses";

        /// <summary>
        ///   Number of removal hits.
        /// </summary>
        public const String REMOVE_HITS = "removeHits";

        /// <summary>
        ///   Number of removal misses.
        /// </summary>
        public const String REMOVE_MISSES = "removeMisses";

        /// <summary>
        ///   Retrieve the complete list of statistics and their associated value.
        /// </summary>
        public IDictionary<String, String> GetStatsMap()
        {
            return stats;
        }

        /// <summary>
        ///   Retrive the value of the specified statistic.
        /// </summary>
        ///
        /// <param name="statName">name of the statistic to retrieve</param>
        ///
        /// <returns>the value for the specified statistic as a string or null</returns>
        public String GetStatistic(String statsName)
        {
            return stats != null ? stats[statsName] : null;
        }

        /// <summary>
        ///   Retrive the value of the specified statistic.
        /// </summary>
        ///
        /// <param name="statName">name of the statistic to retrieve</param>
        ///
        /// <returns>the value for the specified statistic as an int or -1 if no value is available</returns>
        public int GetIntStatistic(String statsName)
        {
            String value = GetStatistic(statsName);
            return value == null ? -1 : int.Parse(value);
        }
        private IDictionary<String, String> stats;
    }

}