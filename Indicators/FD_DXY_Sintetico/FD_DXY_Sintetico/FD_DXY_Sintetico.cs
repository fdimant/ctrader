using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    // Voltamos para IsOverlay = false (Painel Separado)
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SyntheticDXYBarsColors : Indicator
    {
        [Parameter("Inverter Gráfico", DefaultValue = false)]
        public bool InvertChart { get; set; }

        [Parameter("Cor de Alta", DefaultValue = "Green")]
        public string BullColor { get; set; }

        [Parameter("Cor de Baixa", DefaultValue = "Red")]
        public string BearColor { get; set; }

        // Série oculta necessária apenas para o cTrader ajustar o zoom automático da janela inferior
        [Output("DXY Scale", LineColor = "Transparent")]
        public IndicatorDataSeries DxyScale { get; set; }

        private Bars _eurusd, _usdjpy, _gbpusd, _usdcad, _usdchf;

        protected override void Initialize()
        {
            _eurusd = MarketData.GetBars(TimeFrame, "EURUSD");
            _usdjpy = MarketData.GetBars(TimeFrame, "USDJPY");
            _gbpusd = MarketData.GetBars(TimeFrame, "GBPUSD");
            _usdcad = MarketData.GetBars(TimeFrame, "USDCAD");
            _usdchf = MarketData.GetBars(TimeFrame, "USDCHF");
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

            double open, high, low, close;

            if (InvertChart)
            {
                open = CalculateDxy(_eurusd.OpenPrices[eurIndex], _usdjpy.OpenPrices[jpyIndex], _gbpusd.OpenPrices[gbpIndex], _usdcad.OpenPrices[cadIndex], _usdchf.OpenPrices[chfIndex], true);
                high = CalculateDxy(_eurusd.LowPrices[eurIndex], _usdjpy.HighPrices[jpyIndex], _gbpusd.LowPrices[gbpIndex], _usdcad.HighPrices[cadIndex], _usdchf.HighPrices[chfIndex], true);
                low = CalculateDxy(_eurusd.HighPrices[eurIndex], _usdjpy.LowPrices[jpyIndex], _gbpusd.HighPrices[gbpIndex], _usdcad.LowPrices[cadIndex], _usdchf.LowPrices[chfIndex], true);
                close = CalculateDxy(_eurusd.ClosePrices[eurIndex], _usdjpy.ClosePrices[jpyIndex], _gbpusd.ClosePrices[gbpIndex], _usdcad.ClosePrices[cadIndex], _usdchf.ClosePrices[chfIndex], true);
            }
            else
            {
                open = CalculateDxy(_eurusd.OpenPrices[eurIndex], _usdjpy.OpenPrices[jpyIndex], _gbpusd.OpenPrices[gbpIndex], _usdcad.OpenPrices[cadIndex], _usdchf.OpenPrices[chfIndex], false);
                high = CalculateDxy(_eurusd.LowPrices[eurIndex], _usdjpy.HighPrices[jpyIndex], _gbpusd.LowPrices[gbpIndex], _usdcad.HighPrices[cadIndex], _usdchf.HighPrices[chfIndex], false);
                low = CalculateDxy(_eurusd.HighPrices[eurIndex], _usdjpy.LowPrices[jpyIndex], _gbpusd.HighPrices[gbpIndex], _usdcad.LowPrices[cadIndex], _usdchf.LowPrices[chfIndex], false);
                close = CalculateDxy(_eurusd.ClosePrices[eurIndex], _usdjpy.ClosePrices[jpyIndex], _gbpusd.ClosePrices[gbpIndex], _usdcad.ClosePrices[cadIndex], _usdchf.ClosePrices[chfIndex], false);
            }

            // Alimenta a escala com o topo para manter o zoom correto
            DxyScale[index] = high;

            string lineName = "DxyBarColor_" + index;

            // Determina a cor comparando Fechamento com Abertura
            Color barColor = (close >= open) ? Color.FromName(BullColor) : Color.FromName(BearColor);

            // Desenha o range completo (topo e fundo) com a cor correspondente à direção do candle
            IndicatorArea.DrawTrendLine(lineName, index, high, index, low, barColor, 2, LineStyle.Solid);
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
 // Se tivesse todas moedas
            // double dxy = 50.14348112 
            //    * Math.Pow(eurusd, -0.576) 
            //    * Math.Pow(usdjpy, 0.136) 
            //    * Math.Pow(gbpusd, -0.119) 
            //    * Math.Pow(usdcad, 0.091) 
            //    * Math.Pow(usdsek, 0.042) 
            //    * Math.Pow(usdchf, 0.036);

            // Se o usuário marcar "Inverter Gráfico", aplicamos a inversão