using cAlgo.API;
using System.Collections.Generic;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class CandleTriangleIconIndicator : Indicator
    {
        [Parameter("Cor Fundo (Alta)", DefaultValue = "Lime")]
        public Color LowColor { get; set; }

        [Parameter("Cor Topo (Baixa)", DefaultValue = "Red")]
        public Color HighColor { get; set; }

        [Parameter("Cor Invalidação", DefaultValue = "Gray")]
        public Color InvalidatedColor { get; set; }

        private Dictionary<int, double> bullishLevels = new Dictionary<int, double>();
        private Dictionary<int, double> bearishLevels = new Dictionary<int, double>();

        public override void Calculate(int index)
        {
            if (index < 5)
                return;

            int i = index - 1;

            // ===== OFFSET DINÂMICO =====
            double avgRange = 0;

            for (int j = i - 4; j <= i; j++)
                avgRange += Bars.HighPrices[j] - Bars.LowPrices[j];

            avgRange /= 5.0;

            double offset = avgRange * 0.25;

            // ===== DETECÇÃO DE NOVOS TRIÂNGULOS =====

            // FUNDO
            if (Bars.LowPrices[i] < Bars.LowPrices[i - 1] &&
                Bars.LowPrices[i] < Bars.LowPrices[i + 1])
            {
                bullishLevels[i] = Bars.LowPrices[i];

                Chart.DrawIcon(
                    "LowIcon_" + i,
                    ChartIconType.UpTriangle,
                    Bars.OpenTimes[i],
                    Bars.LowPrices[i] - offset,
                    LowColor
                );
            }

            // TOPO
            if (Bars.HighPrices[i] > Bars.HighPrices[i - 1] &&
                Bars.HighPrices[i] > Bars.HighPrices[i + 1])
            {
                bearishLevels[i] = Bars.HighPrices[i];

                Chart.DrawIcon(
                    "HighIcon_" + i,
                    ChartIconType.DownTriangle,
                    Bars.OpenTimes[i],
                    Bars.HighPrices[i] + offset,
                    HighColor
                );
            }

            // ===== VERIFICAÇÃO DE INVALIDAÇÃO =====

            // Invalidação de fundos
            foreach (var level in bullishLevels)
            {
                int levelIndex = level.Key;
                double price = level.Value;

                if (index <= levelIndex)
                    continue;

                if (Bars.LowPrices[index] < price)
                {
                    Chart.DrawIcon(
                        "LowIcon_" + levelIndex,
                        ChartIconType.UpTriangle,
                        Bars.OpenTimes[levelIndex],
                        price - offset,
                        InvalidatedColor
                    );
                }
            }

            // Invalidação de topos
            foreach (var level in bearishLevels)
            {
                int levelIndex = level.Key;
                double price = level.Value;

                if (index <= levelIndex)
                    continue;

                if (Bars.HighPrices[index] > price)
                {
                    Chart.DrawIcon(
                        "HighIcon_" + levelIndex,
                        ChartIconType.DownTriangle,
                        Bars.OpenTimes[levelIndex],
                        price + offset,
                        InvalidatedColor
                    );
                }
            }
        }
    }
}
