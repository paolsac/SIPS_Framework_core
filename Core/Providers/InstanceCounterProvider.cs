using Autofac;
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
        private readonly bool _logActiveReleaseCounter;
        private readonly HashSet<string> _logActiveInstanceCounter_includes;
        private readonly HashSet<string> _logActiveReleaseCounter_includes;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, HashSet<int>> _activeInstanceIds;
        private readonly Dictionary<string, string> _autoregistrationTypes;
        private readonly ILifetimeScope _autofac;

        public InstanceCounterProvider(ILogger<InstanceCounterProvider> logger, IConfiguration configuration, ILifetimeScope autofac)
        {
            _logger = logger;
            _configuration = configuration;
            _logActiveInstanceCounter = _configuration.GetValue<bool>("InstanceCounterProvider:LogInstanceActions:Enabled", true);
            _logActiveReleaseCounter = _configuration.GetValue<bool>("InstanceCounterProvider:LogReleaseActions:Enabled", true);

            _logActiveInstanceCounter_includes = new HashSet<string>(_configuration.GetSection("InstanceCounterProvider:LogInstanceActions:Includes").Get<string[]>() ?? new string[] { });
            _logActiveReleaseCounter_includes = new HashSet<string>(_configuration.GetSection("InstanceCounterProvider:LogReleaseActions:Includes").Get<string[]>() ?? new string[] { });

            _counter = this;
            _activeInstanceIds = new Dictionary<string, HashSet<int>>();
            _autoregistrationTypes = new Dictionary<string, string>();
            _autofac = autofac;

            // watch the call to GetInstanceId. This line must not be moved before the _counter assignment and the _activeInstanceIds initialization

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
                    _activeInstanceIds.Add(classname, new HashSet<int>());
                    _lastId.Add(classname, 0);


                    var a = _autofac.ComponentRegistry.Registrations
                        .SelectMany(r => r.Services)
                        .OfType<Autofac.Core.TypedService>()
                        .Select(s => s.ServiceType)
                        .Where(t => t.Name == classname)
                        .Where(t => typeof(IFCAutoRegisterTransient).IsAssignableFrom(t) || typeof(IFCAutoRegisterScoped).IsAssignableFrom(t) || typeof(IFCAutoRegisterSingleton).IsAssignableFrom(t) || typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t) || typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t))
                        .Select(t => {
                            string interfaceName = "N/A";
                            if (typeof(IFCAutoRegisterTransient).IsAssignableFrom(t)) interfaceName = "IFCAutoRegisterTransient";
                            if (typeof(IFCAutoRegisterScoped).IsAssignableFrom(t)) interfaceName = "IFCAutoRegisterScoped";
                            if (typeof(IFCAutoRegisterSingleton).IsAssignableFrom(t)) interfaceName = "IFCAutoRegisterSingleton";
                            if (typeof(IFCAutoRegisterTransientNamed).IsAssignableFrom(t)) interfaceName = "IFCAutoRegisterTransientNamed";
                            if (typeof(IFCAutoRegisterScopedNamed).IsAssignableFrom(t)) interfaceName = "IFCAutoRegisterScopedNamed";
                            return new { Type = t.Name, Interface = interfaceName };
                        })
                        .Distinct()
                        .ToDictionary(t => t.Type, t => t.Interface);
                    if (a.ContainsKey(classname))
                    {
                        _autoregistrationTypes.Add(classname, a[classname]);
                    }
                    else
                    {
                        _autoregistrationTypes.Add(classname, "N/A");
                    }

                }
                lastUsedId++;
                if(lastUsedId == int.MaxValue)
                {
                    lastUsedId = 0;
                }
                _lastId[classname]= lastUsedId;
                _activeInstanceCounter[classname]++;
                _activeInstanceIds[classname].Add(lastUsedId);
                if (_logActiveInstanceCounter || _logActiveInstanceCounter_includes.Contains(classname))
                    _logger.LogInformation("InstanceCounterProvider created {providerName} [instanceId:{instanceId}] [active instances:{activeInstanceCounter}]", classname, lastUsedId, _activeInstanceCounter[classname]);

                return lastUsedId;
            }
        }

        public void SetRelease(string classname, int id)
        {
            lock (_instanceCounterLock)
            {
                _activeInstanceCounter[classname]--;
                _activeInstanceIds[classname].Remove(id);
                if (_logActiveReleaseCounter || _logActiveReleaseCounter_includes.Contains(classname))
                    _logger.LogInformation("InstanceCounterProvider released {providerName} [instanceId:{instanceId}] [active instances:{activeInstanceCounter}]", classname, id, _activeInstanceCounter[classname]);
            }
        }

        private class StatisticItem
        {
            public string Type { get; set; }
            public int Instances { get; set; }
            public string Interface { get; set; }
            public string IDlist { get; set; }
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
                                            .OrderByDescending(kv => kv.Value)
                                            .ThenBy(kv => kv.Key)
                                            .Select(kv => new StatisticItem
                                            {
                                                Type = kv.Key,
                                                Instances = kv.Value,
                                                Interface = _autoregistrationTypes[kv.Key],
                                                IDlist = TruncateString(_activeInstanceIds.ContainsKey(kv.Key) ? string.Join(",", _activeInstanceIds[kv.Key]) : "N/A", 30)
                                            })
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


        // get active instance counter to the log as readonly
        public IReadOnlyDictionary<string, int> GetActiveInstanceCounter()
        {
            return _activeInstanceCounter;
        }

        // get active instance counter to the log as readonly
        public IReadOnlyDictionary<string, HashSet<int>> GetActiveInstanceIds()
        {
            return _activeInstanceIds;
        }

        // get autoregistration types to the log as readonly
        public IReadOnlyDictionary<string, string> GetAutoregistrationTypes()
        {
            return _autoregistrationTypes;
        }

        private static string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            {
                return input;
            }

            return input.Substring(0, maxLength - 3) + "...";
        }


    }
}
