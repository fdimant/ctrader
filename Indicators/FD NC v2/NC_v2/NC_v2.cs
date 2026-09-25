using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum DisplayMode
    {
        Ambos,
        Atuais,
        SomenteMitigados
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MultiTimeframeFVGFinal : Indicator
    {
        [Parameter("Modo SMC 2", DefaultValue = false, Group = "Modo Especial SMC")]
        public bool SmcMode2 { get; set; }

        [Parameter("Modo de Exibição", DefaultValue = DisplayMode.Ambos, Group = "Configurações Gerais")]
        public DisplayMode SelectedMode { get; set; }

        [Parameter("Candles Retroativos (Lookback)", DefaultValue = 200, Group = "Configurações Gerais")]
        public int Lookback { get; set; }

        [Parameter("Min. Tamanho (Pips)", DefaultValue = 1.0, Group = "Configurações Gerais")]
        public double MinGapSize { get; set; }

        [Parameter("Opacidade Box (0-255)", DefaultValue = 40, MinValue = 0, MaxValue = 255, Group = "Visual")]
        public int Opacity { get; set; }

        [Parameter("Desenhar TFs Inferiores?", DefaultValue = false, Group = "Visual")]
        public bool DrawLowerTFs { get; set; }

        // --- TIMEFRAMES, CORES E RASTROS ---
        
        [Parameter("Ver Semanal?", Group = "Semanal", DefaultValue = true)]
        public bool ShowWeekly { get; set; }
        [Parameter("Cor Semanal", Group = "Semanal", DefaultValue = "#FFFF941F")]
        public Color WeeklyColor { get; set; }
        [Parameter("Manter Rastro?", Group = "Semanal", DefaultValue = true)]
        public bool WeeklyTrace { get; set; }

        [Parameter("Ver Diário?", Group = "Diário", DefaultValue = true)]
        public bool ShowDaily { get; set; }
        [Parameter("Cor Diário", Group = "Diário", DefaultValue = "#FFFFFF33")]
        public Color DailyColor { get; set; }
        [Parameter("Manter Rastro?", Group = "Diário", DefaultValue = true)]
        public bool DailyTrace { get; set; }

        [Parameter("Ver 4 Horas?", Group = "4 Horas", DefaultValue = true)]
        public bool ShowH4 { get; set; }
        [Parameter("Cor 4 Horas", Group = "4 Horas", DefaultValue = "#FFBEBF00")]
        public Color H4Color { get; set; }
        [Parameter("Manter Rastro?", Group = "4 Horas", DefaultValue = true)]
        public bool H4Trace { get; set; }

        [Parameter("Ver 1 Hora?", Group = "1 Hora", DefaultValue = true)]
        public bool ShowH1 { get; set; }
        [Parameter("Cor 1 Hora", Group = "1 Hora", DefaultValue = "#FFFFFF66")]
        public Color H1Color { get; set; }
        [Parameter("Manter Rastro?", Group = "1 Hora", DefaultValue = true)]
        public bool H1Trace { get; set; }

        [Parameter("Ver 30 Minutos?", Group = "30 Minutos", DefaultValue = true)]
        public bool ShowM30 { get; set; }
        [Parameter("Cor 30 Minutos", Group = "30 Minutos", DefaultValue = "#FFFFFF99")]
        public Color M30Color { get; set; }
        [Parameter("Manter Rastro?", Group = "30 Minutos", DefaultValue = true)]
        public bool M30Trace { get; set; }

        [Parameter("Ver 15 Minutos?", Group = "15 Minutos", DefaultValue = true)]
        public bool ShowM15 { get; set; }
        [Parameter("Cor 15 Minutos", Group = "15 Minutos", DefaultValue = "#FFFFFFCD")]
        public Color M15Color { get; set; }
        [Parameter("Manter Rastro?", Group = "15 Minutos", DefaultValue = true)]
        public bool M15Trace { get; set; }

        [Parameter("Ver 5 Minutos?", Group = "5 Minutos", DefaultValue = true)]
        public bool ShowM5 { get; set; }
        [Parameter("Cor 5 Minutos", Group = "5 Minutos", DefaultValue = "#FFFFFFFF")]
        public Color M5Color { get; set; }
        [Parameter("Manter Rastro?", Group = "5 Minutos", DefaultValue = true)]
        public bool M5Trace { get; set; }

        [Parameter("Ver 3 Minutos?", Group = "3 Minutos", DefaultValue = true)]
        public bool ShowM3 { get; set; }
        [Parameter("Cor 3 Minutos", Group = "3 Minutos", DefaultValue = "#FFFFFFFF")]
        public Color M3Color { get; set; }
        [Parameter("Manter Rastro?", Group = "3 Minutos", DefaultValue = true)]
        public bool M3Trace { get; set; }

        private class FvgZone
        {
            public string Id { get; set; }
            public double Top { get; set; }
            public double Bottom { get; set; }
            public double BasePrice { get; set; }
            public DateTime StartTime { get; set; }
            public string TfName { get; set; }
            public Color Color { get; set; }
            public bool Mitigated { get; set; }
            public DateTime MitigationTime { get; set; }
        }

        private List<FvgZone> _allActiveGaps = new List<FvgZone>();

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            _allActiveGaps.Clear();

            // --- LÓGICA DO MODO SMC 2 ---
            if (SmcMode2)
            {
                if (TimeFrame == TimeFrame.Hour4)
                {
                    // No 4H, desenha 1H e 30m
                    ProcessTimeframe(TimeFrame.Hour, H1Color, "H1", true);
                    ProcessTimeframe(TimeFrame.Minute30, M30Color, "M30", true);
                }
                else if (TimeFrame == TimeFrame.Hour)
                {
                    // No 1H, desenha 5m
                    ProcessTimeframe(TimeFrame.Minute5, M5Color, "M5", true);
                }
                else if (TimeFrame == TimeFrame.Minute30)
                {
                    // No 30m, desenha 3m
                    ProcessTimeframe(TimeFrame.Minute3, M3Color, "M3", true);
                }
            }
            else
            {
                // Modo Normal (Configuração Manual)
                if (ShowWeekly) ProcessTimeframe(TimeFrame.Weekly, WeeklyColor, "W1", WeeklyTrace);
                if (ShowDaily) ProcessTimeframe(TimeFrame.Daily, DailyColor, "D1", DailyTrace);
                if (ShowH4) ProcessTimeframe(TimeFrame.Hour4, H4Color, "H4", H4Trace);
                if (ShowH1) ProcessTimeframe(TimeFrame.Hour, H1Color, "H1", H1Trace);
                if (ShowM30) ProcessTimeframe(TimeFrame.Minute30, M30Color, "M30", M30Trace);
                if (ShowM15) ProcessTimeframe(TimeFrame.Minute15, M15Color, "M15", M15Trace);
                if (ShowM5) ProcessTimeframe(TimeFrame.Minute5, M5Color, "M5", M5Trace);
                if (ShowM3) ProcessTimeframe(TimeFrame.Minute3, M3Color, "M3", M3Trace);
            }

            DrawGapsOnChart();
        }

        private void ProcessTimeframe(TimeFrame tf, Color color, string tfLabel, bool allowTrace)
        {
            // Ignora o filtro de TF inferior se o Modo SMC 2 estiver ativo
            if (!SmcMode2 && !DrawLowerTFs && tf < TimeFrame) return;

            var bars = MarketData.GetBars(tf);
            int count = bars.Count;
            int start = Math.Max(count - Lookback, 2);

            for (int i = start; i < count; i++)
            {
                double high0 = bars.HighPrices[i - 2];
                double low2 = bars.LowPrices[i];
                double low0 = bars.LowPrices[i - 2];
                double high2 = bars.HighPrices[i];

                bool isBull = low2 > high0 && (low2 - high0) >= MinGapSize * Symbol.PipSize;
                bool isBear = low0 > high2 && (low0 - high2) >= MinGapSize * Symbol.PipSize;

                if (isBull || isBear)
                {
                    double top = isBull ? low2 : low0;
                    double bottom = isBull ? high0 : high2;
                    double basePrice = isBull ? high0 : low0;

                    bool isMitigated = false;
                    DateTime mitigationTime = DateTime.MinValue;

                    for (int j = i; j < count; j++)
                    {
                        if (isBull && bars.LowPrices[j] <= bottom) { isMitigated = true; mitigationTime = bars.OpenTimes[j]; break; }
                        if (isBear && bars.HighPrices[j] >= top) { isMitigated = true; mitigationTime = bars.OpenTimes[j]; break; }
                    }

                    if (!isMitigated)
                    {
                        if (isBull && Symbol.Bid <= bottom) { isMitigated = true; mitigationTime = Server.TimeInUtc; }
                        if (isBear && Symbol.Ask >= top) { isMitigated = true; mitigationTime = Server.TimeInUtc; }
                    }

                    // --- REGRAS DE FILTRAGEM ---
                    if (SmcMode2)
                    {
                        if (isMitigated)
                        {
                            DateTime currentBarOpenTime = Bars.OpenTimes[Bars.Count - 1];
                            if (mitigationTime < currentBarOpenTime) continue;
                        }
                    }
                    else
                    {
                        if (SelectedMode == DisplayMode.Atuais && isMitigated) continue;
                        if (SelectedMode == DisplayMode.SomenteMitigados && !isMitigated) continue;
                        if (isMitigated && !allowTrace) continue;
                    }

                    _allActiveGaps.Add(new FvgZone
                    {
                        Id = $"fvg_{tfLabel}_{i}",
                        Top = top,
                        Bottom = bottom,
                        BasePrice = basePrice,
                        StartTime = bars.OpenTimes[i - 1],
                        TfName = tfLabel,
                        Color = color,
                        Mitigated = isMitigated,
                        MitigationTime = mitigationTime
                    });
                }
            }
        }

        private void DrawGapsOnChart()
        {
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name.StartsWith("fvg_mtf_") || obj.Name.StartsWith("line_fvg_") || obj.Name.StartsWith("txt_fvg_"))
                    Chart.RemoveObject(obj.Name);
            }

            int effectiveOpacity = SmcMode2 ? 30 : Opacity;

            foreach (var gap in _allActiveGaps)
            {
                var startIndex = Bars.OpenTimes.GetIndexByTime(gap.StartTime);
                if (startIndex == -1) continue;

                string uniqueId = "fvg_mtf_" + gap.Id;
                int currentIndex;

                if (gap.Mitigated)
                {
                    currentIndex = Bars.OpenTimes.GetIndexByTime(gap.MitigationTime);

                    if (currentIndex < 0)
                    {
                        currentIndex = Bars.Count - 1;

                        for (int k = Bars.Count - 1; k >= 0; k--)
                        {
                            if (Bars.OpenTimes[k] <= gap.MitigationTime)
                            {
                                currentIndex = k;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    currentIndex = Bars.Count - 1;
                }

                if (!gap.Mitigated)
                {
                    Color fillColor = Color.FromArgb(effectiveOpacity, gap.Color);

                    Chart.DrawRectangle(
                        uniqueId,
                        startIndex,
                        gap.Bottom,
                        currentIndex,
                        gap.Top,
                        fillColor).IsFilled = true;

                    Chart.DrawTrendLine(
                        "line_" + uniqueId,
                        startIndex,
                        gap.BasePrice,
                        currentIndex,
                        gap.BasePrice,
                        gap.Color,
                        1,
                        LineStyle.Solid);
                }
                else
                {
                    Chart.DrawTrendLine(
                        "line_" + uniqueId,
                        startIndex,
                        gap.BasePrice,
                        currentIndex,
                        gap.BasePrice,
                        gap.Color,
                        1,
                        LineStyle.DotsRare);
                }

                string label = "NC " + gap.TfName + (gap.Mitigated ? " (M)" : "");
                Chart.DrawText("txt_" + uniqueId, label, currentIndex, gap.BasePrice, gap.Color);
            }
        }
    }
}