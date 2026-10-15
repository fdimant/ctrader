using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum BreakoutMode
    {
        Nao,
        D1,
        H4,
        H1,
        M30,
        M15
    }

    public enum StrategyMode
    {
        Nenhuma,
        Estrategia1_Simples,
        Estrategia2_Reversao,
        Ambas
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
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

        // --- PARÂMETROS VISUAIS & ESTRATÉGIA ---
        [Parameter("Linha de Rompimento", Group = "Visual", DefaultValue = BreakoutMode.Nao)]
        public BreakoutMode LinhaRompimento { get; set; }

        [Parameter("Qtd. Linhas de Rompimento", Group = "Visual", DefaultValue = 3, MinValue = 1, MaxValue = 20)]
        public int QtdLinhasRompimento { get; set; }

        [Parameter("Mostrar Tabela de Viés", Group = "Visual", DefaultValue = true)]
        public bool MostrarTabela { get; set; }

        [Parameter("Estratégia Ativa", Group = "Estratégias", DefaultValue = StrategyMode.Nenhuma)]
        public StrategyMode Estrategia { get; set; }

        // --- SISTEMA DE ALERTAS ---
        [Parameter("Ativar Alertas (Pop-up)", Group = "Alertas", DefaultValue = false)]
        public bool EnableAlerts { get; set; }

        [Parameter("Teste de Janela (A cada vela)", Group = "Alertas", DefaultValue = false)]
        public bool TesteDeJanela { get; set; }

        [Parameter("Ativar Som", Group = "Alertas", DefaultValue = true)]
        public bool EnableSound { get; set; }

        [Parameter("Caminho do Som", Group = "Alertas", DefaultValue = @"C:\Windows\Media\notify.wav")]
        public string SoundPath { get; set; }

        // --- SÉRIES DE DADOS E VARIÁVEIS DE CONTROLE ---
        private Bars _barsD, _barsH4, _barsH1, _barsM30, _barsM15;
        private Bars _barsM5, _barsM3, _barsM1;

        private TextBlock _txtBiasD, _txtBiasH4, _txtBiasH1, _txtBiasM30, _txtBiasM15;
        private TextBlock _txtTrendD, _txtTrendH4, _txtTrendH1, _txtTrendM30, _txtTrendM15;
        
        private List<string> _activeBreakoutLines = new List<string>();
        private DateTime _lastAlertTime;
        private int _lastTestBarIndex = -1;

        protected override void Initialize()
        {
            _lastAlertTime = Server.TimeInUtc;

            _barsD = MarketData.GetBars(TimeFrame.Daily);
            _barsH4 = MarketData.GetBars(TimeFrame.Hour4);
            _barsH1 = MarketData.GetBars(TimeFrame.Hour);
            _barsM30 = MarketData.GetBars(TimeFrame.Minute30);
            _barsM15 = MarketData.GetBars(TimeFrame.Minute15);

            _barsM5 = MarketData.GetBars(TimeFrame.Minute5);
            _barsM3 = MarketData.GetBars(TimeFrame.Minute3);
            _barsM1 = MarketData.GetBars(TimeFrame.Minute);

            if (MostrarTabela)
            {
                var painelBorda = new Border
                {
                    BackgroundColor = cAlgo.API.Color.FromArgb(180, 20, 20, 20),
                    CornerRadius = 3,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(10)
                };

                var stackPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10) };
                
                stackPanel.AddChild(new TextBlock 
                { 
                    Text = $"Ativo: {Symbol.Name}", 
                    ForegroundColor = cAlgo.API.Color.White, 
                    FontFamily = "Consolas", 
                    Margin = new Thickness(0, 0, 0, 5),
                    HorizontalAlignment = HorizontalAlignment.Right
                });

                var grid = new Grid(0, 5);
                
                grid.AddRow();
                grid.AddChild(CriarTextoUI("Timeframe", cAlgo.API.Color.White, HorizontalAlignment.Right, 0, 5), 0, 0);
                grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), 0, 1);
                grid.AddChild(CriarTextoUI("Viés Atual", cAlgo.API.Color.White, HorizontalAlignment.Left, 5, 5), 0, 2);
                grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), 0, 3);
                grid.AddChild(CriarTextoUI("Tendência", cAlgo.API.Color.White, HorizontalAlignment.Left, 5, 0), 0, 4);

                grid.AddRow();
                grid.AddChild(CriarTextoUI("---------", cAlgo.API.Color.White, HorizontalAlignment.Right, 0, 5), 1, 0);
                grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), 1, 1);
                grid.AddChild(CriarTextoUI("-------------------", cAlgo.API.Color.White, HorizontalAlignment.Left, 5, 5), 1, 2);
                grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), 1, 3);
                grid.AddChild(CriarTextoUI("-----------", cAlgo.API.Color.White, HorizontalAlignment.Left, 5, 0), 1, 4);

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
        }

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            // --- SISTEMA DE TESTE DE JANELA E SOM ---
            if (TesteDeJanela && index > _lastTestBarIndex)
            {
                _lastTestBarIndex = index;
                string testMsg = $"TESTE DE JANELA: Nova vela (Index: {index}) no ativo {Symbol.Name}.";
                
                Print(testMsg);

                if (EnableSound)
                {
                    Notifications.PlaySound(SoundPath);
                }

                if (RunningMode == RunningMode.RealTime)
                {
                    BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            MessageBox.Show(testMsg, "Teste cTrader", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            Print($"Erro ao abrir janela: {ex.Message}");
                        }
                    });
                }
            }

            if (MostrarTabela)
            {
                if (MonitorD) AtualizarLinhaTabela(_barsD, _barsH1, _txtBiasD, _txtTrendD, "D1");
                if (MonitorH4) AtualizarLinhaTabela(_barsH4, _barsM15, _txtBiasH4, _txtTrendH4, "H4");
                if (MonitorH1) AtualizarLinhaTabela(_barsH1, _barsM5, _txtBiasH1, _txtTrendH1, "H1");
                if (MonitorM30) AtualizarLinhaTabela(_barsM30, _barsM3, _txtBiasM30, _txtTrendM30, "M30");
                if (MonitorM15) AtualizarLinhaTabela(_barsM15, _barsM1, _txtBiasM15, _txtTrendM15, "M15");
            }

            if (LinhaRompimento == BreakoutMode.Nao)
            {
                ClearBreakoutLines();
            }
            else
            {
                DrawHistoricalBreakouts();
            }
        }

        private void DrawHistoricalBreakouts()
        {
            ClearBreakoutLines();

            Bars htfBars = GetHtfBarsByMode(LinhaRompimento);
            Bars ltfBars = GetLtfBarsByMode(LinhaRompimento);
            string tfLabel = LinhaRompimento.ToString();

            if (htfBars == null || ltfBars == null) return;

            int linesDrawn = 0;
            
            for (int h = htfBars.Count - 1; h >= 1; h--)
            {
                if (linesDrawn >= QtdLinhasRompimento) break;

                DateTime htfStart = htfBars.OpenTimes[h];
                DateTime htfNext = (h + 1 < htfBars.Count) ? htfBars.OpenTimes[h + 1] : DateTime.MaxValue;

                int refLtfIndex = -1;
                for (int i = ltfBars.Count - 1; i >= 0; i--)
                {
                    if (ltfBars.OpenTimes[i] < htfStart)
                    {
                        refLtfIndex = i;
                        break;
                    }
                }

                if (refLtfIndex < 1) continue;

                double topBreak = Math.Max(ltfBars.OpenPrices[refLtfIndex], ltfBars.ClosePrices[refLtfIndex]);
                double bottomBreak = Math.Min(ltfBars.OpenPrices[refLtfIndex], ltfBars.ClosePrices[refLtfIndex]);

                int breakoutIndex = -1;
                double breakoutPrice = double.NaN;
                bool isBullish = false;
                string currentDirection = "Neutra";

                for (int i = refLtfIndex + 1; i < ltfBars.Count; i++)
                {
                    if (ltfBars.OpenTimes[i] >= htfNext) break;
                    if (i == ltfBars.Count - 1) continue;

                    double cOpen = ltfBars.OpenPrices[i];
                    double cClose = ltfBars.ClosePrices[i];
                    double prevHigh = ltfBars.HighPrices[i - 1];
                    double prevLow = ltfBars.LowPrices[i - 1];

                    bool isBullBreak = cClose > topBreak || (cOpen > topBreak && cClose > prevHigh);
                    bool isBearBreak = cClose < bottomBreak || (cOpen < bottomBreak && cClose < prevLow);

                    if (isBullBreak && currentDirection != "Alta")
                    {
                        breakoutIndex = i;
                        breakoutPrice = topBreak;
                        isBullish = true;
                        currentDirection = "Alta";
                    }
                    else if (isBearBreak && currentDirection != "Baixa")
                    {
                        breakoutIndex = i;
                        breakoutPrice = bottomBreak;
                        isBullish = false;
                        currentDirection = "Baixa";
                    }
                }

                if (breakoutIndex != -1)
                {
                    string lineName = $"Breakout_Line_{tfLabel}_{h}";
                    DateTime startTime = ltfBars.OpenTimes[refLtfIndex];
                    DateTime endTime = ltfBars.OpenTimes[breakoutIndex];
                    cAlgo.API.Color lineColor = isBullish ? cAlgo.API.Color.LimeGreen : cAlgo.API.Color.Red;

                    Chart.DrawTrendLine(lineName, startTime, breakoutPrice, endTime, breakoutPrice, lineColor, 2, LineStyle.Solid);
                    _activeBreakoutLines.Add(lineName);

                    string stratRes = EvaluateStrategy(endTime, isBullish, LinhaRompimento);
                    if (!string.IsNullOrEmpty(stratRes))
                    {
                        string txtName = lineName + "_strat";
                        Chart.DrawText(txtName, " " + stratRes, endTime, breakoutPrice, lineColor);
                        _activeBreakoutLines.Add(txtName);
                    }
                    
                    linesDrawn++;
                }
            }
        }

        private void ClearBreakoutLines()
        {
            foreach (var lineName in _activeBreakoutLines)
            {
                Chart.RemoveObject(lineName);
            }
            _activeBreakoutLines.Clear();
            Chart.RemoveObject("Breakout_Line_Trend"); 
            Chart.RemoveObject("Breakout_Line_Trend_strat"); 
        }

        private string EvaluateStrategy(DateTime breakoutTime, bool isBullish, BreakoutMode mode)
        {
            if (Estrategia == StrategyMode.Nenhuma) return "";

            Bars supHtfBars = null;
            Bars supLtfBars = null;

            if (mode == BreakoutMode.M15) { supHtfBars = _barsH1; supLtfBars = _barsM5; }
            else if (mode == BreakoutMode.M30) { supHtfBars = _barsH4; supLtfBars = _barsM15; }
            else if (mode == BreakoutMode.H1) { supHtfBars = _barsH4; supLtfBars = _barsM15; }
            else return ""; 

            Bars baseHtfBars = GetHtfBarsByMode(mode);
            
            int baseIdx = GetIndexByTime(baseHtfBars, breakoutTime);
            int supIdx = GetIndexByTime(supHtfBars, breakoutTime);
            
            if (baseIdx < 0 || supIdx < 0) return "";

            string baseBias = GetBias(baseHtfBars, baseIdx);
            string supBias = GetBias(supHtfBars, supIdx);
            string supTrend = GetTrendAt(supHtfBars, supLtfBars, breakoutTime);

            List<string> stratsAttended = new List<string>();

            if (Estrategia == StrategyMode.Estrategia1_Simples || Estrategia == StrategyMode.Ambas)
            {
                bool baseBiasMatch = isBullish ? baseBias.Contains("Alta") : baseBias.Contains("Baixa");
                bool supTrendMatch = isBullish ? supTrend.Contains("Alta") : supTrend.Contains("Baixa");
                
                bool supBiasNotContrary = isBullish 
                    ? !(supBias == "Reversão para Baixa" || supBias == "Continuação de Baixa")
                    : !(supBias == "Reversão para Alta" || supBias == "Continuação de Alta");

                if (baseBiasMatch && supTrendMatch && supBiasNotContrary)
                {
                    stratsAttended.Add("1");
                }
            }

            if (Estrategia == StrategyMode.Estrategia2_Reversao || Estrategia == StrategyMode.Ambas)
            {
                bool supBiasMatch = isBullish ? supBias.Contains("Alta") : supBias.Contains("Baixa");
                bool supTrendMatch = isBullish ? supTrend.Contains("Alta") : supTrend.Contains("Baixa");
                
                Bars baseLtfBars = GetLtfBarsByMode(mode);
                string baseTrend = GetTrendAt(baseHtfBars, baseLtfBars, breakoutTime);
                bool baseTrendMatch = isBullish ? (baseTrend == "Forte Alta") : (baseTrend == "Forte Baixa");
                
                bool prevHtfContrary = false;
                if (baseIdx > 0)
                {
                    double prevOpen = baseHtfBars.OpenPrices[baseIdx - 1];
                    double prevClose = baseHtfBars.ClosePrices[baseIdx - 1];
                    prevHtfContrary = isBullish ? (prevClose < prevOpen) : (prevClose > prevOpen);
                }

                if (supBiasMatch && supTrendMatch && baseTrendMatch && prevHtfContrary)
                {
                    stratsAttended.Add("2");
                }
            }

            if (stratsAttended.Count > 0)
            {
                string prefix = isBullish ? "c(" : "v(";
                return prefix + string.Join(",", stratsAttended) + ")";
            }

            return "";
        }

        private int GetIndexByTime(Bars bars, DateTime time)
        {
            int idx = bars.OpenTimes.GetIndexByTime(time);
            if (idx != -1) return idx;

            for (int i = bars.Count - 1; i >= 0; i--)
            {
                if (bars.OpenTimes[i] <= time) return i;
            }
            return -1;
        }

        private string GetTrendAt(Bars htfBars, Bars ltfBars, DateTime time)
        {
            int htfIndex = GetIndexByTime(htfBars, time);
            if (htfIndex < 1) return "Neutra";

            int htfPrevIndex = htfIndex - 1;
            double prevHtfLow = htfBars.LowPrices[htfPrevIndex];
            double prevHtfHigh = htfBars.HighPrices[htfPrevIndex];

            DateTime htfCurrentStart = htfBars.OpenTimes[htfIndex];

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

            int limitLtfIndex = GetIndexByTime(ltfBars, time);
            if (limitLtfIndex < 0) limitLtfIndex = ltfBars.Count - 1;

            for (int i = refLtfIndex + 1; i <= limitLtfIndex; i++)
            {
                double cOpen = ltfBars.OpenPrices[i];
                double cClose = ltfBars.ClosePrices[i];
                double cHigh = ltfBars.HighPrices[i];
                double cLow = ltfBars.LowPrices[i];
                double prevHigh = ltfBars.HighPrices[i - 1];
                double prevLow = ltfBars.LowPrices[i - 1];

                if (cLow <= prevHtfLow) sweptPrevHtfLow = true;
                if (cHigh >= prevHtfHigh) sweptPrevHtfHigh = true;

                bool isBullishBreak = cClose > topBreak || (cOpen > topBreak && cClose > prevHigh);
                bool isBearishBreak = cClose < bottomBreak || (cOpen < bottomBreak && cClose < prevLow);

                if (isBullishBreak) tendencia = sweptPrevHtfLow ? "Forte Alta" : "Alta";
                else if (isBearishBreak) tendencia = sweptPrevHtfHigh ? "Forte Baixa" : "Baixa";
            }
            return tendencia;
        }

        private Bars GetHtfBarsByMode(BreakoutMode mode)
        {
            switch (mode)
            {
                case BreakoutMode.D1: return _barsD;
                case BreakoutMode.H4: return _barsH4;
                case BreakoutMode.H1: return _barsH1;
                case BreakoutMode.M30: return _barsM30;
                case BreakoutMode.M15: return _barsM15;
                default: return null;
            }
        }

        private Bars GetLtfBarsByMode(BreakoutMode mode)
        {
            switch (mode)
            {
                case BreakoutMode.D1: return _barsH1;
                case BreakoutMode.H4: return _barsM15;
                case BreakoutMode.H1: return _barsM5;
                case BreakoutMode.M30: return _barsM3;
                case BreakoutMode.M15: return _barsM1;
                default: return null;
            }
        }

        private void AtualizarLinhaTabela(Bars htfBars, Bars ltfBars, TextBlock txtBias, TextBlock txtTrend, string tfLabel)
        {
            if (txtBias == null || txtTrend == null) return;
            
            int htfIndex = htfBars.Count - 1;
            
            string bias = GetBias(htfBars, htfIndex);
            txtBias.Text = bias;
            txtBias.ForegroundColor = GetStatusColor(bias);

            string trend = GetTrend(htfBars, ltfBars, tfLabel);
            txtTrend.Text = trend;
            txtTrend.ForegroundColor = GetStatusColor(trend);
        }

        private void AddGridRow(Grid grid, ref int rowIndex, string tfLabel, out TextBlock biasTextBlock, out TextBlock trendTextBlock)
        {
            grid.AddRow();
            grid.AddChild(CriarTextoUI(tfLabel, cAlgo.API.Color.White, HorizontalAlignment.Right, 0, 5), rowIndex, 0);
            grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), rowIndex, 1);
            
            biasTextBlock = CriarTextoUI("-", cAlgo.API.Color.Gray, HorizontalAlignment.Left, 5, 5);
            grid.AddChild(biasTextBlock, rowIndex, 2);

            grid.AddChild(CriarTextoUI("|", cAlgo.API.Color.White, HorizontalAlignment.Center, 0, 0), rowIndex, 3);

            trendTextBlock = CriarTextoUI("-", cAlgo.API.Color.Gray, HorizontalAlignment.Left, 5, 0);
            grid.AddChild(trendTextBlock, rowIndex, 4);
            
            rowIndex++;
        }

        private TextBlock CriarTextoUI(string texto, cAlgo.API.Color cor, HorizontalAlignment alinhamento, double margemEsquerda, double margemDireita)
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

        private cAlgo.API.Color GetStatusColor(string status)
        {
            if (status.Contains("Alta")) return cAlgo.API.Color.LimeGreen;
            if (status.Contains("Baixa")) return cAlgo.API.Color.Red;
            return cAlgo.API.Color.Gray; 
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

        private string GetTrend(Bars htfBars, Bars ltfBars, string tfLabel)
        {
            if (htfBars.Count < 2 || ltfBars.Count < 2) return "Neutra";

            int htfCurrentIndex = htfBars.Count - 1;
            int htfPrevIndex = htfCurrentIndex - 1;
            
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
            
            int currentBreakoutIndex = -1;
            double currentBreakoutPrice = double.NaN;
            bool currentIsBullish = false;
            string currentDirection = "Neutra"; 

            for (int i = refLtfIndex + 1; i < ltfBars.Count; i++)
            {
                double cOpen = ltfBars.OpenPrices[i];
                double cClose = ltfBars.ClosePrices[i];
                double cHigh = ltfBars.HighPrices[i];
                double cLow = ltfBars.LowPrices[i];
                double prevHigh = ltfBars.HighPrices[i - 1];
                double prevLow = ltfBars.LowPrices[i - 1];

                if (cLow <= prevHtfLow) sweptPrevHtfLow = true;
                if (cHigh >= prevHtfHigh) sweptPrevHtfHigh = true;

                if (i < ltfBars.Count - 1)
                {
                    bool isBullishBreak = cClose > topBreak || (cOpen > topBreak && cClose > prevHigh);
                    bool isBearishBreak = cClose < bottomBreak || (cOpen < bottomBreak && cClose < prevLow);

                    if (isBullishBreak) 
                    {
                        tendencia = sweptPrevHtfLow ? "Forte Alta" : "Alta";
                        
                        if (currentDirection != "Alta")
                        {
                            currentBreakoutIndex = i;
                            currentBreakoutPrice = topBreak;
                            currentIsBullish = true;
                            currentDirection = "Alta";
                        }
                    }
                    else if (isBearishBreak) 
                    {
                        tendencia = sweptPrevHtfHigh ? "Forte Baixa" : "Baixa";
                        
                        if (currentDirection != "Baixa")
                        {
                            currentBreakoutIndex = i;
                            currentBreakoutPrice = bottomBreak;
                            currentIsBullish = false;
                            currentDirection = "Baixa";
                        }
                    }
                }
            }

            if (LinhaRompimento.ToString() == tfLabel)
            {
                string lineName = "Breakout_Line_Trend";
                if (currentBreakoutIndex != -1)
                {
                    DateTime startTime = ltfBars.OpenTimes[refLtfIndex];
                    DateTime endTime = ltfBars.OpenTimes[currentBreakoutIndex];
                    cAlgo.API.Color lineColor = currentIsBullish ? cAlgo.API.Color.LimeGreen : cAlgo.API.Color.Red;
                    
                    Chart.DrawTrendLine(lineName, startTime, currentBreakoutPrice, endTime, currentBreakoutPrice, lineColor, 2, LineStyle.Solid);

                    string stratRes = EvaluateStrategy(endTime, currentIsBullish, LinhaRompimento);
                    if (!string.IsNullOrEmpty(stratRes))
                    {
                        Chart.DrawText(lineName + "_strat", " " + stratRes, endTime, currentBreakoutPrice, lineColor);
                        
                        if (EnableAlerts && endTime > _lastAlertTime)
                        {
                            string operacao = currentIsBullish ? "COMPRA" : "VENDA";
                            string numEstrat = stratRes.Replace(currentIsBullish ? "c(" : "v(", "").Replace(")", "");
                            string alertMessage = string.Format("SINAL DE {0} | Estratégia: {1} | Ativo: {2} | Preço: {3}", operacao, numEstrat, Symbol.Name, currentBreakoutPrice);
                            
                            Print(alertMessage);

                            // Disparo sonoro
                            if (EnableSound)
                            {
                                Notifications.PlaySound(SoundPath);
                            }

                            if (RunningMode == RunningMode.RealTime)
                            {
                                BeginInvokeOnMainThread(() =>
                                {
                                    try 
                                    {
                                        MessageBox.Show(alertMessage.Replace(" | ", "\n"), "Alerta cTrader", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    catch (Exception ex)
                                    {
                                        Print($"Erro ao exibir alerta: {ex.Message}");
                                    }
                                });
                            }

                            _lastAlertTime = endTime;
                        }
                    }
                    else
                    {
                        Chart.RemoveObject(lineName + "_strat");
                    }
                }
                else
                {
                    Chart.RemoveObject(lineName);
                    Chart.RemoveObject(lineName + "_strat");
                }
            }

            return tendencia;
        }
    }
}