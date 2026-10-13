using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TradeHUD.DataEngine.Models;

namespace TradeHUD.DataEngine.Services
{
    public class FfCalendarItem
    {
        [JsonPropertyName("title")] public string Title { get; set; }
        [JsonPropertyName("country")] public string Country { get; set; }
        [JsonPropertyName("date")] public string DateString { get; set; }
        [JsonPropertyName("impact")] public string Impact { get; set; }
        [JsonPropertyName("forecast")] public string Forecast { get; set; }
        [JsonPropertyName("actual")] public string Actual { get; set; }
        [JsonPropertyName("previous")] public string Previous { get; set; }
    }

    public class ForexFactoryCalendarService
    {
        private static readonly HttpClient _httpClient;
        private static List<EconomicEvent> _cachedEvents = new List<EconomicEvent>();
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static DateTime _circuitBreakerUntil = DateTime.MinValue;

        static ForexFactoryCalendarService()
        {
#pragma warning disable SYSLIB0014
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
#pragma warning restore SYSLIB0014
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, AllowAutoRedirect = true };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<List<EconomicEvent>> FetchEconomicCalendarAsync(string calendarUrl)
        {
            if (DateTime.Now < _circuitBreakerUntil)
            {
                if (_cachedEvents.Count > 0) return _cachedEvents;
                return new List<EconomicEvent> { new EconomicEvent { Timestamp = DateTime.Now, Description = "CAL: Circuit Breaker Active. Retrying...", Importance = EventImportance.Medium } };
            }

            // --- AUTO-REFRESH TRIGGER ---
            // If an event passed within the last 6 minutes but has no Actual data yet,
            // we override the 15-minute lock to fetch the fresh results immediately.
            bool needsImmediateRefresh = false;
            if (_cachedEvents.Count > 0)
            {
                foreach (var ev in _cachedEvents)
                {
                    if (ev.Timestamp <= DateTime.Now && (DateTime.Now - ev.Timestamp).TotalMinutes <= 6 && string.IsNullOrWhiteSpace(ev.Actual))
                    {
                        needsImmediateRefresh = true;
                        break;
                    }
                }
            }

            if (_cachedEvents.Count > 0)
            {
                // Normal 15-min throttle, or an accelerated 55-second throttle if waiting for a live event drop
                if (!needsImmediateRefresh && (DateTime.Now - _lastFetchTime).TotalMinutes < 15) return _cachedEvents;
                if (needsImmediateRefresh && (DateTime.Now - _lastFetchTime).TotalSeconds < 55) return _cachedEvents;
            }

            string targetUrl = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

            try
            {
                var response = await _httpClient.GetAsync(targetUrl);

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == (HttpStatusCode)403)
                {
                    targetUrl = "https://api.codetabs.com/v1/proxy?quest=" + targetUrl;
                    response = await _httpClient.GetAsync(targetUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        _circuitBreakerUntil = DateTime.Now.AddMinutes(1);
                        if (_cachedEvents.Count > 0) return _cachedEvents;
                        return new List<EconomicEvent> { new EconomicEvent { Timestamp = DateTime.Now, Description = "CAL: IP Banned & Proxy Failed. Paused 1m.", Importance = EventImportance.Medium } };
                    }
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rawEvents = JsonSerializer.Deserialize<List<FfCalendarItem>>(json, options);
                var result = new List<EconomicEvent>();

                if (rawEvents != null)
                {
                    foreach (var item in rawEvents)
                    {
                        if (!string.Equals(item.Country, "USD", StringComparison.OrdinalIgnoreCase) && !string.Equals(item.Country, "US", StringComparison.OrdinalIgnoreCase)) continue;
                        if (DateTimeOffset.TryParse(item.DateString, out DateTimeOffset parsedOffset))
                        {
                            var importance = EventImportance.Low;
                            if (string.Equals(item.Impact, "High", StringComparison.OrdinalIgnoreCase)) importance = EventImportance.High;
                            else if (string.Equals(item.Impact, "Medium", StringComparison.OrdinalIgnoreCase)) importance = EventImportance.Medium;

                            var ecoEvent = new EconomicEvent
                            {
                                Timestamp = parsedOffset.LocalDateTime,
                                Description = $"[{item.Country}] {item.Title}",
                                Importance = importance,
                                Forecast = string.IsNullOrWhiteSpace(item.Forecast) ? null : item.Forecast,
                                Actual = string.IsNullOrWhiteSpace(item.Actual) ? null : item.Actual,
                                Previous = string.IsNullOrWhiteSpace(item.Previous) ? null : item.Previous
                            };
                            ecoEvent.CalculateVolatilityWindow(TimeSpan.FromMinutes(30));
                            result.Add(ecoEvent);
                        }
                    }
                }

                if (result.Count > 0) { _cachedEvents = result; _lastFetchTime = DateTime.Now; }
                return _cachedEvents.Count > 0 ? _cachedEvents : result;
            }
            catch (Exception)
            {
                _circuitBreakerUntil = DateTime.Now.AddMinutes(1);
                if (_cachedEvents.Count > 0) return _cachedEvents;
                return new List<EconomicEvent> { new EconomicEvent { Timestamp = DateTime.Now, Description = $"CAL: Network Error. Pausing 1m...", Importance = EventImportance.Medium } };
            }
        }
    }
}
