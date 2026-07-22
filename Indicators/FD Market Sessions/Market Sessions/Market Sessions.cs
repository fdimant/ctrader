using cAlgo.API;
using cAlgo.API.Internals;
using System;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MarketSessionsPro : Indicator
    {
        // ======== CONTROLE GLOBAL ========

        [Parameter("Number of Days", DefaultValue = 3, MinValue = 1)]
        public int NumberOfDays { get; set; }

        [Parameter("High/Low Only Last Day", DefaultValue = true)]
        public bool OnlyLastDayHighLow { get; set; }

        [Parameter("Chart Timezone Offset (UTC±)", DefaultValue = -3)]
        public int UtcOffset { get; set; }

        [Parameter("Max Timeframe", DefaultValue = "Hour1")]
        public TimeFrame MaxTimeFrame { get; set; }
        // ======== VISUAL ========

        [Parameter("Show High/Low Lines", DefaultValue = true)]
        public bool ShowLines { get; set; }

        [Parameter("Show Labels", DefaultValue = true)]
        public bool ShowLabels { get; set; }

        [Parameter("Show Range (Pips)", DefaultValue = true)]
        public bool ShowRange { get; set; }

        [Parameter("Line Thickness", DefaultValue = 1)]
        public int LineThickness { get; set; }

        [Parameter("Transparency (0-255)", DefaultValue = 40)]
        public int Transparency { get; set; }

        // ======== ASIAN SESSION ========

        [Parameter("Enable Asian Session", DefaultValue = true)]
        public bool EnableAsian { get; set; }

        [Parameter("Asian Session Name", DefaultValue = "ASIAN")]
        public string AsianSessionName { get; set; }

        [Parameter("Asian Start (HH:mm)", DefaultValue = "00:00")]
        public string AsianStart { get; set; }

        [Parameter("Asian End (HH:mm)", DefaultValue = "09:00")]
        public string AsianEnd { get; set; }

        [Parameter("Asian Color")]
        public Color AsianColor { get; set; }

        // ======== LONDON SESSION ========

        [Parameter("Enable London Session", DefaultValue = true)]
        public bool EnableLondon { get; set; }

        [Parameter("London Session Name", DefaultValue = "LONDON")]
        public string LondonSessionName { get; set; }

        [Parameter("London Start (HH:mm)", DefaultValue = "08:00")]
        public string LondonStart { get; set; }

        [Parameter("London End (HH:mm)", DefaultValue = "17:00")]
        public string LondonEnd { get; set; }

        [Parameter("London Color")]
        public Color LondonColor { get; set; }

        // ======== NEW YORK SESSION ========

        [Parameter("Enable New York Session", DefaultValue = true)]
        public bool EnableNewYork { get; set; }

        [Parameter("New York Session Name", DefaultValue = "NEW YORK")]
        public string NewYorkSessionName { get; set; }

        [Parameter("New York Start (HH:mm)", DefaultValue = "13:00")]
        public string NewYorkStart { get; set; }

        [Parameter("New York End (HH:mm)", DefaultValue = "22:00")]
        public string NewYorkEnd { get; set; }

        [Parameter("New York Color")]
        public Color NewYorkColor { get; set; }

        // ======== INTERNOS ========

        private TimeSpan asianStart, asianEnd;
        private TimeSpan londonStart, londonEnd;
        private TimeSpan nyStart, nyEnd;

        protected override void Initialize()
        {
            asianStart = TimeSpan.Parse(AsianStart).Add(TimeSpan.FromHours(UtcOffset*-1));
            asianEnd = TimeSpan.Parse(AsianEnd).Add(TimeSpan.FromHours(UtcOffset*-1));

            londonStart = TimeSpan.Parse(LondonStart).Add(TimeSpan.FromHours(UtcOffset*-1));
            londonEnd = TimeSpan.Parse(LondonEnd).Add(TimeSpan.FromHours(UtcOffset*-1));

            nyStart = TimeSpan.Parse(NewYorkStart).Add(TimeSpan.FromHours(UtcOffset*-1));
            nyEnd = TimeSpan.Parse(NewYorkEnd).Add(TimeSpan.FromHours(UtcOffset*-1));
        }

        public override void Calculate(int index)
        {
            DateTime cutoffDate = Server.Time.Date.AddDays(-NumberOfDays + 1);

            if (GetTimeFrameMinutes(Bars.TimeFrame) > GetTimeFrameMinutes(MaxTimeFrame))
                return;

            if (Bars.OpenTimes[index].Date < cutoffDate)
                return;

            if (EnableAsian)
                DrawSession(index, AsianSessionName, asianStart, asianEnd, AsianColor);

            if (EnableLondon)
                DrawSession(index, LondonSessionName, londonStart, londonEnd, LondonColor);

            if (EnableNewYork)
                DrawSession(index, NewYorkSessionName, nyStart, nyEnd, NewYorkColor);
        }

        private void DrawSession(int index, string sessionName, TimeSpan start, TimeSpan end, Color color)
        {
            DateTime time = Bars.OpenTimes[index];
            TimeSpan barTime = time.TimeOfDay;

            if (!IsInsideSession(barTime, start, end))
                return;

            DateTime sessionStartTime = time.Date + start;
            DateTime sessionEndTime = time.Date + end;

            if (end < start)
            {
                if (barTime < end)
                    sessionStartTime = sessionStartTime.AddDays(-1);
                else
                    sessionEndTime = sessionEndTime.AddDays(1);
            }

            double high = double.MinValue;
            double low = double.MaxValue;

            for (int i = 0; i <= index; i++)
            {
                DateTime t = Bars.OpenTimes[i];

                if (t >= sessionStartTime && t <= sessionEndTime)
                {
                    high = Math.Max(high, Bars.HighPrices[i]);
                    low = Math.Min(low, Bars.LowPrices[i]);
                }
            }

            var fillColor = Color.FromArgb(Transparency, color);

            string baseName = sessionName + sessionStartTime.ToString("yyyyMMdd");

            // ======== RETÂNGULO ========

            var rect = Chart.DrawRectangle(
                baseName,
                sessionStartTime,
                high,
                sessionEndTime,
                low,
                fillColor
            );

            rect.IsFilled = true;

            bool isLastDay = sessionStartTime.Date == Server.Time.Date;

            if (OnlyLastDayHighLow && !isLastDay)
                return;

            DateTime projectionEnd = sessionStartTime.Date.AddDays(1) + start;

            // ======== LINHAS ========

            if (ShowLines)
            {
                Chart.DrawTrendLine(baseName + "_HIGH",
                    sessionStartTime, high,
                    projectionEnd, high,
                    color, LineThickness);

                Chart.DrawTrendLine(baseName + "_LOW",
                    sessionStartTime, low,
                    projectionEnd, low,
                    color, LineThickness);
            }

            // ======== LABEL ========

           if (ShowLabels)
            {
                string label = sessionName;

                if (ShowRange)
                {
                    double rangePips = (high - low) / Symbol.PipSize;
                    label += $" ({Math.Round(rangePips, 1)} pips)";
                }

                double offset = (high - low) * 0.02;  // pequeno respiro
                double labelY = high + offset;

                Chart.DrawText(
                    baseName + "_LABEL",
                    label,
                    sessionStartTime,
                    labelY,
                    color
                ).VerticalAlignment = VerticalAlignment.Top;
            }


        }

        private bool IsInsideSession(TimeSpan time, TimeSpan start, TimeSpan end)
        {
            if (start < end)
                return time >= start && time <= end;

            return time >= start || time <= end;
        }

        private int GetTimeFrameMinutes(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute) return 1;
            if (tf == TimeFrame.Minute2) return 2;
            if (tf == TimeFrame.Minute3) return 3;
            if (tf == TimeFrame.Minute4) return 4;
            if (tf == TimeFrame.Minute5) return 5;
            if (tf == TimeFrame.Minute10) return 10;
            if (tf == TimeFrame.Minute15) return 15;
            if (tf == TimeFrame.Minute30) return 30;
            if (tf == TimeFrame.Hour) return 60;
            if (tf == TimeFrame.Hour4) return 240;
            if (tf == TimeFrame.Daily) return 1440;
            if (tf == TimeFrame.Weekly) return 10080;
            if (tf == TimeFrame.Monthly) return 43200;

            return int.MaxValue;
        }
    }
}
