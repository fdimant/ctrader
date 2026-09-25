using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum HeaderPosition
    {
        Acima,
        Abaixo,
        Ambos,
        Oculto
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class LateralHTFDashboard : Indicator
    {
        // --- Parâmetros Principais ---
        [Parameter("Níveis de HTF Acima", Group = "Configuração HTF", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int LevelsAbove { get; set; }

        [Parameter("Quantidade de Velas por TF", Group = "Configuração HTF", DefaultValue = 5, MinValue = 1, MaxValue = 20)]
        public int CandlesPerTimeframe { get; set; }

        [Parameter("Início do Dashboard (Offset)", Group = "Configuração HTF", DefaultValue = 10, MinValue = 1)]
        public int StartOffset { get; set; }

        [Parameter("Espaçamento entre Velas", Group = "Configuração HTF", DefaultValue = 2, MinValue = 1)]
        public int SpacingCandles { get; set; }

        [Parameter("Espaçamento entre TFs", Group = "Configuração HTF", DefaultValue = 8, MinValue = 2)]
        public int SpacingTFs { get; set; }

        [Parameter("Mostrar Linha Divisória", Group = "Configuração HTF", DefaultValue = true)]
        public bool ShowStartLine { get; set; }

        [Parameter("Cor da Divisória", Group = "Configuração HTF", DefaultValue = "Gray")]
        public Color StartLineColor { get; set; }
        
        [Parameter("Mostrar Linhas de Início HTF", Group = "Configuração HTF", DefaultValue = true)]
        public bool ShowHtfStartLines { get; set; }

        // --- Estilização Geral (Painel Lateral) ---
        [Parameter("Cor de Alta (Geral)", Group = "Estilo HTF Geral", DefaultValue = "MediumSeaGreen")]
        public Color BullColor { get; set; }

        [Parameter("Cor de Baixa (Geral)", Group = "Estilo HTF Geral", DefaultValue = "Tomato")]
        public Color BearColor { get; set; }

        [Parameter("Opacidade Geral (0-255)", Group = "Estilo HTF Geral", DefaultValue = 200, MinValue = 10, MaxValue = 255)]
        public int BodyOpacity { get; set; }

        [Parameter("Espessura do Corpo", Group = "Estilo HTF Geral", DefaultValue = 8, MinValue = 1, MaxValue = 50)]
        public int BodyThickness { get; set; }

        [Parameter("Espessura do Pavio", Group = "Estilo HTF Geral", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int WickThickness { get; set; }

        // --- Cores das Linhas Verticais HTF ---
        [Parameter("Cor Linha M15", Group = "Linhas Verticais HTF", DefaultValue = "Gray")]
        public Color VLineColorM15 { get; set; }

        [Parameter("Cor Linha M30", Group = "Linhas Verticais HTF", DefaultValue = "White")]
        public Color VLineColorM30 { get; set; }

        [Parameter("Cor Linha H1", Group = "Linhas Verticais HTF", DefaultValue = "DodgerBlue")]
        public Color VLineColorH1 { get; set; }

        [Parameter("Cor Linha H4", Group = "Linhas Verticais HTF", DefaultValue = "Orange")]
        public Color VLineColorH4 { get; set; }

        [Parameter("Cor Linha Diário", Group = "Linhas Verticais HTF", DefaultValue = "Magenta")]
        public Color VLineColorD1 { get; set; }

        [Parameter("Cor Linha Semanal", Group = "Linhas Verticais HTF", DefaultValue = "Yellow")]
        public Color VLineColorW1 { get; set; }

        [Parameter("Cor Linha Mensal", Group = "Linhas Verticais HTF", DefaultValue = "Lime")]
        public Color VLineColorMN1 { get; set; }

        // --- Parâmetros de Labels e Tempo Restante ---
        [Parameter("Posição do Cabeçalho", Group = "Labels HTF", DefaultValue = HeaderPosition.Acima)]
        public HeaderPosition CabecalhoPosition { get; set; }

        [Parameter("Mostrar Tempo Restante", Group = "Labels HTF", DefaultValue = true)]
        public bool ShowRemainingTime { get; set; }

        [Parameter("Tamanho da Fonte", Group = "Labels HTF", DefaultValue = 11, MinValue = 6, MaxValue = 16)]
        public int LabelFontSize { get; set; }

        [Parameter("Cor dos Rótulos (Painel)", Group = "Labels HTF", DefaultValue = "White")]
        public Color LabelColor { get; set; }

        // --- Parâmetros PDH / PDL ---
        [Parameter("Mostrar Máx/Mín Dia Anterior", Group = "PDH / PDL", DefaultValue = true)]
        public bool ShowPdhPdl { get; set; }

        [Parameter("Dias PDH/PDL a Exibir", Group = "PDH / PDL", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int PdhPdlDaysCount { get; set; }

        [Parameter("Cor da Máxima (PDH)", Group = "PDH / PDL", DefaultValue = "DodgerBlue")]
        public Color PdhColor { get; set; }

        [Parameter("Cor da Mínima (PDL)", Group = "PDH / PDL", DefaultValue = "DeepPink")]
        public Color PdlColor { get; set; }

        [Parameter("Cor Mitigada", Group = "PDH / PDL", DefaultValue = "Gray")]
        public Color PdhPdlMitigatedColor { get; set; }

        [Parameter("Espessura da Linha", Group = "PDH / PDL", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int PdhPdlThickness { get; set; }

        [Parameter("Estilo da Linha", Group = "PDH / PDL", DefaultValue = LineStyle.Lines)]
        public LineStyle PdhPdlStyle { get; set; }

        private List<Bars> _htfBarsList = new List<Bars>();
        private Bars _dailyBars;

        protected override void Initialize()
        {
            TimeFrame currentTf = TimeFrame;

            for (int i = 0; i < LevelsAbove; i++)
            {
                TimeFrame nextTf = GetNextTimeFrame(currentTf);
                
                if (nextTf == currentTf)
                {
                    break;
                }

                _htfBarsList.Add(MarketData.GetBars(nextTf));
                currentTf = nextTf;
            }

            _dailyBars = MarketData.GetBars(TimeFrame.Daily);
        }

        private TimeFrame GetNextTimeFrame(TimeFrame tf)
        {
            if (tf < TimeFrame.Hour) return TimeFrame.Hour;
            if (tf == TimeFrame.Hour) return TimeFrame.Hour4;
            if (tf == TimeFrame.Hour4) return TimeFrame.Daily;
            if (tf == TimeFrame.Daily) return TimeFrame.Weekly;
            if (tf == TimeFrame.Weekly) return TimeFrame.Monthly;
            
            return tf;
        }

        private string GetTfLabel(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute15) return "M15";
            if (tf == TimeFrame.Minute30) return "M30";
            if (tf == TimeFrame.Hour) return "1H";
            if (tf == TimeFrame.Hour4) return "4H";
            if (tf == TimeFrame.Daily) return "1D";
            if (tf == TimeFrame.Weekly) return "1W";
            if (tf == TimeFrame.Monthly) return "1M";
            return tf.ToString();
        }

        private Color GetVLineColor(TimeFrame tf)
        {
            if (tf == TimeFrame.Minute15) return VLineColorM15;
            if (tf == TimeFrame.Minute30) return VLineColorM30;
            if (tf == TimeFrame.Hour) return VLineColorH1;
            if (tf == TimeFrame.Hour4) return VLineColorH4;
            if (tf == TimeFrame.Daily) return VLineColorD1;
            if (tf == TimeFrame.Weekly) return VLineColorW1;
            if (tf == TimeFrame.Monthly) return VLineColorMN1;
            return Color.Gray;
        }

        public override void Calculate(int index)
        {
            if (ShowPdhPdl && _dailyBars != null && _dailyBars.Count > 1)
            {
                int lastDailyIndex = _dailyBars.Count - 1;
                int startDailyIndex = Math.Max(1, lastDailyIndex - PdhPdlDaysCount + 1);

                int expiredDailyIndex = startDailyIndex - 1;
                Chart.RemoveObject("PDH_Line_" + expiredDailyIndex);
                Chart.RemoveObject("PDL_Line_" + expiredDailyIndex);
                Chart.RemoveObject("PDH_Txt_" + expiredDailyIndex);
                Chart.RemoveObject("PDL_Txt_" + expiredDailyIndex);

                for (int d = startDailyIndex; d <= lastDailyIndex; d++)
                {
                    DrawPdhPdl(d);
                }
            }

            if (!IsLastBar || _htfBarsList.Count == 0) return;

            // Limpa renderizações anteriores
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name.StartsWith("HTF_Lat_") || obj.Name.StartsWith("HTF_VLine_") || obj.Name.StartsWith("HTF_VLineTxt_"))
                    Chart.RemoveObject(obj.Name);
            }

            // --- LINHA DIVISÓRIA VERTICAL ---
            if (ShowStartLine && Bars.Count > 1)
            {
                TimeSpan ltfDuration = Bars.OpenTimes.LastValue - Bars.OpenTimes[Bars.Count - 2];
                DateTime separatorTime = Bars.OpenTimes.LastValue.Add(TimeSpan.FromTicks(ltfDuration.Ticks * (StartOffset - 1)));
                
                Chart.DrawVerticalLine("HTF_Lat_StartLine", separatorTime, StartLineColor, 1, LineStyle.DotsRare);
            }

            int currentDrawIndex = Bars.Count + StartOffset;

            var vLineLabels = new Dictionary<DateTime, List<string>>();
            var vLineColors = new Dictionary<DateTime, Color>();

            for (int level = 0; level < _htfBarsList.Count; level++)
            {
                Bars htfBars = _htfBarsList[level];
                if (htfBars == null || htfBars.Count == 0) continue;

                int lastHtfIndex = htfBars.Count - 1;
                int startHtfIndex = Math.Max(0, lastHtfIndex - CandlesPerTimeframe + 1);

                // Armazena a máxima e mínima exata de todo o bloco de velas desse TF
                double maxBlockHigh = double.MinValue;
                double minBlockLow = double.MaxValue;
                int startBlockIndex = currentDrawIndex;

                for (int i = startHtfIndex; i <= lastHtfIndex; i++)
                {
                    double open = htfBars.OpenPrices[i];
                    double high = htfBars.HighPrices[i];
                    double low = htfBars.LowPrices[i];
                    double close = htfBars.ClosePrices[i];
                    DateTime openTime = htfBars.OpenTimes[i];

                    maxBlockHigh = Math.Max(maxBlockHigh, high);
                    minBlockLow = Math.Min(minBlockLow, low);

                    bool isBull = close >= open;
                    Color baseColor = isBull ? BullColor : BearColor;
                    Color unifiedColor = Color.FromArgb((byte)BodyOpacity, baseColor.R, baseColor.G, baseColor.B);

                    string wickName = $"HTF_Lat_Wick_{level}_{i}";
                    string bodyName = $"HTF_Lat_Body_{level}_{i}";

                    Chart.DrawTrendLine(wickName, currentDrawIndex, high, currentDrawIndex, low, unifiedColor, WickThickness, LineStyle.Solid);

                    double top = Math.Max(open, close);
                    double bottom = Math.Min(open, close);
                    if (top == bottom) top += Symbol.PipSize * 0.1;

                    Chart.DrawTrendLine(bodyName, currentDrawIndex, top, currentDrawIndex, bottom, unifiedColor, BodyThickness, LineStyle.Solid);

                    // --- COLETA DE DADOS PARA AS LINHAS VERTICAIS USANDO A COR ESPECÍFICA DO TF ---
                    if (ShowHtfStartLines)
                    {
                        Color vLineColor = GetVLineColor(htfBars.TimeFrame);
                        string vLineName = $"HTF_VLine_{level}_{i}";
                        Chart.DrawVerticalLine(vLineName, openTime, vLineColor, 1, LineStyle.DotsRare);

                        if (!vLineLabels.ContainsKey(openTime))
                        {
                            vLineLabels[openTime] = new List<string>();
                        }
                        
                        vLineLabels[openTime].Add(GetTfLabel(htfBars.TimeFrame));
                        vLineColors[openTime] = vLineColor; 
                    }

                    currentDrawIndex += SpacingCandles;
                }

                int endBlockIndex = currentDrawIndex - SpacingCandles;
                int centerIndex = startBlockIndex + ((endBlockIndex - startBlockIndex) / 2);

                // --- CABEÇALHO DO PAINEL LATERAL (ACIMA, ABAIXO OU AMBOS) ---
                if (CabecalhoPosition != HeaderPosition.Oculto)
                {
                    string tfLabel = GetTfLabel(htfBars.TimeFrame);
                    string headerText = tfLabel;

                    if (ShowRemainingTime)
                    {
                        TimeSpan tfDuration = (lastHtfIndex > 0) 
                            ? (htfBars.OpenTimes[lastHtfIndex] - htfBars.OpenTimes[lastHtfIndex - 1]) 
                            : TimeSpan.FromHours(1);

                        DateTime closeTime = htfBars.OpenTimes[lastHtfIndex].Add(tfDuration);
                        TimeSpan remaining = closeTime - Server.Time;

                        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

                        string timeFmt = remaining.TotalHours >= 1 
                            ? $"({(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2})" 
                            : $"({remaining.Minutes:D2}:{remaining.Seconds:D2})";

                        headerText = $"{tfLabel}\n{timeFmt}";
                    }

                    string lblName = $"HTF_Lat_Lbl_{level}";

                    if (CabecalhoPosition == HeaderPosition.Acima || CabecalhoPosition == HeaderPosition.Ambos)
                    {
                        var labelTop = Chart.DrawText(lblName + "_top", headerText, centerIndex, maxBlockHigh, LabelColor);
                        labelTop.HorizontalAlignment = HorizontalAlignment.Center;
                        labelTop.VerticalAlignment = VerticalAlignment.Bottom; 
                        labelTop.FontSize = LabelFontSize;
                    }

                    if (CabecalhoPosition == HeaderPosition.Abaixo || CabecalhoPosition == HeaderPosition.Ambos)
                    {
                        var labelBot = Chart.DrawText(lblName + "_bot", headerText, centerIndex, minBlockLow, LabelColor);
                        labelBot.HorizontalAlignment = HorizontalAlignment.Center;
                        labelBot.VerticalAlignment = VerticalAlignment.Top; 
                        labelBot.FontSize = LabelFontSize;
                    }
                }

                currentDrawIndex += SpacingTFs;
            }

            // --- DESENHO DOS RÓTULOS DAS LINHAS VERTICAIS (ALINHADOS PELA MÍNIMA DO DIA) ---
            if (ShowHtfStartLines)
            {
                foreach (var kvp in vLineLabels)
                {
                    DateTime time = kvp.Key;
                    
                    string combinedLabels = string.Join(" / ", kvp.Value);
                    Color clr = vLineColors[time];

                    int dIndex = _dailyBars.OpenTimes.GetIndexByTime(time);
                    if (dIndex == -1)
                    {
                        for (int j = _dailyBars.Count - 1; j >= 0; j--)
                        {
                            if (_dailyBars.OpenTimes[j] <= time)
                            {
                                dIndex = j;
                                break;
                            }
                        }
                    }

                    double referenceLow = dIndex != -1 ? _dailyBars.LowPrices[dIndex] : Bars.LowPrices.LastValue;
                    
                    double yPos = referenceLow - (Symbol.PipSize * 5);

                    string vLineTxtName = $"HTF_VLineTxt_{time.Ticks}";
                    var txtObj = Chart.DrawText(vLineTxtName, combinedLabels, time, yPos, clr);
                    
                    txtObj.FontSize = Math.Max(6, LabelFontSize - 2);
                    txtObj.VerticalAlignment = VerticalAlignment.Top;
                    txtObj.HorizontalAlignment = HorizontalAlignment.Center; 
                }
            }
        }

        private void DrawPdhPdl(int dailyIndex)
        {
            if (dailyIndex < 1) return;

            double pdh = _dailyBars.HighPrices[dailyIndex - 1];
            double pdl = _dailyBars.LowPrices[dailyIndex - 1];

            DateTime todayStart = _dailyBars.OpenTimes[dailyIndex];
            
            int ltfStartIndex = Bars.OpenTimes.GetIndexByTime(todayStart);
            if (ltfStartIndex == -1) return; 

            int ltfEndIndex = Bars.Count - 1;
            DateTime nextDayStart = (dailyIndex < _dailyBars.Count - 1) ? _dailyBars.OpenTimes[dailyIndex + 1] : todayStart.AddDays(1);
            
            int nextDayLtfIndex = Bars.OpenTimes.GetIndexByTime(nextDayStart);
            if (nextDayLtfIndex != -1)
            {
                ltfEndIndex = Math.Max(ltfStartIndex, nextDayLtfIndex - 1);
            }

            bool pdhMitigated = false;
            bool pdlMitigated = false;

            for (int i = ltfStartIndex; i <= ltfEndIndex; i++)
            {
                if (Bars.HighPrices[i] >= pdh) pdhMitigated = true;
                if (Bars.LowPrices[i] <= pdl) pdlMitigated = true;
                if (pdhMitigated && pdlMitigated) break;
            }
            
            if (dailyIndex == _dailyBars.Count - 1)
            {
                if (Symbol.Bid <= pdl) pdlMitigated = true;
                if (Symbol.Ask >= pdh) pdhMitigated = true;
            }

            Color colorPdh = pdhMitigated ? PdhPdlMitigatedColor : PdhColor;
            Color colorPdl = pdlMitigated ? PdhPdlMitigatedColor : PdlColor;

            string pdhName = "PDH_Line_" + dailyIndex;
            string pdlName = "PDL_Line_" + dailyIndex;
            string pdhTxtName = "PDH_Txt_" + dailyIndex;
            string pdlTxtName = "PDL_Txt_" + dailyIndex;

            DateTime drawStart = Bars.OpenTimes[ltfStartIndex];
            DateTime drawEnd = Bars.OpenTimes[ltfEndIndex];
            
            if (dailyIndex == _dailyBars.Count - 1)
            {
                drawEnd = drawStart.AddDays(1); 
            }

            Chart.DrawTrendLine(pdhName, drawStart, pdh, drawEnd, pdh, colorPdh, PdhPdlThickness, PdhPdlStyle);
            Chart.DrawTrendLine(pdlName, drawStart, pdl, drawEnd, pdl, colorPdl, PdhPdlThickness, PdhPdlStyle);
            
            if (CabecalhoPosition != HeaderPosition.Oculto)
            {
                var pdhTxt = Chart.DrawText(pdhTxtName, " PDH", drawStart, pdh, colorPdh);
                var pdlTxt = Chart.DrawText(pdlTxtName, " PDL", drawStart, pdl, colorPdl);
                
                pdhTxt.VerticalAlignment = VerticalAlignment.Bottom;
                pdlTxt.VerticalAlignment = VerticalAlignment.Top;
            }
        }
    }
}