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

            if (_cachedEvents.Count > 0 && (DateTime.Now - _lastFetchTime).TotalMinutes < 15) return _cachedEvents;

            // HARDCODED: Completely ignores the ATAS settings box 
            string targetUrl = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

            try
            {
                var response = await _httpClient.GetAsync(targetUrl);

                // If banned by FairEconomy, use CodeTabs proxy to mask the IP
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
                                Importance = importance
                            };
                            ecoEvent.CalculateVolatilityWindow(TimeSpan.FromMinutes(30));
                            result.Add(ecoEvent);
                        }
                    }
                }

                if (result.Count > 0) { _cachedEvents = result; _lastFetchTime = DateTime.Now; }
                return _cachedEvents.Count > 0 ? _cachedEvents : result;
            }
            catch (Exception ex)
            {
                _circuitBreakerUntil = DateTime.Now.AddMinutes(1);
                if (_cachedEvents.Count > 0) return _cachedEvents;
                return new List<EconomicEvent> { new EconomicEvent { Timestamp = DateTime.Now, Description = $"CAL: Network Error. Pausing 1m...", Importance = EventImportance.Medium } };
            }
        }
    }
}