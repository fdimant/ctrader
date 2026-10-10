using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MultiTimeframeBias : Indicator
    {
        // --- PARÂMETROS DE TIMEFRAMES ---
        [Parameter("Monitorar Diário", Group = "Timeframes", DefaultValue = true)]
        public bool MonitorD { get; set; }

        [Parameter("Monitorar H4", Group = "Timeframes", DefaultValue = true)]
        public bool MonitorH4 { get; set; }

        [Parameter("Monitorar H1", Group = "Timeframes", DefaultValue = true)]
        public bool MonitorH1 { get; set; }

        [Parameter("Monitorar M30", Group = "Timeframes", DefaultValue = true)]
        public bool MonitorM30 { get; set; }

        [Parameter("Monitorar M15", Group = "Timeframes", DefaultValue = true)]
        public bool MonitorM15 { get; set; }

        // --- SÉRIES DE DADOS (HTF e LTF) ---
        private Bars _barsD, _barsH4, _barsH1, _barsM30, _barsM15;
        private Bars _barsM5, _barsM3, _barsM1;

        // --- VARIÁVEIS DE INTERFACE ---
        private TextBlock _txtBiasD, _txtBiasH4, _txtBiasH1, _txtBiasM30, _txtBiasM15;
        private TextBlock _txtTrendD, _txtTrendH4, _txtTrendH1, _txtTrendM30, _txtTrendM15;

        protected override void Initialize()
        {
            _barsD = MarketData.GetBars(TimeFrame.Daily);
            _barsH4 = MarketData.GetBars(TimeFrame.Hour4);
            _barsH1 = MarketData.GetBars(TimeFrame.Hour);
            _barsM30 = MarketData.GetBars(TimeFrame.Minute30);
            _barsM15 = MarketData.GetBars(TimeFrame.Minute15);

            _barsM5 = MarketData.GetBars(TimeFrame.Minute5);
            _barsM3 = MarketData.GetBars(TimeFrame.Minute3);
            _barsM1 = MarketData.GetBars(TimeFrame.Minute);

            var painelBorda = new Border
            {
                BackgroundColor = Color.FromArgb(180, 20, 20, 20),
                CornerRadius = 3,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10)
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) };
            
            stackPanel.AddChild(new TextBlock 
            { 
                Text = $"Ativo: {Symbol.Name}", 
                ForegroundColor = Color.White, 
                FontFamily = "Consolas", 
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Right
            });

            var grid = new Grid(0, 5);
            
            grid.AddRow();
            grid.AddChild(CriarTextoUI("Timeframe", Color.White, HorizontalAlignment.Right, 0, 5), 0, 0);
            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), 0, 1);
            grid.AddChild(CriarTextoUI("Viés Atual", Color.White, HorizontalAlignment.Left, 5, 5), 0, 2);
            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), 0, 3);
            grid.AddChild(CriarTextoUI("Tendência", Color.White, HorizontalAlignment.Left, 5, 0), 0, 4);

            grid.AddRow();
            grid.AddChild(CriarTextoUI("---------", Color.White, HorizontalAlignment.Right, 0, 5), 1, 0);
            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), 1, 1);
            grid.AddChild(CriarTextoUI("-------------------", Color.White, HorizontalAlignment.Left, 5, 5), 1, 2);
            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), 1, 3);
            grid.AddChild(CriarTextoUI("-----------", Color.White, HorizontalAlignment.Left, 5, 0), 1, 4);

            int rowIndex = 2;

            if (MonitorD) AddGridRow(grid, ref rowIndex, "D1", out _txtBiasD, out _txtTrendD);
            if (MonitorH4) AddGridRow(grid, ref rowIndex, "H4", out _txtBiasH4, out _txtTrendH4);
            if (MonitorH1) AddGridRow(grid, ref rowIndex, "H1", out _txtBiasH1, out _txtTrendH1);
            if (MonitorM30) AddGridRow(grid, ref rowIndex, "M30", out _txtBiasM30, out _txtTrendM30);
            if (MonitorM15) AddGridRow(grid, ref rowIndex, "M15", out _txtBiasM15, out _txtTrendM15);

            stackPanel.AddChild(grid);
            painelBorda.Child = stackPanel;
            Chart.AddControl(painelBorda);
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            if (MonitorD) AtualizarLinhaTabela(_barsD, _barsH1, _txtBiasD, _txtTrendD);
            if (MonitorH4) AtualizarLinhaTabela(_barsH4, _barsM15, _txtBiasH4, _txtTrendH4);
            if (MonitorH1) AtualizarLinhaTabela(_barsH1, _barsM5, _txtBiasH1, _txtTrendH1);
            if (MonitorM30) AtualizarLinhaTabela(_barsM30, _barsM3, _txtBiasM30, _txtTrendM30);
            if (MonitorM15) AtualizarLinhaTabela(_barsM15, _barsM1, _txtBiasM15, _txtTrendM15);
        }

        private void AtualizarLinhaTabela(Bars htfBars, Bars ltfBars, TextBlock txtBias, TextBlock txtTrend)
        {
            if (txtBias == null || txtTrend == null) return;
            
            int htfIndex = htfBars.Count - 1;
            
            string bias = GetBias(htfBars, htfIndex);
            txtBias.Text = bias;
            txtBias.ForegroundColor = GetStatusColor(bias);

            string trend = GetTrend(htfBars, ltfBars);
            txtTrend.Text = trend;
            txtTrend.ForegroundColor = GetStatusColor(trend);
        }

        private void AddGridRow(Grid grid, ref int rowIndex, string tfLabel, out TextBlock biasTextBlock, out TextBlock trendTextBlock)
        {
            grid.AddRow();
            grid.AddChild(CriarTextoUI(tfLabel, Color.White, HorizontalAlignment.Right, 0, 5), rowIndex, 0);
            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), rowIndex, 1);
            
            biasTextBlock = CriarTextoUI("-", Color.Gray, HorizontalAlignment.Left, 5, 5);
            grid.AddChild(biasTextBlock, rowIndex, 2);

            grid.AddChild(CriarTextoUI("|", Color.White, HorizontalAlignment.Center, 0, 0), rowIndex, 3);

            trendTextBlock = CriarTextoUI("-", Color.Gray, HorizontalAlignment.Left, 5, 0);
            grid.AddChild(trendTextBlock, rowIndex, 4);
            
            rowIndex++;
        }

        private TextBlock CriarTextoUI(string texto, Color cor, HorizontalAlignment alinhamento, double margemEsquerda, double margemDireita)
        {
            return new TextBlock
            {
                Text = texto,
                ForegroundColor = cor,
                FontFamily = "Consolas",
                HorizontalAlignment = alinhamento,
                Margin = new Thickness(margemEsquerda, 0, margemDireita, 0)
            };
        }

        private Color GetStatusColor(string status)
        {
            if (status.Contains("Alta")) return Color.LimeGreen;
            if (status.Contains("Baixa")) return Color.Red;
            return Color.Gray; 
        }

        private string GetBias(Bars bars, int currentIndex)
        {
            if (currentIndex < 2) return "Viés Lateral";

            int b1 = currentIndex - 1;
            int b2 = currentIndex - 2;

            double b2Open = bars.OpenPrices[b2];
            double b2Close = bars.ClosePrices[b2];
            double b2High = bars.HighPrices[b2];
            double b2Low = bars.LowPrices[b2];

            double b1Open = bars.OpenPrices[b1];
            double b1Close = bars.ClosePrices[b1];
            double b1High = bars.HighPrices[b1];
            double b1Low = bars.LowPrices[b1];

            bool b2IsBullish = b2Close > b2Open;
            bool b2IsBearish = b2Close < b2Open;
            bool b1IsBullish = b1Close > b1Open;
            bool b1IsBearish = b1Close < b1Open;

            if (b2IsBullish)
            {
                if (b1Close > b2High) return "Continuação de Alta";
                if (b1IsBearish && b1High > b2Close && b1Close < b2Low) return "Reversão para Baixa";
                if (b1High > b2High && b1Close < b2Close) return "Reversão para Baixa";
            }
            else if (b2IsBearish)
            {
                if (b1Close < b2Low) return "Continuação de Baixa";
                if (b1IsBullish && b1Low < b2Close && b1Close > b2High) return "Reversão para Alta";
                if (b1Low < b2Low && b1Close > b2Close) return "Reversão para Alta";
            }

            return "Viés Lateral";
        }

        private string GetTrend(Bars htfBars, Bars ltfBars)
        {
            if (htfBars.Count < 2 || ltfBars.Count < 2) return "Neutra";

            int htfCurrentIndex = htfBars.Count - 1;
            int htfPrevIndex = htfCurrentIndex - 1;
            
            // Extremos do candle anterior do HTF para validar a captura de liquidez
            double prevHtfLow = htfBars.LowPrices[htfPrevIndex];
            double prevHtfHigh = htfBars.HighPrices[htfPrevIndex];

            DateTime htfCurrentStart = htfBars.OpenTimes[htfCurrentIndex];

            int refLtfIndex = -1;
            for (int i = ltfBars.Count - 1; i >= 0; i--)
            {
                if (ltfBars.OpenTimes[i] < htfCurrentStart)
                {
                    refLtfIndex = i;
                    break;
                }
            }

            if (refLtfIndex < 1) return "Neutra";

            double topBreak = Math.Max(ltfBars.OpenPrices[refLtfIndex], ltfBars.ClosePrices[refLtfIndex]);
            double bottomBreak = Math.Min(ltfBars.OpenPrices[refLtfIndex], ltfBars.ClosePrices[refLtfIndex]);

            string tendencia = "Neutra";
            bool sweptPrevHtfLow = false;
            bool sweptPrevHtfHigh = false;

            // Laço cronológico - a cada vela ele atualiza a visão mais recente do mercado
            for (int i = refLtfIndex + 1; i < ltfBars.Count; i++)
            {
                double cOpen = ltfBars.OpenPrices[i];
                double cClose = ltfBars.ClosePrices[i];
                double cHigh = ltfBars.HighPrices[i];
                double cLow = ltfBars.LowPrices[i];
                double prevHigh = ltfBars.HighPrices[i - 1];
                double prevLow = ltfBars.LowPrices[i - 1];

                // 1. Verifica captura de liquidez do HTF (Agora com <= e >= para capturas exatas/milimétricas)
                if (cLow <= prevHtfLow) sweptPrevHtfLow = true;
                if (cHigh >= prevHtfHigh) sweptPrevHtfHigh = true;

                // 2. Avaliações de rompimentos consolidadas (Fechamento limpo OU Gap com fechamento acima da máxima/mínima anterior)
                bool isBullishBreak = cClose > topBreak || (cOpen > topBreak && cClose > prevHigh);
                bool isBearishBreak = cClose < bottomBreak || (cOpen < bottomBreak && cClose < prevLow);

                if (isBullishBreak) 
                {
                    tendencia = sweptPrevHtfLow ? "Forte Alta" : "Alta";
                }
                else if (isBearishBreak) 
                {
                    tendencia = sweptPrevHtfHigh ? "Forte Baixa" : "Baixa";
                }
                // Se nenhum romper com o corpo ou gap, a tendência anterior apenas se mantém inalterada (esperando o próximo candle).
            }

            return tendencia;
        }
    }
}