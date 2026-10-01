using cAlgo.API;
using cAlgo.API.Internals;
using System;

namespace cAlgo
{
    public enum PanelPosition
    {
        SuperiorEsquerda,
        SuperiorDireita,
        InferiorEsquerda,
        InferiorDireita
    }

    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class ADRIndicator : Indicator
    {
        [Parameter("Períodos ADR Médio", DefaultValue = 5, MinValue = 1)]
        public int AdrPeriods { get; set; }

        [Parameter("Posição do Painel", DefaultValue = PanelPosition.InferiorDireita)]
        public PanelPosition Position { get; set; }

        [Parameter("Cor do Texto", DefaultValue = "#000000")]
        public Color TextColor { get; set; }

        private Bars _dailyBars;
        private const string LabelName = "ADR_LABEL";

        protected override void Initialize()
        {
            _dailyBars = MarketData.GetBars(TimeFrame.Daily);
        }

        public override void Calculate(int index)
        {
            if (_dailyBars.Count < AdrPeriods + 1)
                return;

            double adrMedia = 0;

            // ADR médio dos últimos N dias fechados
            for (int i = 1; i <= AdrPeriods; i++)
            {
                double range = _dailyBars.HighPrices.Last(i) - _dailyBars.LowPrices.Last(i);
                adrMedia += range;
            }

            adrMedia /= AdrPeriods;

            // ADR do dia atual
            double adrAtual = _dailyBars.HighPrices.LastValue - _dailyBars.LowPrices.LastValue;

            // Converter para pips
            double adrMediaPips = adrMedia / Symbol.PipSize;
            double adrAtualPips = adrAtual / Symbol.PipSize;

            double percentual = adrMediaPips > 0
                ? (adrAtualPips / adrMediaPips) * 100
                : 0;

            string texto = string.Format(
                "ADR Atual: {0:F1} pips | ADR Média: {1:F1} pips | {2:F1}%",
                adrAtualPips,
                adrMediaPips,
                percentual);

            // Definir posição
            VerticalAlignment vertical;
            HorizontalAlignment horizontal;

            switch (Position)
            {
                case PanelPosition.SuperiorEsquerda:
                    vertical = VerticalAlignment.Top;
                    horizontal = HorizontalAlignment.Left;
                    break;

                case PanelPosition.SuperiorDireita:
                    vertical = VerticalAlignment.Top;
                    horizontal = HorizontalAlignment.Right;
                    break;

                case PanelPosition.InferiorEsquerda:
                    vertical = VerticalAlignment.Bottom;
                    horizontal = HorizontalAlignment.Left;
                    break;

                default:
                    vertical = VerticalAlignment.Bottom;
                    horizontal = HorizontalAlignment.Right;
                    break;
            }

            Chart.DrawStaticText(
                LabelName,
                texto,
                vertical,
                horizontal,
                TextColor);
        }
    }
}