using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SyntheticDXYCandlesOverlay : Indicator
    {
        [Parameter("Inverter Gráfico", DefaultValue = false)]
        public bool InvertChart { get; set; }

        // Uso do tipo Color para ativar o seletor nativo de cores do cTrader
        [Parameter("Cor de Alta", DefaultValue = "Green")]
        public Color BullColor { get; set; }

        [Parameter("Cor de Baixa", DefaultValue = "Red")]
        public Color BearColor { get; set; }

        [Parameter("Espessura do Corpo", DefaultValue = 6, MinValue = 1, MaxValue = 25)]
        public int BodyThickness { get; set; }

        private IndicatorDataSeries _dxyOpen;
        private IndicatorDataSeries _dxyHigh;
        private IndicatorDataSeries _dxyLow;
        private IndicatorDataSeries _dxyClose;

        private Bars _eurusd, _usdjpy, _gbpusd, _usdcad, _usdchf;

        protected override void Initialize()
        {
            _dxyOpen = CreateDataSeries();
            _dxyHigh = CreateDataSeries();
            _dxyLow = CreateDataSeries();
            _dxyClose = CreateDataSeries();

            _eurusd = MarketData.GetBars(TimeFrame, "EURUSD");
            _usdjpy = MarketData.GetBars(TimeFrame, "USDJPY");
            _gbpusd = MarketData.GetBars(TimeFrame, "GBPUSD");
            _usdcad = MarketData.GetBars(TimeFrame, "USDCAD");
            _usdchf = MarketData.GetBars(TimeFrame, "USDCHF");

            Chart.ScrollChanged += OnChartScrollChanged;
        }

        private void OnChartScrollChanged(ChartScrollEventArgs args)
        {
            RedrawVisibleCandles();
        }

        public override void Calculate(int index)
        {
            DateTime currentTime = Bars.OpenTimes[index];

            int eurIndex = _eurusd.OpenTimes.GetIndexByTime(currentTime);
            int jpyIndex = _usdjpy.OpenTimes.GetIndexByTime(currentTime);
            int gbpIndex = _gbpusd.OpenTimes.GetIndexByTime(currentTime);
            int cadIndex = _usdcad.OpenTimes.GetIndexByTime(currentTime);
            int chfIndex = _usdchf.OpenTimes.GetIndexByTime(currentTime);

            eurIndex = (eurIndex != -1) ? eurIndex : _eurusd.ClosePrices.Count - 1;
            jpyIndex = (jpyIndex != -1) ? jpyIndex : _usdjpy.ClosePrices.Count - 1;
            gbpIndex = (gbpIndex != -1) ? gbpIndex : _gbpusd.ClosePrices.Count - 1;
            cadIndex = (cadIndex != -1) ? cadIndex : _usdcad.ClosePrices.Count - 1;
            chfIndex = (chfIndex != -1) ? chfIndex : _usdchf.ClosePrices.Count - 1;

            double open = CalculateDxy(_eurusd.OpenPrices[eurIndex], _usdjpy.OpenPrices[jpyIndex], _gbpusd.OpenPrices[gbpIndex], _usdcad.OpenPrices[cadIndex], _usdchf.OpenPrices[chfIndex], InvertChart);
            double high = CalculateDxy(_eurusd.LowPrices[eurIndex], _usdjpy.HighPrices[jpyIndex], _gbpusd.LowPrices[gbpIndex], _usdcad.HighPrices[cadIndex], _usdchf.HighPrices[chfIndex], InvertChart);
            double low = CalculateDxy(_eurusd.HighPrices[eurIndex], _usdjpy.LowPrices[jpyIndex], _gbpusd.LowPrices[gbpIndex], _usdcad.HighPrices[cadIndex], _usdchf.HighPrices[chfIndex], InvertChart);
            double close = CalculateDxy(_eurusd.ClosePrices[eurIndex], _usdjpy.ClosePrices[jpyIndex], _gbpusd.ClosePrices[gbpIndex], _usdcad.ClosePrices[cadIndex], _usdchf.ClosePrices[chfIndex], InvertChart);

            _dxyOpen[index] = open;
            _dxyHigh[index] = Math.Max(open, Math.Max(close, Math.Max(high, low)));
            _dxyLow[index] = Math.Min(open, Math.Min(close, Math.Min(high, low)));
            _dxyClose[index] = close;

            if (IsLastBar)
            {
                RedrawVisibleCandles();
            }
        }

        private void RedrawVisibleCandles()
        {
            Chart.RemoveAllObjects();

            int startIndex = Math.Max(0, Chart.FirstVisibleBarIndex - 2);
            int endIndex = Math.Min(Bars.Count - 1, Chart.LastVisibleBarIndex + 2);

            if (startIndex >= endIndex || double.IsNaN(_dxyClose[startIndex]))
                return;

            double chartBasePrice = Bars.ClosePrices[startIndex];
            double dxyBasePrice = _dxyClose[startIndex];

            if (dxyBasePrice == 0) return;

            double scaleRatio = chartBasePrice / dxyBasePrice;

            for (int i = startIndex; i <= endIndex; i++)
            {
                if (double.IsNaN(_dxyClose[i])) continue;

                double open = _dxyOpen[i] * scaleRatio;
                double high = _dxyHigh[i] * scaleRatio;
                double low = _dxyLow[i] * scaleRatio;
                double close = _dxyClose[i] * scaleRatio;

                // Atribuição direta da cor selecionada no painel
                Color color = (close >= open) ? BullColor : BearColor;

                // 1. Pavio
                Chart.DrawTrendLine("W_" + i, i, high, i, low, color, 1, LineStyle.Solid);

                // 2. Corpo
                double top = open;
                double bottom = close;

                if (Math.Abs(top - bottom) < 0.00001)
                {
                    top += 0.0001;
                }

                Chart.DrawTrendLine("B_" + i, i, top, i, bottom, color, BodyThickness, LineStyle.Solid);
            }
        }

        private double CalculateDxy(double eurusd, double usdjpy, double gbpusd, double usdcad, double usdchf, bool invert)
        {
            if (eurusd == 0 || usdjpy == 0 || gbpusd == 0 || usdcad == 0 || usdchf == 0)
                return 0;

            double dxy = 50.315
                * Math.Pow(eurusd, -0.601) 
                * Math.Pow(usdjpy, 0.142) 
                * Math.Pow(gbpusd, -0.124) 
                * Math.Pow(usdcad, 0.095) 
                * Math.Pow(usdchf, 0.038);

            return invert ? (100 / dxy) : dxy;
        }
    }
}