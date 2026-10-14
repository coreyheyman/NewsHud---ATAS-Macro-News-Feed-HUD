using System;
using System.Text.Json.Serialization;

namespace TradeHUD.DataEngine.Models
{
    public enum EventImportance { Low = 1, Medium = 2, High = 3 }

    public interface IMarketEvent
    {
        DateTime Timestamp { get; }
        string Description { get; }
        EventImportance Importance { get; }
    }

    public class NewsItem : IMarketEvent
    {
        [JsonIgnore]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("headline")]
        public string Description { get; set; }

        // Add this to catch the article body from Finnhub
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("url")]
        public string SourceUrl { get; set; }

        [JsonIgnore]
        public EventImportance Importance { get; set; }

        [JsonPropertyName("datetime")]
        public long UnixTimeSeconds { get; set; }
    }

    public class EconomicEvent : IMarketEvent
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string Country { get; set; }
        public EventImportance Importance { get; set; }
        public string Actual { get; set; }
        public string Forecast { get; set; }
        public string Previous { get; set; }

        public DateTime VolatilityWindowStart { get; set; }
        public DateTime VolatilityWindowEnd { get; set; }

        public EconomicEvent()
        {
            Importance = EventImportance.Medium;
        }

        public void CalculateVolatilityWindow(TimeSpan windowDuration)
        {
            VolatilityWindowStart = Timestamp.Add(-windowDuration);
            VolatilityWindowEnd = Timestamp.Add(windowDuration);
        }
    }
}