using cAlgo.API;
using cAlgo.API.Internals;
using System;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MarketSessionsPro : Indicator
    {
        // =========================================================
        // CONTROLE
        // =========================================================

        [Parameter("Number of Days", DefaultValue = 3, MinValue = 1)]
        public int NumberOfDays { get; set; }

        [Parameter("High/Low Only Last Day", DefaultValue = true)]
        public bool OnlyLastDayHighLow { get; set; }

        [Parameter("Max Timeframe", DefaultValue = "Hour4")]
        public TimeFrame MaxTimeFrame { get; set; }

        [Parameter("UTC Offset", DefaultValue = 0, MinValue = -12, MaxValue = 14)]
        public int UtcOffset { get; set; }


        // =========================================================
        // VISUAL - SESSÕES
        // =========================================================

        [Parameter("Show High/Low Lines", DefaultValue = true)]
        public bool ShowLines { get; set; }

        [Parameter("Show Labels", DefaultValue = true)]
        public bool ShowLabels { get; set; }

        [Parameter("Show Range (Pips)", DefaultValue = true)]
        public bool ShowRange { get; set; }

        [Parameter("Line Thickness", DefaultValue = 1, MinValue = 1, MaxValue = 5)]
        public int LineThickness { get; set; }

        [Parameter("Transparency (0-255)", DefaultValue = 40, MinValue = 0, MaxValue = 255)]
        public int Transparency { get; set; }


        // =========================================================
        // DAY START
        // =========================================================

        [Parameter("Use Day Start", DefaultValue = false)]
        public bool UseDayStart { get; set; }

        [Parameter("Day Start (HH:mm)", DefaultValue = "00:00")]
        public string DayStart { get; set; }

        [Parameter("Day Start Color", DefaultValue = "White")]
        public Color DayStartColor { get; set; }

        [Parameter("Day Start Thickness", DefaultValue = 1, MinValue = 1, MaxValue = 5)]
        public int DayStartThickness { get; set; }


        // =========================================================
        // ASIAN SESSION
        // =========================================================

        [Parameter("Enable Asian Session", DefaultValue = true)]
        public bool EnableAsian { get; set; }

        [Parameter("Asian Session Name", DefaultValue = "ASIAN")]
        public string AsianSessionName { get; set; }

        [Parameter("Asian Start (HH:mm)", DefaultValue = "00:00")]
        public string AsianStart { get; set; }

        [Parameter("Asian End (HH:mm)", DefaultValue = "09:00")]
        public string AsianEnd { get; set; }

        [Parameter("Asian Color", DefaultValue = "DodgerBlue")]
        public Color AsianColor { get; set; }


        // =========================================================
        // LONDON SESSION
        // =========================================================

        [Parameter("Enable London Session", DefaultValue = true)]
        public bool EnableLondon { get; set; }

        [Parameter("London Session Name", DefaultValue = "LONDON")]
        public string LondonSessionName { get; set; }

        [Parameter("London Start (HH:mm)", DefaultValue = "08:00")]
        public string LondonStart { get; set; }

        [Parameter("London End (HH:mm)", DefaultValue = "17:00")]
        public string LondonEnd { get; set; }

        [Parameter("London Color", DefaultValue = "Green")]
        public Color LondonColor { get; set; }


        // =========================================================
        // NEW YORK SESSION
        // =========================================================

        [Parameter("Enable New York Session", DefaultValue = true)]
        public bool EnableNewYork { get; set; }

        [Parameter("New York Session Name", DefaultValue = "NEW YORK")]
        public string NewYorkSessionName { get; set; }

        [Parameter("New York Start (HH:mm)", DefaultValue = "13:00")]
        public string NewYorkStart { get; set; }

        [Parameter("New York End (HH:mm)", DefaultValue = "22:00")]
        public string NewYorkEnd { get; set; }

        [Parameter("New York Color", DefaultValue = "Red")]
        public Color NewYorkColor { get; set; }


        // =========================================================
        // INTERNOS
        // =========================================================

        private TimeSpan asianStart;
        private TimeSpan asianEnd;

        private TimeSpan londonStart;
        private TimeSpan londonEnd;

        private TimeSpan nyStart;
        private TimeSpan nyEnd;

        private TimeSpan dayStart;


        // =========================================================
        // INITIALIZE
        // =========================================================

        protected override void Initialize()
        {
            // Os horários informados pelo usuário são considerados
            // no timezone definido pelo UTC Offset.
            //
            // Como o indicador trabalha em UTC, fazemos:
            //
            // Horário UTC = Horário informado - UTC Offset

            asianStart = TimeSpan.Parse(AsianStart)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            asianEnd = TimeSpan.Parse(AsianEnd)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            londonStart = TimeSpan.Parse(LondonStart)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            londonEnd = TimeSpan.Parse(LondonEnd)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            nyStart = TimeSpan.Parse(NewYorkStart)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            nyEnd = TimeSpan.Parse(NewYorkEnd)
                .Add(TimeSpan.FromHours(UtcOffset * -1));

            dayStart = TimeSpan.Parse(DayStart)
                .Add(TimeSpan.FromHours(UtcOffset * -1));
        }


        // =========================================================
        // CALCULATE
        // =========================================================

        public override void Calculate(int index)
        {
            // -----------------------------------------------------
            // Verifica o timeframe
            // -----------------------------------------------------

            if (GetTimeFrameMinutes(Bars.TimeFrame) >
                GetTimeFrameMinutes(MaxTimeFrame))
                return;


            DateTime barDate = Bars.OpenTimes[index].Date;

            DateTime cutoffDate =
                Server.Time.Date.AddDays(-NumberOfDays + 1);

            if (barDate < cutoffDate)
                return;


            // -----------------------------------------------------
            // DAY START
            // -----------------------------------------------------

            if (UseDayStart)
                DrawDayStart(index);


            // -----------------------------------------------------
            // SESSÕES
            // -----------------------------------------------------

            if (EnableAsian)
                DrawSession(
                    index,
                    AsianSessionName,
                    asianStart,
                    asianEnd,
                    AsianColor
                );

            if (EnableLondon)
                DrawSession(
                    index,
                    LondonSessionName,
                    londonStart,
                    londonEnd,
                    LondonColor
                );

            if (EnableNewYork)
                DrawSession(
                    index,
                    NewYorkSessionName,
                    nyStart,
                    nyEnd,
                    NewYorkColor
                );
        }


        // =========================================================
        // DAY START
        // =========================================================

        private void DrawDayStart(int index)
        {
            DateTime candleStart = Bars.OpenTimes[index];

            DateTime candleEnd;

            // Se for o último candle disponível, usamos a duração
            // do timeframe para determinar o seu final.
            if (index < Bars.Count - 1)
            {
                candleEnd = Bars.OpenTimes[index + 1];
            }
            else
            {
                candleEnd = candleStart.AddMinutes(
                    GetTimeFrameMinutes(Bars.TimeFrame)
                );
            }

            // Day Start do dia correspondente ao candle
            DateTime dayStartTime = candleStart.Date + dayStart;

            // Se o Day Start estiver dentro deste candle,
            // desenhamos a linha no início do candle.
            if (dayStartTime >= candleStart &&
                dayStartTime < candleEnd)
            {
                string objectName =
                    "DAYSTART_" +
                    dayStartTime.ToString("yyyyMMdd");

                Chart.DrawVerticalLine(
                    objectName,
                    candleStart,
                    DayStartColor,
                    DayStartThickness,
                    LineStyle.Solid
                );
            }
        }


        // =========================================================
        // DRAW SESSION
        // =========================================================

        private void DrawSession(
            int index,
            string sessionName,
            TimeSpan start,
            TimeSpan end,
            Color color)
        {
            DateTime time = Bars.OpenTimes[index];

            TimeSpan barTime = time.TimeOfDay;

            if (!IsInsideSession(barTime, start, end))
                return;


            // -----------------------------------------------------
            // HORÁRIOS DA SESSÃO
            // -----------------------------------------------------

            DateTime sessionStartTime =
                time.Date + start;

            DateTime sessionEndTime =
                time.Date + end;


            // Sessão cruzando meia-noite
            if (end < start)
            {
                if (barTime < end)
                    sessionStartTime =
                        sessionStartTime.AddDays(-1);
                else
                    sessionEndTime =
                        sessionEndTime.AddDays(1);
            }


            // -----------------------------------------------------
            // HIGH / LOW DA SESSÃO
            // -----------------------------------------------------

            double high = double.MinValue;
            double low = double.MaxValue;

            for (int i = 0; i <= index; i++)
            {
                DateTime t = Bars.OpenTimes[i];

                if (t >= sessionStartTime &&
                    t <= sessionEndTime)
                {
                    high = Math.Max(
                        high,
                        Bars.HighPrices[i]
                    );

                    low = Math.Min(
                        low,
                        Bars.LowPrices[i]
                    );
                }
            }


            // Segurança
            if (high == double.MinValue ||
                low == double.MaxValue)
                return;


            // -----------------------------------------------------
            // CORES
            // -----------------------------------------------------

            Color fillColor =
                Color.FromArgb(
                    Transparency,
                    color
                );


            string baseName =
                sessionName +
                sessionStartTime.ToString("yyyyMMdd");


            // -----------------------------------------------------
            // RETÂNGULO
            // -----------------------------------------------------

            var rect = Chart.DrawRectangle(
                baseName,
                sessionStartTime,
                high,
                sessionEndTime,
                low,
                fillColor
            );

            rect.IsFilled = true;


            // -----------------------------------------------------
            // SOMENTE ÚLTIMO DIA
            // -----------------------------------------------------

            bool isLastDay =
                sessionStartTime.Date ==
                Server.Time.Date;

            if (OnlyLastDayHighLow && !isLastDay)
                return;


            // -----------------------------------------------------
            // PROJEÇÃO DAS LINHAS
            // -----------------------------------------------------

            DateTime projectionEnd =
                sessionStartTime.Date.AddDays(1) + start;


            if (ShowLines)
            {
                Chart.DrawTrendLine(
                    baseName + "_HIGH",
                    sessionStartTime,
                    high,
                    projectionEnd,
                    high,
                    color,
                    LineThickness,
                    LineStyle.Solid
                );

                Chart.DrawTrendLine(
                    baseName + "_LOW",
                    sessionStartTime,
                    low,
                    projectionEnd,
                    low,
                    color,
                    LineThickness,
                    LineStyle.Solid
                );
            }


            // -----------------------------------------------------
            // LABEL
            // -----------------------------------------------------

            if (ShowLabels)
            {
                string label =
                    sessionName;

                if (ShowRange)
                {
                    double rangePips =
                        (high - low) /
                        Symbol.PipSize;

                    label +=
                        $" ({Math.Round(rangePips, 1)} pips)";
                }


                // Coloca o label acima da máxima.
                double offset =
                    (high - low) * 0.02;

                // Para sessões com range muito pequeno,
                // garante um deslocamento mínimo.
                double minimumOffset =
                    Symbol.PipSize * 2;

                offset =
                    Math.Max(
                        offset,
                        minimumOffset
                    );

                double labelY =
                    high + offset;


                var text = Chart.DrawText(
                    baseName + "_LABEL",
                    label,
                    sessionStartTime,
                    labelY,
                    color
                );

                text.VerticalAlignment =
                    VerticalAlignment.Bottom;
            }
        }


        // =========================================================
        // VERIFICA SE ESTÁ DENTRO DA SESSÃO
        // =========================================================

        private bool IsInsideSession(
            TimeSpan time,
            TimeSpan start,
            TimeSpan end)
        {
            if (start < end)
                return time >= start &&
                       time <= end;

            // Sessão cruza meia-noite
            return time >= start ||
                   time <= end;
        }


        // =========================================================
        // TIMEFRAME EM MINUTOS
        // =========================================================

        private int GetTimeFrameMinutes(
            TimeFrame timeFrame)
        {
            if (timeFrame == TimeFrame.Minute)
                return 1;

            if (timeFrame == TimeFrame.Minute2)
                return 2;

            if (timeFrame == TimeFrame.Minute3)
                return 3;

            if (timeFrame == TimeFrame.Minute4)
                return 4;

            if (timeFrame == TimeFrame.Minute5)
                return 5;

            if (timeFrame == TimeFrame.Minute10)
                return 10;

            if (timeFrame == TimeFrame.Minute15)
                return 15;

            if (timeFrame == TimeFrame.Minute30)
                return 30;

            if (timeFrame == TimeFrame.Hour)
                return 60;

            if (timeFrame == TimeFrame.Hour4)
                return 240;

            if (timeFrame == TimeFrame.Daily)
                return 1440;

            if (timeFrame == TimeFrame.Weekly)
                return 10080;

            if (timeFrame == TimeFrame.Monthly)
                return 43200;

            return int.MaxValue;
        }
    }
}