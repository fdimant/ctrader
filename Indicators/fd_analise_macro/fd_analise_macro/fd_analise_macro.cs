using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class DailyBiasAndSMC : Indicator
    {
        // --- PARÂMETROS GERAIS ---
        [Parameter("Mostrar FVG 4H?", Group = "FVG", DefaultValue = true)]
        public bool ShowH4Fvg { get; set; }

        [Parameter("Mostrar FVGs Tocados (Parciais)?", Group = "FVG", DefaultValue = true)]
        public bool ShowTouchedFvg { get; set; }

        [Parameter("Mostrar Liquidez H1?", Group = "Liquidez", DefaultValue = true)]
        public bool ShowH1Liquidity { get; set; }

        [Parameter("Candles Retroativos (Geral)", Group = "Geral", DefaultValue = 200)]
        public int Lookback { get; set; }

        private Bars _dailyBars;
        private Bars _h1Bars;

        // --- ESTRUTURAS DE DADOS ---
        private class FvgZone
        {
            public string Id { get; set; }
            public double Top { get; set; }
            public double Bottom { get; set; }
            public double BasePrice { get; set; }
            public DateTime StartTime { get; set; }
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

        private List<FvgZone> _allGaps = new List<FvgZone>();
        private List<LiquidityZone> _allLiquidity = new List<LiquidityZone>();

        protected override void Initialize()
        {
            _dailyBars = MarketData.GetBars(TimeFrame.Daily);
            _h1Bars = MarketData.GetBars(TimeFrame.Hour);
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            // 1. --- LÓGICA DO VIÉS DIÁRIO ---
            int currentDailyIndex = _dailyBars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);
            string tendencia = "Viés Lateral";
            Color textColor = Color.Gray;

            if (currentDailyIndex >= 2)
            {
                int d1 = currentDailyIndex - 1;
                int d2 = currentDailyIndex - 2;

                double d2Open = _dailyBars.OpenPrices[d2];
                double d2Close = _dailyBars.ClosePrices[d2];
                double d2High = _dailyBars.HighPrices[d2];
                double d2Low = _dailyBars.LowPrices[d2];

                double d1Open = _dailyBars.OpenPrices[d1];
                double d1Close = _dailyBars.ClosePrices[d1];
                double d1High = _dailyBars.HighPrices[d1];
                double d1Low = _dailyBars.LowPrices[d1];

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

            // 2. --- LIMPEZA DE DADOS E GRÁFICO ---
            _allGaps.Clear();
            _allLiquidity.Clear();
            LimparDesenhos();

            // 3. --- PROCESSAMENTO DE FVG E LIQUIDEZ ---
            if (ShowH4Fvg) ProcessarFVG(TimeFrame.Hour4, "H4");
            if (ShowH1Liquidity) ProcessarLiquidez(TimeFrame.Hour, "H1");

            DesenharFVG();
            DesenharLiquidez();

            // 4. --- LÓGICA DE FORMAÇÃO DO PAVIO E CISD ---
            string pavioStatus = tendencia == "Viés Lateral" ? "-" : "Pendente";

            if (tendencia != "Viés Lateral" && currentDailyIndex >= 0)
            {
                bool isBullBias = tendencia.Contains("Alta");
                DateTime todayStart = _dailyBars.OpenTimes[currentDailyIndex];
                
                int h1StartIndex = -1;
                for (int i = _h1Bars.Count - 1; i >= 0; i--)
                {
                    if (_h1Bars.OpenTimes[i] <= todayStart)
                    {
                        h1StartIndex = (_h1Bars.OpenTimes[i] == todayStart) ? i : i + 1;
                        break;
                    }
                }

                if (h1StartIndex != -1 && h1StartIndex < _h1Bars.Count)
                {
                    int h1CurrentIndex = _h1Bars.Count - 1;
                    
                    double extremePrice = isBullBias ? double.MaxValue : double.MinValue;
                    int extremeIndex = h1StartIndex;
                    bool isLocked = false;

                    // Varredura para achar a mitigação e acompanhar o extremo contínuo
                    for (int i = h1StartIndex; i <= h1CurrentIndex; i++)
                    {
                        bool isNewExtreme = false;

                        if (isBullBias && _h1Bars.LowPrices[i] <= extremePrice)
                        {
                            extremePrice = _h1Bars.LowPrices[i];
                            extremeIndex = i;
                            isNewExtreme = true;
                        }
                        else if (!isBullBias && _h1Bars.HighPrices[i] >= extremePrice)
                        {
                            extremePrice = _h1Bars.HighPrices[i];
                            extremeIndex = i;
                            isNewExtreme = true;
                        }

                        if (isNewExtreme && !isLocked)
                        {
                            bool mitigatedPOI = false;
                            string poiDetails = "";

                            foreach (var gap in _allGaps)
                            {
                                if (gap.StartTime < _h1Bars.OpenTimes[i])
                                {
                                    if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _h1Bars.OpenTimes[i]) continue;
                                    if (isBullBias && gap.IsBullish && extremePrice <= gap.Top) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                    if (!isBullBias && !gap.IsBullish && extremePrice >= gap.Bottom) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                }
                            }
                            
                            if (!mitigatedPOI)
                            {
                                foreach (var liq in _allLiquidity)
                                {
                                    if (liq.StartTime < _h1Bars.OpenTimes[i])
                                    {
                                        if (liq.IsMitigated && liq.MitigationTime < _h1Bars.OpenTimes[i]) continue;
                                        if (isBullBias && !liq.IsSwingHigh && extremePrice <= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                        if (!isBullBias && liq.IsSwingHigh && extremePrice >= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                    }
                                }
                            }

                            if (mitigatedPOI)
                            {
                                pavioStatus = "Pendente (" + poiDetails + ")";
                                isLocked = true; 
                            }
                        }
                    }

                    // --- CÁLCULO DINÂMICO DO CISD (BASEADO NO CORPO) ---
                    if (isLocked)
                    {
                        double cisdLevel = double.NaN;
                        DateTime cisdTime = DateTime.MinValue;

                        int searchLimit = Math.Max(1, extremeIndex - 48);

                        if (isBullBias)
                        {
                            for (int j = extremeIndex - 1; j >= searchLimit; j--)
                            {
                                if (j + 1 < _h1Bars.Count && j - 1 >= 0)
                                {
                                    // Localiza o fractal usando pavios, mas extrai o nível pelo corpo superior
                                    if (_h1Bars.HighPrices[j] > _h1Bars.HighPrices[j - 1] && _h1Bars.HighPrices[j] > _h1Bars.HighPrices[j + 1])
                                    {
                                        cisdLevel = Math.Max(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                        cisdTime = _h1Bars.OpenTimes[j];
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int j = extremeIndex - 1; j >= searchLimit; j--)
                            {
                                if (j + 1 < _h1Bars.Count && j - 1 >= 0)
                                {
                                    // Localiza o fractal usando pavios, mas extrai o nível pelo corpo inferior
                                    if (_h1Bars.LowPrices[j] < _h1Bars.LowPrices[j - 1] && _h1Bars.LowPrices[j] < _h1Bars.LowPrices[j + 1])
                                    {
                                        cisdLevel = Math.Min(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                        cisdTime = _h1Bars.OpenTimes[j];
                                        break;
                                    }
                                }
                            }
                        }

                        // Fallback usando limite de corpos
                        if (double.IsNaN(cisdLevel))
                        {
                            int startFallback = Math.Max(0, extremeIndex - 6);
                            double fallbackPrice = isBullBias ? double.MinValue : double.MaxValue;
                            int fallbackIndex = extremeIndex;

                            for (int j = startFallback; j < extremeIndex; j++)
                            {
                                if (isBullBias)
                                {
                                    double bodyTop = Math.Max(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                    if (bodyTop > fallbackPrice) { fallbackPrice = bodyTop; fallbackIndex = j; }
                                }
                                else
                                {
                                    double bodyBottom = Math.Min(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                    if (bodyBottom < fallbackPrice) { fallbackPrice = bodyBottom; fallbackIndex = j; }
                                }
                            }

                            if (fallbackPrice != double.MinValue && fallbackPrice != double.MaxValue)
                            {
                                cisdLevel = fallbackPrice;
                                cisdTime = _h1Bars.OpenTimes[fallbackIndex];
                            }
                        }

                        // --- VERIFICAÇÃO DE CONFIRMAÇÃO DO CISD (FECHAMENTO) ---
                        if (!double.IsNaN(cisdLevel))
                        {
                            bool cisdConfirmado = false;

                            for (int k = extremeIndex; k <= h1CurrentIndex; k++)
                            {
                                // Confirma apenas se o FECHAMENTO romper a linha do corpo do CISD
                                if (isBullBias && _h1Bars.ClosePrices[k] > cisdLevel) { cisdConfirmado = true; break; }
                                if (!isBullBias && _h1Bars.ClosePrices[k] < cisdLevel) { cisdConfirmado = true; break; }
                            }

                            if (cisdConfirmado)
                            {
                                pavioStatus = isBullBias ? "Fundo Confirmado (CISD)" : "Topo Confirmado (CISD)";
                            }

                            // Renderização do CISD
                            Color cisdColor = isBullBias ? Color.Lime : Color.Red;
                            string lineId = "CISD_Line_" + currentDailyIndex;
                            string txtId = "CISD_Txt_" + currentDailyIndex;

                            DateTime drawStart = cisdTime;
                            DateTime drawEnd = Server.TimeInUtc.AddHours(24);
                            
                            // Linha tracejada se aguardando, sólida se confirmou fechamento acima/abaixo
                            LineStyle style = cisdConfirmado ? LineStyle.Solid : LineStyle.DotsRare;
                            string statusTxt = cisdConfirmado ? " CISD Confirmado" : " CISD Aguardado";

                            Chart.DrawTrendLine(lineId, drawStart, cisdLevel, drawEnd, cisdLevel, cisdColor, 2, style);
                            Chart.DrawText(txtId, statusTxt, drawStart, cisdLevel, cisdColor);
                        }
                    }
                }
            }

            string textoPainel = string.Format("Ativo: {0}\nTendência Diária: {1}\nPavio D: {2}", Symbol.Name, tendencia, pavioStatus);
            Chart.DrawStaticText("DailyBiasTable", textoPainel, VerticalAlignment.Top, HorizontalAlignment.Right, textColor);
        }

        private void ProcessarLiquidez(TimeFrame tf, string tfLabel)
        {
            var bars = MarketData.GetBars(tf);
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

                    _allLiquidity.Add(new LiquidityZone
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

                    _allLiquidity.Add(new LiquidityZone
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

        private void DesenharLiquidez()
        {
            foreach (var liq in _allLiquidity)
            {
                var startIndex = Bars.OpenTimes.GetIndexByTime(liq.StartTime);
                if (startIndex == -1) continue;

                int currentIndex;
                if (liq.IsMitigated)
                {
                    currentIndex = Bars.OpenTimes.GetIndexByTime(liq.MitigationTime);
                    if (currentIndex < 0)
                    {
                        currentIndex = Bars.Count - 1;
                        for (int k = Bars.Count - 1; k >= 0; k--)
                        {
                            if (Bars.OpenTimes[k] <= liq.MitigationTime) { currentIndex = k; break; }
                        }
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

        private void ProcessarFVG(TimeFrame tf, string tfLabel)
        {
            var bars = MarketData.GetBars(tf);
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

                    _allGaps.Add(new FvgZone
                    {
                        Id = "fvg_" + tfLabel + "_" + i,
                        Top = top,
                        Bottom = bottom,
                        BasePrice = basePrice,
                        StartTime = bars.OpenTimes[i - 1], 
                        IsBullish = isBull,
                        IsTouched = isTouched,
                        TouchTime = touchTime,
                        IsFullyMitigated = isFullyMitigated,
                        FullyMitigatedTime = fullyMitigatedTime
                    });
                }
            }
        }

        private void DesenharFVG()
        {
            foreach (var gap in _allGaps)
            {
                if (gap.IsFullyMitigated) continue;
                if (gap.IsTouched && !ShowTouchedFvg) continue;

                var startIndex = Bars.OpenTimes.GetIndexByTime(gap.StartTime);
                if (startIndex == -1) continue;

                int currentIndex;
                if (gap.IsTouched)
                {
                    currentIndex = Bars.OpenTimes.GetIndexByTime(gap.TouchTime);
                    if (currentIndex < 0)
                    {
                        currentIndex = Bars.Count - 1;
                        for (int k = Bars.Count - 1; k >= 0; k--)
                        {
                            if (Bars.OpenTimes[k] <= gap.TouchTime) { currentIndex = k; break; }
                        }
                    }
                }
                else
                {
                    currentIndex = Bars.Count;
                }

                Color fvgColor = gap.IsBullish ? Color.Green : Color.Red;
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