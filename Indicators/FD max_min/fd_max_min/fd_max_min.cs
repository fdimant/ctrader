using cAlgo.API;
using cAlgo.API.Internals;
using System;

namespace cAlgo
{
    public enum PriceMode
    {
        Wicks,
        Body,
        Both
    }

    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class MultiTFHighLow : Indicator
    {
        // GLOBAL
        [Parameter("Espessura da Linha", DefaultValue = 1)]
        public int LineThickness { get; set; }

        [Parameter("Estilo da Linha", DefaultValue = LineStyle.Solid)]
        public LineStyle LineStyle { get; set; }

        [Parameter("Mostrar Preço no Label", DefaultValue = true)]
        public bool ShowPrice { get; set; }

        [Parameter("Offset Label Esquerda (barras)", DefaultValue = 3)]
        public int LabelOffset { get; set; }

        // CORES
        [Parameter("Cor High", DefaultValue = "Red")]
        public Color HighColor { get; set; }

        [Parameter("Cor Low", DefaultValue = "DodgerBlue")]
        public Color LowColor { get; set; }

        [Parameter("Cor Body High", DefaultValue = "Orange")]
        public Color BodyHighColor { get; set; }

        [Parameter("Cor Body Low", DefaultValue = "DeepSkyBlue")]
        public Color BodyLowColor { get; set; }

        // TIMEFRAMES
        [Parameter("Weekly Ativo", DefaultValue = true)]
        public bool WeeklyEnabled { get; set; }

        [Parameter("Daily Ativo", DefaultValue = true)]
        public bool DailyEnabled { get; set; }

        [Parameter("H4 Ativo", DefaultValue = true)]
        public bool H4Enabled { get; set; }

        [Parameter("H1 Ativo", DefaultValue = true)]
        public bool H1Enabled { get; set; }

        // MODO
        [Parameter("Tipo de Preço", DefaultValue = PriceMode.Wicks)]
        public PriceMode Mode { get; set; }

        public override void Calculate(int index)
        {
            if (index != Bars.Count - 1)
                return;

            if (WeeklyEnabled)
                DrawLevels(TimeFrame.Weekly, "W1");

            if (DailyEnabled)
                DrawLevels(TimeFrame.Daily, "D1");

            if (H4Enabled)
                DrawLevels(TimeFrame.Hour4, "H4");

            if (H1Enabled)
                DrawLevels(TimeFrame.Hour, "H1");
        }

        private void DrawLevels(TimeFrame tf, string tfName)
        {
            var bars = MarketData.GetBars(tf);

            if (bars.Count < 3)
                return;

            int last = bars.Count - 2;

            double high = bars.HighPrices[last];
            double low = bars.LowPrices[last];

            double bodyHigh = Math.Max(bars.OpenPrices[last], bars.ClosePrices[last]);
            double bodyLow = Math.Min(bars.OpenPrices[last], bars.ClosePrices[last]);

            DateTime startTime = bars.OpenTimes[last];

            int startIndex = Bars.OpenTimes.GetIndexByTime(startTime);
            int labelIndex = startIndex - LabelOffset;

            if (Mode == PriceMode.Wicks || Mode == PriceMode.Both)
            {
                DrawLevel(tfName + "_high", startTime, high, labelIndex, HighColor, tfName + " High");
                DrawLevel(tfName + "_low", startTime, low, labelIndex, LowColor, tfName + " Low");
            }

            if (Mode == PriceMode.Body || Mode == PriceMode.Both)
            {
                DrawLevel(tfName + "_bodyHigh", startTime, bodyHigh, labelIndex, BodyHighColor, tfName + " Body High");
                DrawLevel(tfName + "_bodyLow", startTime, bodyLow, labelIndex, BodyLowColor, tfName + " Body Low");
            }
        }

        private void DrawLevel(string id, DateTime startTime, double price, int labelIndex, Color color, string label)
        {
            Chart.DrawTrendLine(
                id,
                startTime,
                price,
                Bars.LastBar.OpenTime,
                price,
                color,
                LineThickness,
                LineStyle);

            string text = ShowPrice ? $"{label} ({price})" : label;

            Chart.DrawText(
                id + "_label",
                text,
                labelIndex,
                price,
                color);
        }
    }
}