using Microsoft.Web.Iis.Rewrite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace IisUrlRewriteProviders
{
    public class DBProvider : IRewriteProvider, IProviderDescriptor, IDisposable
    {
        private string _connectionString;
        private string _storedProcedure;
        private int _cacheMinutesInterval;

        private readonly Timer _timer;
        private IDictionary<string, string> _cachedMappings;

        public DBProvider()
        {
            _timer = new Timer(RefreshCachedMappings, null, Timeout.Infinite, Timeout.Infinite);
        }

        public IEnumerable<SettingDescriptor> GetSettings()
        {
            yield return new SettingDescriptor("ConnectionString", "Sql Database Connection String");
            yield return new SettingDescriptor("StoredProcedure", "Name of the stored procedure");
            yield return new SettingDescriptor("CacheMinutesInterval", "Cache interval in minutes");
        }

        public void Initialize(IDictionary<string, string> settings, IRewriteContext rewriteContext)
        {
            if (!settings.TryGetValue("ConnectionString", out _connectionString) || string.IsNullOrEmpty(_connectionString))
                throw new ArgumentException("ConnectionString provider setting is required and cannot be empty");

            if (!settings.TryGetValue("StoredProcedure", out _storedProcedure) || string.IsNullOrEmpty(_storedProcedure))
            {
                throw new ArgumentException("StoredProcedure provider setting is required and cannot be empty");
            }

            if (!settings.TryGetValue("CacheMinutesInterval", out string cacheMinutesIntervalString) || string.IsNullOrEmpty(cacheMinutesIntervalString))
            {
                throw new ArgumentException("CacheMinutesInterval provider setting is required and cannot be empty");
            }
                
            if (!int.TryParse(cacheMinutesIntervalString, out _cacheMinutesInterval) || (_cacheMinutesInterval <= 0 && _cacheMinutesInterval >= 60))
            {
                throw new ArgumentException("CacheMinutesInterval provider setting must be an integer > 0 and < 60");
            }

            _cachedMappings = LoadMappingsFromDatabase();

            _timer.Change((int)TimeSpan.FromMinutes(_cacheMinutesInterval).TotalMilliseconds, Timeout.Infinite);
        }

        public string Rewrite(string input)
        {
            if (_cachedMappings.TryGetValue(input, out string output))
            {
                return output;
            }

            return String.Empty;
        }

        private IDictionary<string, string> LoadMappingsFromDatabase()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(_storedProcedure, connection))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    connection.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(reader[0].ToString(), reader[1].ToString());
                        }
                    }
                }
            }

            return result;
        }

        private void RefreshCachedMappings(object state)
        {
            try
            {
                _cachedMappings = LoadMappingsFromDatabase();
            }
            finally
            {
                try
                {
                    _timer.Change((int)TimeSpan.FromMinutes(_cacheMinutesInterval).TotalMilliseconds, Timeout.Infinite);
                }
                catch (ObjectDisposedException)
                {
                    // This catch is needed because when timer is disposed with WaitHandle
                    // it waits until the callback thread completes, but the timer is still disposed
                    // before the callback completes.
                }
            }           
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (ManualResetEvent e = new ManualResetEvent(false))
                {
                    {
                        _timer.Dispose(e);
                        e.WaitOne();
                    }
                }
            }
        }
    }
}
