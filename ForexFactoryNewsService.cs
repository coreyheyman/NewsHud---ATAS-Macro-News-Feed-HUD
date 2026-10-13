using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TradeHUD.DataEngine.Models;

namespace TradeHUD.DataEngine.Services
{
    public class ForexFactoryNewsService
    {
        private static readonly HttpClient _httpClient;
        private static List<NewsItem> _cachedNews = new List<NewsItem>();
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private static DateTime _circuitBreakerUntil = DateTime.MinValue;

        static ForexFactoryNewsService()
        {
#pragma warning disable SYSLIB0014
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
#pragma warning restore SYSLIB0014

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<List<NewsItem>> FetchNewsAsync(string newsUrl)
        {
            if (DateTime.Now < _circuitBreakerUntil)
            {
                if (_cachedNews.Count > 0) return _cachedNews;
                return new List<NewsItem> { new NewsItem { Timestamp = DateTime.Now, Description = "NEWS: Circuit Breaker Active. Retrying...", Summary = "Network calls paused.", Importance = EventImportance.High } };
            }

            if (_cachedNews.Count > 0 && (DateTime.Now - _lastFetchTime).TotalSeconds < 55) return _cachedNews;

            // HARDCODED: Completely ignores the ATAS settings box so it stops passing your banned links
            string targetUrl = "https://search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=10001147";

            try
            {
                var response = await _httpClient.GetAsync(targetUrl);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    _circuitBreakerUntil = DateTime.Now.AddMinutes(1);
                    if (_cachedNews.Count > 0) return _cachedNews;
                    return new List<NewsItem> { new NewsItem { Timestamp = DateTime.Now, Description = "NEWS: Rate Limit. Pausing network calls...", Summary = "Server returned 429.", Importance = EventImportance.High } };
                }

                response.EnsureSuccessStatusCode();
                var xmlStream = await response.Content.ReadAsStreamAsync();
                var xdoc = XDocument.Load(xmlStream);
                var newsItems = new List<NewsItem>();

                foreach (var item in xdoc.Descendants("item"))
                {
                    string title = item.Element("title")?.Value?.Trim() ?? "";
                    string pubDateStr = item.Element("pubDate")?.Value?.Trim() ?? "";
                    string description = item.Element("description")?.Value?.Trim() ?? "";

                    if (string.IsNullOrEmpty(title)) continue;

                    DateTime pubDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(pubDateStr) && DateTimeOffset.TryParse(pubDateStr, out DateTimeOffset dto)) pubDate = dto.LocalDateTime;

                    string cleanSummary = string.IsNullOrEmpty(description) ? "No additional summary." : Regex.Replace(description, "<.*?>", string.Empty).Trim();

                    newsItems.Add(new NewsItem
                    {
                        Timestamp = pubDate,
                        Description = System.Net.WebUtility.HtmlDecode(title),
                        Summary = System.Net.WebUtility.HtmlDecode(cleanSummary),
                        Importance = DetermineNewsImpact(title)
                    });
                }

                if (newsItems.Count > 0) { _cachedNews = newsItems; _lastFetchTime = DateTime.Now; }
                return _cachedNews.Count > 0 ? _cachedNews : newsItems;
            }
            catch (Exception ex)
            {
                _circuitBreakerUntil = DateTime.Now.AddMinutes(1);
                if (_cachedNews.Count > 0) return _cachedNews;
                return new List<NewsItem> { new NewsItem { Timestamp = DateTime.Now, Description = "NEWS: Network Error. Pausing for 1m...", Summary = ex.Message, Importance = EventImportance.High } };
            }
        }

        private EventImportance DetermineNewsImpact(string headline)
        {
            if (string.IsNullOrEmpty(headline)) return EventImportance.Low;
            string lower = headline.ToLowerInvariant();
            string[] highImpactKeywords = { "fomc", "fed ", "powell", "cpi ", "pce ", "nfp ", "inflation", "interest rate", "war ", "missile", "nuclear", "strike", "attack", "emergency", "putin", "trump", "biden", "geopolitical", "nasdaq", "s&p", "dow " };
            foreach (var keyword in highImpactKeywords) if (lower.Contains(keyword)) return EventImportance.High;
            return EventImportance.Low;
        }
    }
}
