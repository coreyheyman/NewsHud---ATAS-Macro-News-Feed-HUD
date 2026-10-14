using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Linq;
using TradeHUD.DataEngine.Models;
using TradeHUD.DataEngine.Services;

namespace TradeHUD.DataEngine
{
    public class MarketDataManager : INotifyPropertyChanged
    {
        public string NewsUrl { get; set; } = "https://www.fxstreet.com/rss/news";
        public string CalendarUrl { get; set; } = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

        public DateTime LastUpdated { get; private set; } = DateTime.Now;
        public event Action OnDataRefreshed;

        private readonly ForexFactoryNewsService _newsService;
        private readonly ForexFactoryCalendarService _calendarService;
        private readonly System.Timers.Timer _refreshTimer;
        private readonly ObservableCollection<IMarketEvent> _marketEvents = new ObservableCollection<IMarketEvent>();

        private readonly object _collectionLock = new object();
        private int _lastUpdateMinute = -1;

        public ObservableCollection<IMarketEvent> MarketEvents => _marketEvents;

        public MarketDataManager()
        {
            _newsService = new ForexFactoryNewsService();
            _calendarService = new ForexFactoryCalendarService();

            _refreshTimer = new System.Timers.Timer(1000);
            _refreshTimer.Elapsed += (sender, e) =>
            {
                int currentMinute = DateTime.Now.Minute;
                if (DateTime.Now.Second == 0 && currentMinute != _lastUpdateMinute)
                {
                    _lastUpdateMinute = currentMinute;
                    _ = RefreshDataAsync();
                }
            };
            _refreshTimer.AutoReset = true;
            _refreshTimer.Enabled = true;

            _ = RefreshDataAsync();
        }

        public void Stop()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
            }
        }

        private async Task RefreshDataAsync()
        {
            var newsItems = await _newsService.FetchNewsAsync(NewsUrl);
            var economicEvents = await _calendarService.FetchEconomicCalendarAsync(CalendarUrl);

            var combinedEvents = newsItems.Cast<IMarketEvent>().Concat(economicEvents).OrderBy(e => e.Timestamp).ToList();

            lock (_collectionLock)
            {
                _marketEvents.Clear();
                foreach (var marketEvent in combinedEvents)
                {
                    _marketEvents.Add(marketEvent);
                }
            }

            LastUpdated = DateTime.Now;
            OnPropertyChanged(nameof(MarketEvents));
            OnDataRefreshed?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}