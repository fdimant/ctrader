using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum DailyBiasMode
    {
        Auto,
        Continuação_de_Alta,
        Reversão_para_Alta,
        Continuação_de_Baixa,
        Reversão_para_Baixa
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class DailyBiasAndSMC : Indicator
    {
        // --- PARÂMETROS GERAIS ---
        [Parameter("Tendência Diária Fixa", Group = "Viés e Estrutura", DefaultValue = DailyBiasMode.Auto)]
        public DailyBiasMode ManualDailyBias { get; set; }

        [Parameter("Mostrar FVG H4 (Fase 1)?", Group = "Fase 1: Formação Diária", DefaultValue = true)]
        public bool ShowH4Fvg { get; set; }

        [Parameter("Mostrar Liquidez H1 (Fase 1)?", Group = "Fase 1: Formação Diária", DefaultValue = true)]
        public bool ShowH1Liquidity { get; set; }

        [Parameter("Mostrar FVGs Tocados (Parciais)?", Group = "Geral", DefaultValue = true)]
        public bool ShowTouchedFvg { get; set; }

        [Parameter("Candles Retroativos (Geral)", Group = "Geral", DefaultValue = 200)]
        public int Lookback { get; set; }

        [Parameter("Mostrar Painel?", Group = "Geral", DefaultValue = false)]
        public bool ShowPanel { get; set; }

        private Bars _dailyBars;
        private Bars _h4Bars;
        private Bars _h1Bars;

        // --- ESTRUTURAS DE DADOS ---
        private class FvgZone
        {
            public string Id { get; set; }
            public double Top { get; set; }
            public double Bottom { get; set; }
            public double BasePrice { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime ConfirmationTime { get; set; } 
            public bool IsBullish { get; set; }
            public bool IsTouched { get; set; }
            public DateTime TouchTime { get; set; }
            public bool IsFullyMitigated { get; set; }
            public DateTime FullyMitigatedTime { get; set; }
        }

        private class LiquidityZone
        {
            public string Id { get; set; }
            public double Price { get; set; }
            public DateTime StartTime { get; set; }
            public bool IsSwingHigh { get; set; }
            public bool IsMitigated { get; set; }
            public DateTime MitigationTime { get; set; }
        }

        private List<FvgZone> _gapsH4 = new List<FvgZone>();
        private List<LiquidityZone> _liqH1 = new List<LiquidityZone>();

        protected override void Initialize()
        {
            _dailyBars = MarketData.GetBars(TimeFrame.Daily);
            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h1Bars = MarketData.GetBars(TimeFrame.Hour);
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            // 1. --- LÓGICA DO VIÉS DIÁRIO ---
            int currentDailyIndex = _dailyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            string tendencia = "Viés Lateral";
            Color textColor = Color.Gray;

            double d1High = double.MinValue;
            double d1Low = double.MaxValue;

            if (ManualDailyBias != DailyBiasMode.Auto)
            {
                tendencia = ManualDailyBias.ToString().Replace("_", " ");
                textColor = tendencia.Contains("Alta") ? Color.LimeGreen : Color.Red;
            }
            else if (currentDailyIndex >= 2)
            {
                int d1 = currentDailyIndex - 1;
                int d2 = currentDailyIndex - 2;

                double d2Open = _dailyBars.OpenPrices[d2];
                double d2Close = _dailyBars.ClosePrices[d2];
                double d2High = _dailyBars.HighPrices[d2];
                double d2Low = _dailyBars.LowPrices[d2];

                double d1Open = _dailyBars.OpenPrices[d1];
                double d1Close = _dailyBars.ClosePrices[d1];
                d1High = _dailyBars.HighPrices[d1];
                d1Low = _dailyBars.LowPrices[d1];

                bool d2IsBullish = d2Close > d2Open;
                bool d2IsBearish = d2Close < d2Open;
                bool d1IsBullish = d1Close > d1Open;
                bool d1IsBearish = d1Close < d1Open;

                if (d2IsBullish)
                {
                    if (d1Close > d2High) { tendencia = "Continuação de Alta"; textColor = Color.LimeGreen; }
                    else if (d1IsBearish && d1High > d2Close && d1Close < d2Low) { tendencia = "Reversão para Baixa"; textColor = Color.Red; }
                    else if (d1High > d2High && d1Close < d2Close) { tendencia = "Reversão para Baixa"; textColor = Color.Red; }
                }
                else if (d2IsBearish)
                {
                    if (d1Close < d2Low) { tendencia = "Continuação de Baixa"; textColor = Color.Red; }
                    else if (d1IsBullish && d1Low < d2Close && d1Close > d2High) { tendencia = "Reversão para Alta"; textColor = Color.LimeGreen; }
                    else if (d1Low < d2Low && d1Close > d2Close) { tendencia = "Reversão para Alta"; textColor = Color.LimeGreen; }
                }
            }

            // 2. --- LIMPEZA E PROCESSAMENTO EM BACKGROUND ---
            _gapsH4.Clear(); 
            _liqH1.Clear(); 
            LimparDesenhos();

            ProcessarFVG(_h4Bars, "H4", _gapsH4);
            ProcessarLiquidez(_h1Bars, "H1", _liqH1);

            // 3. --- LÓGICA DE FORMAÇÃO DO PAVIO E CISD DIÁRIO (DUPLA CHECAGEM) ---
            DateTime todayStart = DateTime.MinValue;
            
            bool hasConfirmed = false;
            string finalStatus = "";
            
            // Variáveis do Lado Touro (Procurando Fundo)
            string bullStatus = "Pendente";
            double bullExtremePrice = double.MaxValue;
            int bullExtremeIndex = -1;
            bool bullIsLocked = false;
            bool bullCisdConfirmado = false;
            double bullCisdLevel = double.NaN;
            DateTime bullCisdTime = DateTime.MinValue;

            // Variáveis do Lado Urso (Procurando Topo)
            string bearStatus = "Pendente";
            double bearExtremePrice = double.MinValue;
            int bearExtremeIndex = -1;
            bool bearIsLocked = false;
            bool bearCisdConfirmado = false;
            double bearCisdLevel = double.NaN;
            DateTime bearCisdTime = DateTime.MinValue;

            if (currentDailyIndex >= 0)
            {
                todayStart = _dailyBars.OpenTimes[currentDailyIndex];
                
                int h1StartIndex = GetCoveringIndex(_h1Bars, todayStart);
                if (h1StartIndex != -1) { while (h1StartIndex < _h1Bars.Count - 1 && _h1Bars.OpenTimes[h1StartIndex] < todayStart) h1StartIndex++; }

                if (h1StartIndex != -1 && h1StartIndex < _h1Bars.Count)
                {
                    int h1CurrentIndex = _h1Bars.Count - 1;
                    bullExtremeIndex = h1StartIndex;
                    bearExtremeIndex = h1StartIndex;

                    for (int i = h1StartIndex; i <= h1CurrentIndex; i++)
                    {
                        if (hasConfirmed) break;

                        // -----------------------------------------
                        // LÓGICA DE FUNDO (BULL)
                        // -----------------------------------------
                        if (!bullIsLocked)
                        {
                            if (_h1Bars.LowPrices[i] <= bullExtremePrice) { bullExtremePrice = _h1Bars.LowPrices[i]; bullExtremeIndex = i; }

                            bool mitigatedPOI = false;
                            string poiDetails = "";
                            foreach (var gap in _gapsH4) {
                                if (gap.ConfirmationTime <= _h1Bars.OpenTimes[i] && gap.IsBullish) {
                                    if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _h1Bars.OpenTimes[i]) continue;
                                    if (_h1Bars.LowPrices[i] <= gap.Top) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                }
                            }
                            if (!mitigatedPOI) {
                                foreach (var liq in _liqH1) {
                                    if (liq.StartTime < _h1Bars.OpenTimes[i] && !liq.IsSwingHigh) {
                                        if (liq.IsMitigated && liq.MitigationTime < _h1Bars.OpenTimes[i]) continue;
                                        if (_h1Bars.LowPrices[i] <= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                    }
                                }
                            }
                            if (mitigatedPOI) {
                                bullStatus = "Pendente (" + poiDetails + ")";
                                bullIsLocked = true;
                                bullExtremePrice = _h1Bars.LowPrices[i];
                                bullExtremeIndex = i;
                            }
                        }

                        if (bullIsLocked && !bullCisdConfirmado)
                        {
                            if (_h1Bars.LowPrices[i] <= bullExtremePrice) { bullExtremePrice = _h1Bars.LowPrices[i]; bullExtremeIndex = i; }

                            bullCisdLevel = double.NaN;
                            int searchLimit = Math.Max(1, bullExtremeIndex - 48);

                            for (int j = bullExtremeIndex - 1; j >= searchLimit; j--) {
                                if (j + 1 < _h1Bars.Count && j - 1 >= 0) {
                                    if (_h1Bars.HighPrices[j] > _h1Bars.HighPrices[j - 1] && _h1Bars.HighPrices[j] > _h1Bars.HighPrices[j + 1]) {
                                        bullCisdLevel = Math.Max(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                        bullCisdTime = _h1Bars.OpenTimes[j];
                                        break;
                                    }
                                }
                            }

                            if (double.IsNaN(bullCisdLevel)) {
                                int startFallback = Math.Max(0, bullExtremeIndex - 6);
                                double fallbackPrice = double.MinValue;
                                int fallbackIndex = bullExtremeIndex;
                                for (int j = startFallback; j < bullExtremeIndex; j++) {
                                    double bodyTop = Math.Max(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                    if (bodyTop > fallbackPrice) { fallbackPrice = bodyTop; fallbackIndex = j; }
                                }
                                if (fallbackPrice != double.MinValue) {
                                    bullCisdLevel = fallbackPrice;
                                    bullCisdTime = _h1Bars.OpenTimes[fallbackIndex];
                                }
                            }

                            if (!double.IsNaN(bullCisdLevel) && i >= bullExtremeIndex) {
                                if (_h1Bars.ClosePrices[i] > bullCisdLevel) {
                                    bullCisdConfirmado = true;
                                    hasConfirmed = true;
                                    finalStatus = "Fundo Confirmado (CISD)";
                                    textColor = Color.LimeGreen;
                                    break;
                                }
                            }
                        }

                        // -----------------------------------------
                        // LÓGICA DE TOPO (BEAR)
                        // -----------------------------------------
                        if (!bearIsLocked)
                        {
                            if (_h1Bars.HighPrices[i] >= bearExtremePrice) { bearExtremePrice = _h1Bars.HighPrices[i]; bearExtremeIndex = i; }

                            bool mitigatedPOI = false;
                            string poiDetails = "";
                            foreach (var gap in _gapsH4) {
                                if (gap.ConfirmationTime <= _h1Bars.OpenTimes[i] && !gap.IsBullish) {
                                    if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _h1Bars.OpenTimes[i]) continue;
                                    if (_h1Bars.HighPrices[i] >= gap.Bottom) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                }
                            }
                            if (!mitigatedPOI) {
                                foreach (var liq in _liqH1) {
                                    if (liq.StartTime < _h1Bars.OpenTimes[i] && liq.IsSwingHigh) {
                                        if (liq.IsMitigated && liq.MitigationTime < _h1Bars.OpenTimes[i]) continue;
                                        if (_h1Bars.HighPrices[i] >= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                    }
                                }
                            }
                            if (mitigatedPOI) {
                                bearStatus = "Pendente (" + poiDetails + ")";
                                bearIsLocked = true;
                                bearExtremePrice = _h1Bars.HighPrices[i];
                                bearExtremeIndex = i;
                            }
                        }

                        if (bearIsLocked && !bearCisdConfirmado)
                        {
                            if (_h1Bars.HighPrices[i] >= bearExtremePrice) { bearExtremePrice = _h1Bars.HighPrices[i]; bearExtremeIndex = i; }

                            bearCisdLevel = double.NaN;
                            int searchLimit = Math.Max(1, bearExtremeIndex - 48);

                            for (int j = bearExtremeIndex - 1; j >= searchLimit; j--) {
                                if (j + 1 < _h1Bars.Count && j - 1 >= 0) {
                                    if (_h1Bars.LowPrices[j] < _h1Bars.LowPrices[j - 1] && _h1Bars.LowPrices[j] < _h1Bars.LowPrices[j + 1]) {
                                        bearCisdLevel = Math.Min(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                        bearCisdTime = _h1Bars.OpenTimes[j];
                                        break;
                                    }
                                }
                            }

                            if (double.IsNaN(bearCisdLevel)) {
                                int startFallback = Math.Max(0, bearExtremeIndex - 6);
                                double fallbackPrice = double.MaxValue;
                                int fallbackIndex = bearExtremeIndex;
                                for (int j = startFallback; j < bearExtremeIndex; j++) {
                                    double bodyBottom = Math.Min(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                    if (bodyBottom < fallbackPrice) { fallbackPrice = bodyBottom; fallbackIndex = j; }
                                }
                                if (fallbackPrice != double.MaxValue) {
                                    bearCisdLevel = fallbackPrice;
                                    bearCisdTime = _h1Bars.OpenTimes[fallbackIndex];
                                }
                            }

                            if (!double.IsNaN(bearCisdLevel) && i >= bearExtremeIndex) {
                                if (_h1Bars.ClosePrices[i] < bearCisdLevel) {
                                    bearCisdConfirmado = true;
                                    hasConfirmed = true;
                                    finalStatus = "Topo Confirmado (CISD)";
                                    textColor = Color.Red;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // 4. --- RENDERIZAÇÃO GRÁFICA E TEXTUAL ---
            if (ShowH4Fvg) DesenharFVG(_gapsH4, Color.Orange, Color.Purple);
            if (ShowH1Liquidity) DesenharLiquidez(_liqH1);

            string textoPainel;

            if (hasConfirmed)
            {
                textoPainel = string.Format("Ativo: {0}\nTendência Diária: {1}\nPavio D: {2}", Symbol.Name, tendencia, finalStatus);
                
                if (bullCisdConfirmado && !double.IsNaN(bullCisdLevel))
                {
                    Chart.DrawTrendLine("CISD_Line_D_" + currentDailyIndex, bullCisdTime, bullCisdLevel, Server.TimeInUtc.AddHours(24), bullCisdLevel, Color.Lime, 2, LineStyle.Solid);
                    Chart.DrawText("CISD_Txt_D_" + currentDailyIndex, " CISD Confirmado", bullCisdTime, bullCisdLevel, Color.Lime);
                }
                else if (bearCisdConfirmado && !double.IsNaN(bearCisdLevel))
                {
                    Chart.DrawTrendLine("CISD_Line_D_" + currentDailyIndex, bearCisdTime, bearCisdLevel, Server.TimeInUtc.AddHours(24), bearCisdLevel, Color.Red, 2, LineStyle.Solid);
                    Chart.DrawText("CISD_Txt_D_" + currentDailyIndex, " CISD Confirmado", bearCisdTime, bearCisdLevel, Color.Red);
                }
            }
            else
            {
                textoPainel = string.Format("Ativo: {0}\nTendência Diária: {1}\nPavio D (Fundo): {2}\nPavio D (Topo): {3}", Symbol.Name, tendencia, bullStatus, bearStatus);
                
                if (bullIsLocked && !double.IsNaN(bullCisdLevel))
                {
                    Chart.DrawTrendLine("CISD_Line_D_Bull_" + currentDailyIndex, bullCisdTime, bullCisdLevel, Server.TimeInUtc.AddHours(24), bullCisdLevel, Color.Lime, 2, LineStyle.DotsRare);
                    Chart.DrawText("CISD_Txt_D_Bull_" + currentDailyIndex, " CISD Aguardado", bullCisdTime, bullCisdLevel, Color.Lime);
                }
                if (bearIsLocked && !double.IsNaN(bearCisdLevel))
                {
                    Chart.DrawTrendLine("CISD_Line_D_Bear_" + currentDailyIndex, bearCisdTime, bearCisdLevel, Server.TimeInUtc.AddHours(24), bearCisdLevel, Color.Red, 2, LineStyle.DotsRare);
                    Chart.DrawText("CISD_Txt_D_Bear_" + currentDailyIndex, " CISD Aguardado", bearCisdTime, bearCisdLevel, Color.Red);
                }
            }

            if (ShowPanel)
            {
                Chart.DrawStaticText("DailyBiasTable", textoPainel, VerticalAlignment.Top, HorizontalAlignment.Right, textColor);
            }
            else
            {
                Chart.RemoveObject("DailyBiasTable");
            }
        }

        // --- FUNÇÕES AUXILIARES ---

        private int GetCoveringIndex(Bars bars, DateTime time)
        {
            for (int i = bars.Count - 1; i >= 0; i--)
            {
                if (bars.OpenTimes[i] <= time) return i;
            }
            return -1;
        }

        private void ProcessarLiquidez(Bars bars, string tfLabel, List<LiquidityZone> listOut)
        {
            int count = bars.Count;
            int start = Math.Max(count - Lookback, 2);

            for (int i = start; i < count - 1; i++)
            {
                double prevHigh = bars.HighPrices[i - 2];
                double currHigh = bars.HighPrices[i - 1]; 
                double nextHigh = bars.HighPrices[i];

                double prevLow = bars.LowPrices[i - 2];
                double currLow = bars.LowPrices[i - 1];   
                double nextLow = bars.LowPrices[i];

                bool isSwingHigh = currHigh > prevHigh && currHigh > nextHigh;
                bool isSwingLow = currLow < prevLow && currLow < nextLow;

                if (isSwingHigh)
                {
                    bool isMitigated = false;
                    DateTime mitigationTime = DateTime.MinValue;

                    for (int j = i + 1; j < count; j++)
                    {
                        if (bars.HighPrices[j] >= currHigh) { isMitigated = true; mitigationTime = bars.OpenTimes[j]; break; }
                    }

                    if (!isMitigated && Symbol.Ask >= currHigh) 
                    { 
                        isMitigated = true; 
                        mitigationTime = Server.TimeInUtc; 
                    }

                    DateTime exactStartTime = AncorarTempoNoLtf(bars.OpenTimes[i - 1], bars.OpenTimes[i], currHigh, true);

                    listOut.Add(new LiquidityZone
                    {
                        Id = "liqH_" + tfLabel + "_" + (i - 1),
                        Price = currHigh,
                        StartTime = exactStartTime, 
                        IsSwingHigh = true,
                        IsMitigated = isMitigated,
                        MitigationTime = mitigationTime
                    });
                }

                if (isSwingLow)
                {
                    bool isMitigated = false;
                    DateTime mitigationTime = DateTime.MinValue;

                    for (int j = i + 1; j < count; j++)
                    {
                        if (bars.LowPrices[j] <= currLow) { isMitigated = true; mitigationTime = bars.OpenTimes[j]; break; }
                    }

                    if (!isMitigated && Symbol.Bid <= currLow) 
                    { 
                        isMitigated = true; 
                        mitigationTime = Server.TimeInUtc; 
                    }

                    DateTime exactStartTime = AncorarTempoNoLtf(bars.OpenTimes[i - 1], bars.OpenTimes[i], currLow, false);

                    listOut.Add(new LiquidityZone
                    {
                        Id = "liqL_" + tfLabel + "_" + (i - 1),
                        Price = currLow,
                        StartTime = exactStartTime, 
                        IsSwingHigh = false,
                        IsMitigated = isMitigated,
                        MitigationTime = mitigationTime
                    });
                }
            }
        }

        private DateTime AncorarTempoNoLtf(DateTime exactStartTime, DateTime nextHtfTime, double pivotPrice, bool isHigh)
        {
            int ltfStartIndex = Bars.OpenTimes.GetIndexByTime(exactStartTime);
            if (ltfStartIndex != -1)
            {
                int ltfEndIndex = Bars.OpenTimes.GetIndexByTime(nextHtfTime);
                if (ltfEndIndex == -1) ltfEndIndex = Bars.Count - 1;
                else ltfEndIndex--;

                for (int k = ltfStartIndex; k <= ltfEndIndex && k < Bars.Count; k++)
                {
                    if (isHigh && Math.Abs(Bars.HighPrices[k] - pivotPrice) < Symbol.PipSize * 0.1) return Bars.OpenTimes[k];
                    if (!isHigh && Math.Abs(Bars.LowPrices[k] - pivotPrice) < Symbol.PipSize * 0.1) return Bars.OpenTimes[k];
                }
            }
            return exactStartTime;
        }

        private void DesenharLiquidez(List<LiquidityZone> liqList)
        {
            foreach (var liq in liqList)
            {
                var startIndex = Bars.OpenTimes.GetIndexByTime(liq.StartTime);
                
                if (startIndex == -1) startIndex = GetCoveringIndex(Bars, liq.StartTime);
                if (startIndex == -1) continue;

                int currentIndex;
                if (liq.IsMitigated)
                {
                    currentIndex = Bars.OpenTimes.GetIndexByTime(liq.MitigationTime);
                    if (currentIndex < 0)
                    {
                        currentIndex = GetCoveringIndex(Bars, liq.MitigationTime);
                        if (currentIndex < 0) currentIndex = Bars.Count - 1;
                    }
                }
                else
                {
                    currentIndex = Bars.Count;
                }

                Color liqColor = liq.IsSwingHigh ? Color.Orange : Color.DodgerBlue;
                Chart.DrawTrendLine("line_" + liq.Id, startIndex, liq.Price, currentIndex, liq.Price, liqColor, 1, LineStyle.DotsRare);
            }
        }

        private void ProcessarFVG(Bars bars, string tfLabel, List<FvgZone> listOut)
        {
            int count = bars.Count;
            int start = Math.Max(count - Lookback, 2);

            for (int i = start; i < count - 1; i++)
            {
                double high0 = bars.HighPrices[i - 2];
                double low2 = bars.LowPrices[i];
                double low0 = bars.LowPrices[i - 2];
                double high2 = bars.HighPrices[i];

                bool isBull = low2 > high0; 
                bool isBear = low0 > high2; 

                if (isBull || isBear)
                {
                    double top = isBull ? low2 : low0; 
                    double bottom = isBull ? high0 : high2; 
                    double basePrice = isBull ? high0 : low0; 

                    bool isTouched = false;
                    bool isFullyMitigated = false;
                    DateTime touchTime = DateTime.MinValue;
                    DateTime fullyMitigatedTime = DateTime.MinValue;

                    for (int j = i + 1; j < count; j++)
                    {
                        if (isBull)
                        {
                            if (bars.LowPrices[j] <= top) { if (!isTouched) { isTouched = true; touchTime = bars.OpenTimes[j]; } }
                            if (bars.LowPrices[j] <= bottom) { isFullyMitigated = true; fullyMitigatedTime = bars.OpenTimes[j]; break; }
                        }
                        else if (isBear)
                        {
                            if (bars.HighPrices[j] >= bottom) { if (!isTouched) { isTouched = true; touchTime = bars.OpenTimes[j]; } }
                            if (bars.HighPrices[j] >= top) { isFullyMitigated = true; fullyMitigatedTime = bars.OpenTimes[j]; break; }
                        }
                    }

                    if (!isFullyMitigated)
                    {
                        if (isBull) {
                            if (Symbol.Bid <= top && !isTouched) { isTouched = true; touchTime = Server.TimeInUtc; }
                            if (Symbol.Bid <= bottom) { isFullyMitigated = true; fullyMitigatedTime = Server.TimeInUtc; }
                        }
                        else if (isBear) {
                            if (Symbol.Ask >= bottom && !isTouched) { isTouched = true; touchTime = Server.TimeInUtc; }
                            if (Symbol.Ask >= top) { isFullyMitigated = true; fullyMitigatedTime = Server.TimeInUtc; }
                        }
                    }

                    TimeSpan tfDuration = bars.OpenTimes[i] - bars.OpenTimes[i - 1];
                    DateTime confTime = bars.OpenTimes[i].Add(tfDuration);

                    listOut.Add(new FvgZone
                    {
                        Id = "fvg_" + tfLabel + "_" + i,
                        Top = top,
                        Bottom = bottom,
                        BasePrice = basePrice,
                        StartTime = bars.OpenTimes[i - 1], 
                        ConfirmationTime = confTime,
                        IsBullish = isBull,
                        IsTouched = isTouched,
                        TouchTime = touchTime,
                        IsFullyMitigated = isFullyMitigated,
                        FullyMitigatedTime = fullyMitigatedTime
                    });
                }
            }
        }

        private void DesenharFVG(List<FvgZone> gapsList, Color corBull = default(Color), Color corBear = default(Color))
        {
            if (corBull == default(Color)) corBull = Color.Green;
            if (corBear == default(Color)) corBear = Color.Red;

            foreach (var gap in gapsList)
            {
                if (gap.IsFullyMitigated) continue;
                if (gap.IsTouched && !ShowTouchedFvg) continue;

                var startIndex = Bars.OpenTimes.GetIndexByTime(gap.StartTime);
                
                if (startIndex == -1) startIndex = GetCoveringIndex(Bars, gap.StartTime);
                if (startIndex == -1) continue;

                int currentIndex;
                if (gap.IsTouched)
                {
                    currentIndex = Bars.OpenTimes.GetIndexByTime(gap.TouchTime);
                    if (currentIndex < 0)
                    {
                        currentIndex = GetCoveringIndex(Bars, gap.TouchTime);
                        if (currentIndex < 0) currentIndex = Bars.Count - 1;
                    }
                }
                else
                {
                    currentIndex = Bars.Count;
                }

                Color fvgColor = gap.IsBullish ? corBull : corBear;
                int opacity = gap.IsTouched ? 15 : 50; 
                Color fillColor = Color.FromArgb(opacity, fvgColor); 

                var rect = Chart.DrawRectangle(gap.Id, startIndex, gap.Bottom, currentIndex, gap.Top, fillColor);
                rect.IsFilled = true; 

                if (!gap.IsTouched) Chart.DrawTrendLine("line_" + gap.Id, startIndex, gap.BasePrice, currentIndex, gap.BasePrice, fvgColor, 2, LineStyle.Solid);
                else Chart.DrawTrendLine("line_" + gap.Id, startIndex, gap.BasePrice, currentIndex, gap.BasePrice, fvgColor, 1, LineStyle.DotsRare);
            }
        }

        private void LimparDesenhos()
        {
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name.StartsWith("fvg_") || 
                    obj.Name.StartsWith("line_fvg_") || 
                    obj.Name.StartsWith("liqH_") || 
                    obj.Name.StartsWith("liqL_") || 
                    obj.Name.StartsWith("line_liq") ||
                    obj.Name.StartsWith("CISD_"))
                {
                    Chart.RemoveObject(obj.Name);
                }
            }
        }
    }
}