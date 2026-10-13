using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using ATAS.Indicators;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using OFT.Rendering.Control;
using TradeHUD.DataEngine;
using TradeHUD.DataEngine.Models;

namespace CustomIndicators
{
    [DisplayName("NewsHud - Macro & News Feed")]
    public class TradeHudVisualIndicator : Indicator
    {
        public enum HeaderPositionOption { Top, Bottom }

        #region Settings Properties

        // --- HUD VISIBILITY ---
        [Display(Name = "Show HUD", GroupName = "1. HUD Visibility", Order = 1)]
        public bool EnableHud { get; set; } = true;

        [Display(Name = "Expand HUD Box", GroupName = "1. HUD Visibility", Order = 2)]
        public bool IsExpanded { get; set; } = true;

        [Display(Name = "Header Position", GroupName = "1. HUD Visibility", Order = 3)]
        public HeaderPositionOption HeaderPosition { get; set; } = HeaderPositionOption.Top;

        [Display(Name = "Show Timeline Icons", GroupName = "1. HUD Visibility", Order = 4)]
        public bool ShowTimelineIcons { get; set; } = true;

        // --- LAYOUT & SIZING ---
        [Display(Name = "Offset X (Pixels)", GroupName = "2. Layout & Sizing", Order = 1)]
        public int OffsetX { get; set; } = 20;

        [Display(Name = "Offset Y (Pixels)", GroupName = "2. Layout & Sizing", Order = 2)]
        public int OffsetY { get; set; } = 20;

        [Display(Name = "Box Height (Expanded)", GroupName = "2. Layout & Sizing", Order = 3)]
        public int BoxHeight { get; set; } = 350;

        // --- EVENT FILTERING ---
        [Display(Name = "High-Impact Only (Today's Events)", GroupName = "3. Event Filtering", Order = 1)]
        public bool HighImpactOnly { get; set; } = false;

        [Display(Name = "Max Events to Show", GroupName = "3. Event Filtering", Order = 2)]
        public int MaxEventsToShow { get; set; } = 5;

        // --- ALERTS ---
        [Display(Name = "Enable Event Flash Alerts", GroupName = "4. Alerts", Order = 1)]
        public bool EnableAlertFlashes { get; set; } = true;

        [Display(Name = "Flash Duration (Minutes)", GroupName = "4. Alerts", Order = 2)]
        public int FlashDurationMinutes { get; set; } = 5;

        #endregion

        private MarketDataManager _dataManager;
        private RenderFont _headerFont = new RenderFont("Arial", 11, FontStyle.Bold);
        private RenderFont _textFont = new RenderFont("Arial", 10, FontStyle.Regular);

        private Rectangle _headerHitBox;
        private Rectangle _upButtonHitBox;
        private Rectangle _downButtonHitBox;

        private IMarketEvent _expandedEvent = null;
        private IMarketEvent _hoveredEvent = null;
        private int _scrollOffset = 0;

        private class EventHitBox
        {
            public Rectangle Bounds { get; set; }
            public IMarketEvent Item { get; set; }
        }

        private List<EventHitBox> _eventHitBoxes = new List<EventHitBox>();
        private List<EventHitBox> _timelineHitBoxes = new List<EventHitBox>();

        public TradeHudVisualIndicator() : base(true)
        {
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);

            DataSeries[0].IsHidden = true;

            _dataManager = new MarketDataManager();
            _dataManager.OnDataRefreshed += () => RedrawChart();
        }

        public override bool ProcessMouseClick(RenderControlMouseEventArgs e)
        {
            if (!EnableHud || ChartInfo == null) return false;

            if (IsExpanded && _upButtonHitBox.Contains(e.Location))
            {
                _scrollOffset = Math.Max(_scrollOffset - 5, 0);
                RedrawChart();
                return true;
            }

            if (IsExpanded && _downButtonHitBox.Contains(e.Location))
            {
                _scrollOffset = Math.Min(_scrollOffset + 5, 30);
                RedrawChart();
                return true;
            }

            if (_headerHitBox.Contains(e.Location))
            {
                IsExpanded = !IsExpanded;
                _expandedEvent = null;
                _scrollOffset = 0;
                RedrawChart();
                return true;
            }

            foreach (var hitBox in _timelineHitBoxes)
            {
                if (hitBox.Bounds.Contains(e.Location))
                {
                    _expandedEvent = (_expandedEvent == hitBox.Item) ? null : hitBox.Item;
                    RedrawChart();
                    return true;
                }
            }

            if (IsExpanded)
            {
                foreach (var hitBox in _eventHitBoxes)
                {
                    if (hitBox.Bounds.Contains(e.Location))
                    {
                        _expandedEvent = (_expandedEvent == hitBox.Item) ? null : hitBox.Item;
                        RedrawChart();
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool ProcessMouseMove(RenderControlMouseEventArgs e)
        {
            if (!EnableHud || ChartInfo == null) return false;

            IMarketEvent currentHover = null;

            foreach (var hitBox in _timelineHitBoxes)
            {
                if (hitBox.Bounds.Contains(e.Location))
                {
                    currentHover = hitBox.Item;
                    break;
                }
            }

            if (currentHover == null && IsExpanded)
            {
                foreach (var hitBox in _eventHitBoxes)
                {
                    if (hitBox.Bounds.Contains(e.Location))
                    {
                        currentHover = hitBox.Item;
                        break;
                    }
                }
            }

            if (_hoveredEvent != currentHover)
            {
                _hoveredEvent = currentHover;
                RedrawChart();
            }

            return false;
        }

        private List<string> WrapText(string text, int maxLineLength)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text)) return list;

            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxLineLength)
                {
                    if (currentLine.Length > 0) list.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine += (currentLine.Length > 0 ? " " : "") + word;
                }
            }
            if (currentLine.Length > 0) list.Add(currentLine);
            return list;
        }

        private int GetXCoordinateByTime(DateTime targetTime)
        {
            int lastBar = CurrentBar - 1;
            if (lastBar < 1) return 0;

            var lastCandle = GetCandle(lastBar);

            if (targetTime <= lastCandle.Time)
            {
                for (int i = lastBar; i >= 0; i--)
                {
                    if (GetCandle(i).Time <= targetTime)
                        return ChartInfo.GetXByBar(i);
                }
                return ChartInfo.GetXByBar(0);
            }

            int lastX = ChartInfo.GetXByBar(lastBar);
            int prevX = ChartInfo.GetXByBar(lastBar - 1);
            int dx = lastX - prevX;
            if (dx <= 0) dx = 5;

            var prevCandle = GetCandle(lastBar - 1);
            double dt = (lastCandle.Time - prevCandle.Time).TotalSeconds;
            if (dt <= 0) dt = 60;

            double futureSeconds = (targetTime - lastCandle.Time).TotalSeconds;
            double extrapolatedX = lastX + ((futureSeconds / dt) * dx);

            return (int)extrapolatedX;
        }

        protected override void OnDispose()
        {
            if (_dataManager != null)
            {
                _dataManager.Stop();
            }
            base.OnDispose();
        }

        protected override void OnCalculate(int bar, decimal value) { }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (ChartInfo == null || _dataManager == null || !EnableHud) return;

            int width = 340;
            int height = BoxHeight;
            int x = OffsetX;
            int baseY = OffsetY;

            _eventHitBoxes.Clear();
            _timelineHitBoxes.Clear();

            var eventsCopy = _dataManager.MarketEvents.ToList();

            DateTime currentTime = DateTime.Now;
            var todayQuery = eventsCopy.OfType<EconomicEvent>()
                .Where(e => e.Timestamp.Date == DateTime.Today && e.Timestamp.AddMinutes(15) >= currentTime);

            if (HighImpactOnly)
            {
                todayQuery = todayQuery.Where(e => e.Importance == EventImportance.High);
            }

            var todayEvents = todayQuery.OrderBy(e => e.Timestamp).Take(Math.Max(1, MaxEventsToShow)).ToList();
            var newsItems = eventsCopy.OfType<NewsItem>()
                .OrderByDescending(e => e.Timestamp).Skip(_scrollOffset).Take(15).ToList();


            // ==========================================
            // 1. DRAW TIMELINE ICONS (Bottom of Chart)
            // ==========================================
            if (ShowTimelineIcons)
            {
                int bottomY = Container.Region.Height - 10;

                foreach (var ev in todayEvents)
                {
                    int xCenter = GetXCoordinateByTime(ev.Timestamp);

                    if (xCenter < -50 || xCenter > Container.Region.Width + 200) continue;

                    string lbl = ev.Importance == EventImportance.High ? "HIGH" :
                                 (ev.Importance == EventImportance.Medium ? "MED" : "LOW");

                    Color impactColor = ev.Importance == EventImportance.High ? Color.FromArgb(255, 230, 50, 50) :
                                      (ev.Importance == EventImportance.Medium ? Color.FromArgb(255, 255, 165, 0) : Color.FromArgb(255, 0, 220, 100));

                    Color textColor = (ev == _hoveredEvent) ? Color.FromArgb(255, 120, 200, 255) : impactColor;
                    Color bgColor = Color.FromArgb(255, 30, 30, 35);
                    Color borderColor = (ev == _hoveredEvent) ? Color.FromArgb(255, 120, 200, 255) : Color.FromArgb(255, 60, 60, 60);

                    int boxW = 40;
                    int boxH = 18;
                    int boxX = xCenter - (boxW / 2);
                    int iconY = bottomY - boxH;

                    var iconRect = new Rectangle(boxX, iconY, boxW, boxH);

                    context.FillRectangle(bgColor, iconRect);
                    context.DrawRectangle(new RenderPen(borderColor, 1), iconRect);
                    context.DrawString(lbl, _textFont, textColor, boxX + 4, iconY + 1);

                    _timelineHitBoxes.Add(new EventHitBox { Bounds = iconRect, Item = ev });

                    if (ev == _expandedEvent)
                    {
                        string popupText = $"{ev.Timestamp:HH:mm} | {ev.Description}";
                        var wrappedHeadline = WrapText(popupText, 40);

                        int popW = 260;
                        int popH = 10 + (wrappedHeadline.Count * 16);
                        int popX = xCenter - (popW / 2);
                        int popY = iconY - popH - 10;

                        if (popX < 5) popX = 5;
                        if (popX + popW > Container.Region.Width - 5) popX = Container.Region.Width - popW - 5;

                        var popRect = new Rectangle(popX, popY, popW, popH);
                        context.FillRectangle(Color.FromArgb(240, 30, 30, 35), popRect);
                        context.DrawRectangle(new RenderPen(textColor, 2), popRect);

                        context.DrawLine(new RenderPen(textColor, 2), xCenter, popY + popH, xCenter, iconY);

                        int textY = popY + 5;
                        foreach (var line in wrappedHeadline)
                        {
                            context.DrawString(line, _headerFont, Color.White, popX + 10, textY);
                            textY += 16;
                        }
                    }
                }
            }


            // ==========================================
            // 2. DRAW MAIN HUD (Top Left / Right)
            // ==========================================
            if (!IsExpanded)
            {
                int collapsedY = (HeaderPosition == HeaderPositionOption.Top) ? baseY : baseY - 25;
                int collapsedWidth = 115;

                _headerHitBox = new Rectangle(x, collapsedY, collapsedWidth, 25);
                context.FillRectangle(Color.FromArgb(230, 25, 25, 30), _headerHitBox);
                context.DrawRectangle(new RenderPen(Color.FromArgb(255, 60, 60, 60), 1), _headerHitBox);
                context.DrawString("NewsHud [ + ]", _headerFont, Color.White, x + 10, collapsedY + 5);
                return;
            }

            int boxY = (HeaderPosition == HeaderPositionOption.Top) ? baseY : baseY - height;
            var mainRect = new Rectangle(x, boxY, width, height);
            context.FillRectangle(Color.FromArgb(230, 25, 25, 30), mainRect);
            context.DrawRectangle(new RenderPen(Color.FromArgb(255, 60, 60, 60), 1), mainRect);

            int headerY = (HeaderPosition == HeaderPositionOption.Top) ? boxY : (boxY + height - 25);
            int contentStartY = (HeaderPosition == HeaderPositionOption.Top) ? (boxY + 35) : (boxY + 10);
            int maxYLimit = (HeaderPosition == HeaderPositionOption.Top) ? (boxY + height - 15) : (headerY - 15);

            _headerHitBox = new Rectangle(x, headerY, width, 25);
            context.FillRectangle(Color.FromArgb(255, 40, 40, 45), _headerHitBox);
            context.DrawString("NewsHud [ - ]", _headerFont, Color.White, x + 10, headerY + 5);

            _upButtonHitBox = new Rectangle(x + width - 45, headerY + 3, 20, 18);
            _downButtonHitBox = new Rectangle(x + width - 22, headerY + 3, 20, 18);

            context.FillRectangle(Color.FromArgb(255, 60, 60, 70), _upButtonHitBox);
            context.DrawString("▲", _textFont, Color.White, x + width - 42, headerY + 3);

            context.FillRectangle(Color.FromArgb(255, 60, 60, 70), _downButtonHitBox);
            context.DrawString("▼", _textFont, Color.White, x + width - 19, headerY + 3);

            int currentY = contentStartY;

            // --- SECTION: TODAY'S EVENTS ---
            context.DrawString("TODAY'S EVENTS", _headerFont, Color.LightGray, x + 10, currentY);
            currentY += 20;

            if (todayEvents.Any())
            {
                foreach (var ev in todayEvents)
                {
                    if (HeaderPosition == HeaderPositionOption.Bottom && currentY > maxYLimit - 40) break;

                    int itemStartY = currentY;
                    Color impactColor = ev.Importance == EventImportance.High ? Color.FromArgb(255, 230, 50, 50) :
                                        (ev.Importance == EventImportance.Medium ? Color.FromArgb(255, 255, 165, 0) : Color.FromArgb(255, 0, 220, 100));

                    if (ev == _hoveredEvent && ev != _expandedEvent) impactColor = Color.FromArgb(255, 120, 200, 255);

                    if (ev == _expandedEvent)
                    {
                        var wrappedHeadline = WrapText($"{ev.Timestamp:HH:mm} | {ev.Description}", 42);
                        foreach (var line in wrappedHeadline)
                        {
                            if (currentY > maxYLimit) break;
                            context.DrawString(line, _headerFont, impactColor, x + 10, currentY);
                            currentY += 18;
                        }

                        string details = "No additional forecast data provided.";
                        if (!string.IsNullOrEmpty(ev.Actual) || !string.IsNullOrEmpty(ev.Forecast))
                            details = $"Actual: {ev.Actual ?? "-"} | Forecast: {ev.Forecast ?? "-"}";

                        var wrappedSummary = WrapText(details, 52);
                        foreach (var line in wrappedSummary)
                        {
                            if (currentY > maxYLimit) break;
                            context.DrawString(line, _textFont, Color.Gray, x + 20, currentY);
                            currentY += 16;
                        }
                        currentY += 8;
                    }
                    else
                    {
                        string eventText = $"{ev.Timestamp:HH:mm} | {ev.Description}";
                        if (eventText.Length > 45) eventText = eventText.Substring(0, 42) + "...";

                        context.DrawString(eventText, _textFont, impactColor, x + 10, currentY);
                        currentY += 18;
                    }

                    _eventHitBoxes.Add(new EventHitBox { Bounds = new Rectangle(x, itemStartY, width, currentY - itemStartY), Item = ev });
                }
            }
            else
            {
                context.DrawString(HighImpactOnly ? "No upcoming high-impact events." : "No upcoming events.", _textFont, Color.Gray, x + 10, currentY);
                currentY += 18;
            }

            currentY += 5;
            context.DrawLine(new RenderPen(Color.FromArgb(255, 60, 60, 60), 1), x + 10, currentY, x + width - 10, currentY);
            currentY += 10;

            // --- SECTION: LIVE NEWS ---
            string lastRefreshedTime = _dataManager.LastUpdated.ToString("HH:mm:ss");
            context.DrawString("LIVE NEWS", _headerFont, Color.LightGray, x + 10, currentY);
            context.DrawString($"|  Refreshed: {lastRefreshedTime}", _textFont, Color.Gray, x + 100, currentY + 1);
            currentY += 20;

            if (newsItems.Any())
            {
                foreach (var news in newsItems)
                {
                    if (HeaderPosition == HeaderPositionOption.Top && currentY > maxYLimit - 25) break;
                    if (HeaderPosition == HeaderPositionOption.Bottom && currentY > maxYLimit) break;

                    int itemStartY = currentY;
                    Color impactColor = news.Importance == EventImportance.High ? Color.FromArgb(255, 230, 50, 50) : Color.White;

                    if (news == _hoveredEvent && news != _expandedEvent) impactColor = Color.FromArgb(255, 120, 200, 255);

                    if (news == _expandedEvent)
                    {
                        var wrappedHeadline = WrapText($"{news.Timestamp:HH:mm} | {news.Description}", 42);
                        foreach (var line in wrappedHeadline)
                        {
                            if (currentY > maxYLimit) break;
                            context.DrawString(line, _headerFont, impactColor, x + 10, currentY);
                            currentY += 18;
                        }

                        var wrappedSummary = WrapText(news.Summary, 52);
                        foreach (var line in wrappedSummary)
                        {
                            if (currentY > maxYLimit) break;
                            context.DrawString(line, _textFont, Color.Gray, x + 20, currentY);
                            currentY += 16;
                        }
                        currentY += 8;
                    }
                    else
                    {
                        string fullLine = $"{news.Timestamp:HH:mm} | {news.Description}";
                        if (fullLine.Length > 48) fullLine = fullLine.Substring(0, 45) + "...";

                        if (currentY > maxYLimit) break;
                        context.DrawString(fullLine, _textFont, impactColor, x + 10, currentY);
                        currentY += 18;
                    }

                    _eventHitBoxes.Add(new EventHitBox { Bounds = new Rectangle(x, itemStartY, width, currentY - itemStartY), Item = news });
                }
            }
            else
            {
                context.DrawString("Waiting for news feed...", _textFont, Color.Gray, x + 10, currentY);
            }
        }
    }
}
