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

        [Parameter("Mostrar FVG H1 (Fase 2)?", Group = "Fase 2: H4 em Andamento", DefaultValue = true)]
        public bool ShowH1Fvg { get; set; }

        [Parameter("Mostrar FVG M30 (Fase 2)?", Group = "Fase 2: H4 em Andamento", DefaultValue = true)]
        public bool ShowM30Fvg { get; set; }

        [Parameter("Mostrar Liquidez M15 (Fase 2)?", Group = "Fase 2: H4 em Andamento", DefaultValue = true)]
        public bool ShowM15Liquidity { get; set; }

        [Parameter("Mostrar FVGs Tocados (Parciais)?", Group = "Geral", DefaultValue = true)]
        public bool ShowTouchedFvg { get; set; }

        [Parameter("Candles Retroativos (Geral)", Group = "Geral", DefaultValue = 200)]
        public int Lookback { get; set; }

        private Bars _dailyBars;
        private Bars _h4Bars;
        private Bars _h1Bars;
        private Bars _m30Bars;
        private Bars _m15Bars;

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
        private List<FvgZone> _gapsH1 = new List<FvgZone>();
        private List<FvgZone> _gapsM30 = new List<FvgZone>();
        
        private List<LiquidityZone> _liqH1 = new List<LiquidityZone>();
        private List<LiquidityZone> _liqM15 = new List<LiquidityZone>();

        protected override void Initialize()
        {
            _dailyBars = MarketData.GetBars(TimeFrame.Daily);
            _h4Bars = MarketData.GetBars(TimeFrame.Hour4);
            _h1Bars = MarketData.GetBars(TimeFrame.Hour);
            _m30Bars = MarketData.GetBars(TimeFrame.Minute30);
            _m15Bars = MarketData.GetBars(TimeFrame.Minute15);
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
                // Se for manual, atribui o texto substituindo os underscores
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
            _gapsH4.Clear(); _gapsH1.Clear(); _gapsM30.Clear();
            _liqH1.Clear(); _liqM15.Clear();
            LimparDesenhos();

            ProcessarFVG(_h4Bars, "H4", _gapsH4);
            ProcessarFVG(_h1Bars, "H1", _gapsH1);
            ProcessarFVG(_m30Bars, "M30", _gapsM30);
            
            ProcessarLiquidez(_h1Bars, "H1", _liqH1);
            ProcessarLiquidez(_m15Bars, "M15", _liqM15);

            // 3. --- LÓGICA DE FORMAÇÃO DO PAVIO E CISD DIÁRIO ---
            string pavioStatus = tendencia == "Viés Lateral" ? "Aguardando Rompimento D-1" : "Pendente";
            bool isBullBias = tendencia.Contains("Alta");
            
            bool phase2Active = false; 
            DateTime dailyConfirmTime = DateTime.MinValue;
            int dailyConfirmIndex = -1;
            DateTime todayStart = DateTime.MinValue;

            if (currentDailyIndex >= 0)
            {
                todayStart = _dailyBars.OpenTimes[currentDailyIndex];
                
                int h1StartIndex = GetCoveringIndex(_h1Bars, todayStart);
                if (h1StartIndex != -1) { while (h1StartIndex < _h1Bars.Count - 1 && _h1Bars.OpenTimes[h1StartIndex] < todayStart) h1StartIndex++; }

                if (h1StartIndex != -1 && h1StartIndex < _h1Bars.Count)
                {
                    int h1CurrentIndex = _h1Bars.Count - 1;
                    double extremePrice = isBullBias ? double.MaxValue : double.MinValue;
                    int extremeIndex = h1StartIndex;
                    bool isLocked = false;
                    
                    bool cisdConfirmado = false;
                    bool isInvalidated = false;
                    double cisdLevel = double.NaN;
                    DateTime cisdTime = DateTime.MinValue;

                    for (int i = h1StartIndex; i <= h1CurrentIndex; i++)
                    {
                        if (isInvalidated) break;

                        if (!isLocked)
                        {
                            if (tendencia == "Viés Lateral" && currentDailyIndex >= 2 && ManualDailyBias == DailyBiasMode.Auto)
                            {
                                if (_h1Bars.HighPrices[i] > d1High)
                                {
                                    tendencia = "Viés Lateral - Baixa";
                                    textColor = Color.Red;
                                    isBullBias = false;
                                    pavioStatus = "Rompeu topo D-1";
                                    isLocked = true;
                                    extremePrice = _h1Bars.HighPrices[i];
                                    extremeIndex = i;
                                }
                                else if (_h1Bars.LowPrices[i] < d1Low)
                                {
                                    tendencia = "Viés Lateral - Alta";
                                    textColor = Color.LimeGreen;
                                    isBullBias = true;
                                    pavioStatus = "Rompeu fundo D-1";
                                    isLocked = true;
                                    extremePrice = _h1Bars.LowPrices[i];
                                    extremeIndex = i;
                                }
                            }

                            if (!isLocked)
                            {
                                if (isBullBias && _h1Bars.LowPrices[i] <= extremePrice) { extremePrice = _h1Bars.LowPrices[i]; extremeIndex = i; }
                                else if (!isBullBias && _h1Bars.HighPrices[i] >= extremePrice) { extremePrice = _h1Bars.HighPrices[i]; extremeIndex = i; }

                                bool mitigatedPOI = false;
                                string poiDetails = "";
                                double currentEvalPrice = isBullBias ? _h1Bars.LowPrices[i] : _h1Bars.HighPrices[i];

                                foreach (var gap in _gapsH4)
                                {
                                    if (gap.ConfirmationTime <= _h1Bars.OpenTimes[i])
                                    {
                                        if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _h1Bars.OpenTimes[i]) continue;
                                        if (isBullBias && gap.IsBullish && currentEvalPrice <= gap.Top) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                        if (!isBullBias && !gap.IsBullish && currentEvalPrice >= gap.Bottom) { mitigatedPOI = true; poiDetails = "Mitigou FVG H4"; break; }
                                    }
                                }
                                
                                if (!mitigatedPOI)
                                {
                                    foreach (var liq in _liqH1)
                                    {
                                        if (liq.StartTime < _h1Bars.OpenTimes[i])
                                        {
                                            if (liq.IsMitigated && liq.MitigationTime < _h1Bars.OpenTimes[i]) continue;
                                            if (isBullBias && !liq.IsSwingHigh && currentEvalPrice <= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                            if (!isBullBias && liq.IsSwingHigh && currentEvalPrice >= liq.Price) { mitigatedPOI = true; poiDetails = "Capturou Liq H1"; break; }
                                        }
                                    }
                                }

                                if (mitigatedPOI)
                                {
                                    pavioStatus = "Pendente (" + poiDetails + ")";
                                    isLocked = true; 
                                    extremePrice = currentEvalPrice;
                                    extremeIndex = i;
                                }
                            }
                        }
                        
                        if (isLocked)
                        {
                            if (!cisdConfirmado)
                            {
                                if (isBullBias && _h1Bars.LowPrices[i] <= extremePrice) { extremePrice = _h1Bars.LowPrices[i]; extremeIndex = i; }
                                else if (!isBullBias && _h1Bars.HighPrices[i] >= extremePrice) { extremePrice = _h1Bars.HighPrices[i]; extremeIndex = i; }

                                cisdLevel = double.NaN;
                                int searchLimit = Math.Max(1, extremeIndex - 48);

                                if (isBullBias)
                                {
                                    for (int j = extremeIndex - 1; j >= searchLimit; j--)
                                    {
                                        if (j + 1 < _h1Bars.Count && j - 1 >= 0)
                                        {
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
                                            if (_h1Bars.LowPrices[j] < _h1Bars.LowPrices[j - 1] && _h1Bars.LowPrices[j] < _h1Bars.LowPrices[j + 1])
                                            {
                                                cisdLevel = Math.Min(_h1Bars.OpenPrices[j], _h1Bars.ClosePrices[j]);
                                                cisdTime = _h1Bars.OpenTimes[j];
                                                break;
                                            }
                                        }
                                    }
                                }

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

                                if (!double.IsNaN(cisdLevel) && i >= extremeIndex)
                                {
                                    if (isBullBias && _h1Bars.ClosePrices[i] > cisdLevel) { cisdConfirmado = true; dailyConfirmTime = _h1Bars.OpenTimes[i]; dailyConfirmIndex = i; }
                                    if (!isBullBias && _h1Bars.ClosePrices[i] < cisdLevel) { cisdConfirmado = true; dailyConfirmTime = _h1Bars.OpenTimes[i]; dailyConfirmIndex = i; }
                                }
                            }
                            else
                            {
                                if (isBullBias && _h1Bars.LowPrices[i] < extremePrice) { isInvalidated = true; }
                                if (!isBullBias && _h1Bars.HighPrices[i] > extremePrice) { isInvalidated = true; }
                            }
                        }
                    }

                    if (isLocked)
                    {
                        if (isInvalidated)
                        {
                            tendencia += " - Inval. Reversão";
                            textColor = Color.Gray;
                            pavioStatus = "CISD Invalidado (Extremo Capturado)";
                        }
                        else if (cisdConfirmado)
                        {
                            phase2Active = true; 
                            if (tendencia.Contains("Lateral")) pavioStatus = isBullBias ? "Rompimento D-1 + Fundo CISD" : "Rompimento D-1 + Topo CISD";
                            else pavioStatus = isBullBias ? "Fundo Confirmado (CISD)" : "Topo Confirmado (CISD)"; 
                        }

                        if (!double.IsNaN(cisdLevel))
                        {
                            Color cisdColor = isInvalidated ? Color.Gray : (isBullBias ? Color.Lime : Color.Red);
                            LineStyle style = (cisdConfirmado && !isInvalidated) ? LineStyle.Solid : LineStyle.DotsRare;
                            string statusTxt = isInvalidated ? " CISD Invalidado" : (cisdConfirmado ? " CISD Confirmado" : " CISD Aguardado");

                            Chart.DrawTrendLine("CISD_Line_D_" + currentDailyIndex, cisdTime, cisdLevel, Server.TimeInUtc.AddHours(24), cisdLevel, cisdColor, 2, style);
                            Chart.DrawText("CISD_Txt_D_" + currentDailyIndex, statusTxt, cisdTime, cisdLevel, cisdColor);
                        }
                    }
                }
            }

            // 4. --- LÓGICA DO H4 (FASE 2) ---
            string h4PanelText = "";

            if (phase2Active && todayStart != DateTime.MinValue && dailyConfirmIndex != -1)
            {
                DateTime phase2StartTime = (dailyConfirmIndex + 1 < _h1Bars.Count) 
                    ? _h1Bars.OpenTimes[dailyConfirmIndex + 1] 
                    : dailyConfirmTime.AddHours(1);

                int startH4Idx = GetCoveringIndex(_h4Bars, dailyConfirmTime);
                
                if (startH4Idx != -1)
                {
                    for (int h = startH4Idx; h < _h4Bars.Count; h++)
                    {
                        DateTime h4OpenTime = _h4Bars.OpenTimes[h];
                        
                        if (h4OpenTime < todayStart || h4OpenTime >= todayStart.AddHours(24)) continue;
                        
                        if (h4OpenTime.AddHours(4) <= phase2StartTime) continue;

                        int m15Start = GetCoveringIndex(_m15Bars, h4OpenTime);
                        if (m15Start != -1) { while (m15Start < _m15Bars.Count - 1 && _m15Bars.OpenTimes[m15Start] < h4OpenTime) m15Start++; }
                        if (m15Start == -1) continue;

                        int m15End = (h < _h4Bars.Count - 1) ? GetCoveringIndex(_m15Bars, _h4Bars.OpenTimes[h + 1]) - 1 : _m15Bars.Count - 1;
                        if (m15End == -1) m15End = _m15Bars.Count - 1;

                        double h4ExtPrice = isBullBias ? double.MaxValue : double.MinValue;
                        int h4ExtIndex = -1;
                        bool h4Locked = false;
                        bool h4CisdConfirmado = false;
                        bool h4Invalidated = false;
                        double h4CisdLevel = double.NaN;
                        DateTime h4CisdTime = DateTime.MinValue;
                        string h4Status = "Aguardando Pavio...";

                        for (int m = m15Start; m <= m15End; m++)
                        {
                            if (_m15Bars.OpenTimes[m] < phase2StartTime) continue;

                            if (h4Invalidated) break;

                            if (!h4Locked)
                            {
                                if (isBullBias && _m15Bars.LowPrices[m] <= h4ExtPrice) { h4ExtPrice = _m15Bars.LowPrices[m]; h4ExtIndex = m; }
                                else if (!isBullBias && _m15Bars.HighPrices[m] >= h4ExtPrice) { h4ExtPrice = _m15Bars.HighPrices[m]; h4ExtIndex = m; }

                                double evalPrice = isBullBias ? _m15Bars.LowPrices[m] : _m15Bars.HighPrices[m];
                                bool mitigated = false;
                                string poi = "";

                                foreach (var gap in _gapsH1)
                                {
                                    if (gap.ConfirmationTime <= _m15Bars.OpenTimes[m])
                                    {
                                        if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _m15Bars.OpenTimes[m]) continue;
                                        if (isBullBias && gap.IsBullish && evalPrice <= gap.Top) { mitigated = true; poi = "FVG H1"; break; }
                                        if (!isBullBias && !gap.IsBullish && evalPrice >= gap.Bottom) { mitigated = true; poi = "FVG H1"; break; }
                                    }
                                }
                                
                                if (!mitigated)
                                {
                                    foreach (var gap in _gapsM30)
                                    {
                                        if (gap.ConfirmationTime <= _m15Bars.OpenTimes[m])
                                        {
                                            if (gap.IsFullyMitigated && gap.FullyMitigatedTime < _m15Bars.OpenTimes[m]) continue;
                                            if (isBullBias && gap.IsBullish && evalPrice <= gap.Top) { mitigated = true; poi = "FVG M30"; break; }
                                            if (!isBullBias && !gap.IsBullish && evalPrice >= gap.Bottom) { mitigated = true; poi = "FVG M30"; break; }
                                        }
                                    }
                                }

                                if (!mitigated)
                                {
                                    foreach (var liq in _liqM15)
                                    {
                                        if (liq.StartTime < _m15Bars.OpenTimes[m])
                                        {
                                            if (liq.IsMitigated && liq.MitigationTime < _m15Bars.OpenTimes[m]) continue;
                                            if (isBullBias && !liq.IsSwingHigh && evalPrice <= liq.Price) { mitigated = true; poi = "Liq M15"; break; }
                                            if (!isBullBias && liq.IsSwingHigh && evalPrice >= liq.Price) { mitigated = true; poi = "Liq M15"; break; }
                                        }
                                    }
                                }

                                if (mitigated)
                                {
                                    h4Status = "Pendente (" + poi + ")";
                                    h4Locked = true;
                                    h4ExtPrice = evalPrice;
                                    h4ExtIndex = m;
                                }
                            }

                            if (h4Locked)
                            {
                                if (!h4CisdConfirmado)
                                {
                                    if (isBullBias && _m15Bars.LowPrices[m] <= h4ExtPrice) { h4ExtPrice = _m15Bars.LowPrices[m]; h4ExtIndex = m; }
                                    else if (!isBullBias && _m15Bars.HighPrices[m] >= h4ExtPrice) { h4ExtPrice = _m15Bars.HighPrices[m]; h4ExtIndex = m; }

                                    h4CisdLevel = double.NaN;
                                    int limit = Math.Max(m15Start, h4ExtIndex - 48);

                                    if (isBullBias)
                                    {
                                        for (int j = h4ExtIndex - 1; j >= limit; j--)
                                        {
                                            if (j + 1 < _m15Bars.Count && j - 1 >= 0)
                                            {
                                                if (_m15Bars.HighPrices[j] > _m15Bars.HighPrices[j - 1] && _m15Bars.HighPrices[j] > _m15Bars.HighPrices[j + 1])
                                                {
                                                    h4CisdLevel = Math.Max(_m15Bars.OpenPrices[j], _m15Bars.ClosePrices[j]);
                                                    h4CisdTime = _m15Bars.OpenTimes[j];
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        for (int j = h4ExtIndex - 1; j >= limit; j--)
                                        {
                                            if (j + 1 < _m15Bars.Count && j - 1 >= 0)
                                            {
                                                if (_m15Bars.LowPrices[j] < _m15Bars.LowPrices[j - 1] && _m15Bars.LowPrices[j] < _m15Bars.LowPrices[j + 1])
                                                {
                                                    h4CisdLevel = Math.Min(_m15Bars.OpenPrices[j], _m15Bars.ClosePrices[j]);
                                                    h4CisdTime = _m15Bars.OpenTimes[j];
                                                    break;
                                                }
                                            }
                                        }
                                    }

                                    if (double.IsNaN(h4CisdLevel))
                                    {
                                        int fbStart = Math.Max(m15Start, h4ExtIndex - 6);
                                        double fbPrice = isBullBias ? double.MinValue : double.MaxValue;
                                        int fbIdx = h4ExtIndex;

                                        for (int j = fbStart; j < h4ExtIndex; j++)
                                        {
                                            if (isBullBias)
                                            {
                                                double bodyTop = Math.Max(_m15Bars.OpenPrices[j], _m15Bars.ClosePrices[j]);
                                                if (bodyTop > fbPrice) { fbPrice = bodyTop; fbIdx = j; }
                                            }
                                            else
                                            {
                                                double bodyBot = Math.Min(_m15Bars.OpenPrices[j], _m15Bars.ClosePrices[j]);
                                                if (bodyBot < fbPrice) { fbPrice = bodyBot; fbIdx = j; }
                                            }
                                        }

                                        if (fbPrice != double.MinValue && fbPrice != double.MaxValue)
                                        {
                                            h4CisdLevel = fbPrice;
                                            h4CisdTime = _m15Bars.OpenTimes[fbIdx];
                                        }
                                    }

                                    if (!double.IsNaN(h4CisdLevel) && m >= h4ExtIndex)
                                    {
                                        if (isBullBias && _m15Bars.ClosePrices[m] > h4CisdLevel) h4CisdConfirmado = true;
                                        if (!isBullBias && _m15Bars.ClosePrices[m] < h4CisdLevel) h4CisdConfirmado = true;
                                    }
                                }
                                else
                                {
                                    if (isBullBias && _m15Bars.LowPrices[m] < h4ExtPrice) h4Invalidated = true;
                                    if (!isBullBias && _m15Bars.HighPrices[m] > h4ExtPrice) h4Invalidated = true;
                                }
                            }
                        }

                        if (h4ExtIndex != -1) 
                        {
                            if (h4Locked)
                            {
                                if (h4Invalidated) h4Status = "CISD H4 Invalidado";
                                else if (h4CisdConfirmado) h4Status = isBullBias ? "Fundo Confirmado (CISD H4)" : "Topo Confirmado (CISD H4)";
                            }

                            string h4Hour = h4OpenTime.Add(Application.UserTimeOffset).ToString("HH");
                            h4PanelText += $"\nH4({h4Hour}h): {h4Status}";

                            if (h4Locked && !double.IsNaN(h4CisdLevel))
                            {
                                Color h4CisdColor = h4Invalidated ? Color.Gray : (isBullBias ? Color.Yellow : Color.Magenta);
                                LineStyle style = (h4CisdConfirmado && !h4Invalidated) ? LineStyle.Solid : LineStyle.DotsRare;
                                
                                Chart.DrawTrendLine("CISD_Line_H4_" + h, h4CisdTime, h4CisdLevel, Server.TimeInUtc.AddHours(4), h4CisdLevel, h4CisdColor, 2, style);
                                Chart.DrawText("CISD_Txt_H4_" + h, h4CisdConfirmado ? " CISD H4" : " Wait H4", h4CisdTime, h4CisdLevel, h4CisdColor);
                            }
                        }
                    }
                }
            }

            // 5. --- RENDERIZAÇÃO CONDICIONAL (CONTROLE VISUAL) ---
            if (!phase2Active)
            {
                if (ShowH4Fvg) DesenharFVG(_gapsH4, Color.Orange, Color.Purple);
                if (ShowH1Liquidity) DesenharLiquidez(_liqH1);
            }
            else
            {
                if (ShowH1Fvg) DesenharFVG(_gapsH1, Color.LightSkyBlue, Color.HotPink);
                if (ShowM30Fvg) DesenharFVG(_gapsM30, Color.Teal, Color.Maroon);
                if (ShowM15Liquidity) DesenharLiquidez(_liqM15);
            }

            string textoPainel = string.Format("Ativo: {0}\nTendência Diária: {1}\nPavio D: {2}{3}", Symbol.Name, tendencia, pavioStatus, h4PanelText);
            Chart.DrawStaticText("DailyBiasTable", textoPainel, VerticalAlignment.Top, HorizontalAlignment.Right, textColor);
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