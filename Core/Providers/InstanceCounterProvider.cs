using ConsoleTables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIPS.Framework.Core.AutoRegister.Interfaces;
using SIPS.Framework.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SIPS.Framework.Core.Providers
{

    public partial class InstanceCounterProvider : IFCAutoRegisterSingleton, IDisposable, IFCSupportInstanceCounter
    {
        #region handle dispose
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _counter.SetRelease(this.GetType().Name, InstanceId);
                }

                disposedValue = true;

            }
        }

        public void Dispose()
        {
            // Non modificare questo codice. Inserire il codice di pulizia nel metodo 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
        private readonly InstanceCounterProvider _counter;
        public int InstanceId { get; private set; }
        private readonly ILogger<InstanceCounterProvider> _logger;
        private readonly Dictionary<string, int> _lastId = new Dictionary<string, int>();
        private readonly object _instanceCounterLock = new object();
        private readonly Dictionary<string, int> _activeInstanceCounter = new Dictionary<string, int>();
        private readonly bool _logActiveInstanceCounter;
        private readonly IConfiguration _configuration;

        public InstanceCounterProvider(ILogger<InstanceCounterProvider> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _logActiveInstanceCounter = true;
            _configuration.GetValue<bool>("InstanceCounterProvider:LogInstanceActions:Enabled", true);

            _counter = this;
            InstanceId = _counter.GetInstanceId(this.GetType().Name);
        }

        public int GetInstanceId(string classname)
        {
            lock (_instanceCounterLock)
            {
                int lastUsedId;
                if (!_lastId.TryGetValue(classname, out lastUsedId))
                {
                    _activeInstanceCounter.Add(classname, 0);
                    _lastId.Add(classname, 0);
                }
                lastUsedId++;
                _lastId[classname]++;
                _activeInstanceCounter[classname]++;
                if (_logActiveInstanceCounter)
                    _logger.LogInformation("InstanceCounterProvider created [{instanceId}] {providerName} ", lastUsedId, classname);

                return lastUsedId;
            }
        }

        public void SetRelease(string classname, int id)
        {
            lock (_instanceCounterLock)
            {
                _activeInstanceCounter[classname]--;
                if (_logActiveInstanceCounter)
                    _logger.LogInformation("InstanceCounterProvider released [{instanceId}] {providerName} ", id, classname);
            }
        }

        private class StatisticItem
        {
            public string Type { get; set; }
            public int Instances { get; set; }
        }
        public void LogCurrentInstanceStatistics()
        {
            lock (_instanceCounterLock)
            {
                _logger.LogInformation($"InstanceCounterProvider____LogCurrentInstanceStatistics____{_activeInstanceCounter.Count} types / {_activeInstanceCounter.Count(kv => kv.Value > 0)} active types / {_activeInstanceCounter.Values.Sum()} instances");
                try
                {
                    ConsoleTable
                    .From<StatisticItem>(_activeInstanceCounter
                                            .Where(kv => kv.Value > 0)
                                            .OrderByDescending(kv=>kv.Value)
                                            .ThenBy(kv=>kv.Key)
                                            .Select(kv => new StatisticItem { Type = kv.Key, Instances = kv.Value })
                                        )
                    .Configure(o => o.NumberAlignment = Alignment.Right)
                    .Write(ConsoleTables.Format.MarkDown);
                }
                catch (Exception)
                {

                    throw;
                }

            }
        }

    }
}
