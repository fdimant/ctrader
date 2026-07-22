using cAlgo.API;
using cAlgo.API.Indicators;
using System;
using System.Linq;
using System.Collections.Generic;

namespace cAlgo
{
    public enum PanelPosition { Left, Right }

    public class SymbolCache
    {
        public string Name;
        public Bars Bars;
        public ExponentialMovingAverage EmaFast;
        public ExponentialMovingAverage EmaSlow;
        public RelativeStrengthIndex Rsi;
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class ForexDashboardUI : Indicator
    {
        [Parameter("Symbols", DefaultValue = "EURUSD,GBPUSD,USDJPY,EURJPY")]
        public string SymbolsInput { get; set; }

        [Parameter("Timeframe", DefaultValue = "Minute5")]
        public TimeFrame Tf { get; set; }

        [Parameter("Panel Position", DefaultValue = PanelPosition.Left)]
        public PanelPosition Position { get; set; }

        private List<SymbolCache> cache = new List<SymbolCache>();

        private const string PREFIX = "FXDB_";

        protected override void Initialize()
        {
            foreach (var s in SymbolsInput.Split(','))
            {
                var symbol = Symbols.GetSymbol(s.Trim());
                if (symbol == null) continue;

                var bars = MarketData.GetBars(Tf, symbol.Name);
                if (bars == null) continue;

                cache.Add(new SymbolCache
                {
                    Name = symbol.Name,
                    Bars = bars,
                    EmaFast = Indicators.ExponentialMovingAverage(bars.ClosePrices, 9),
                    EmaSlow = Indicators.ExponentialMovingAverage(bars.ClosePrices, 21),
                    Rsi = Indicators.RelativeStrengthIndex(bars.ClosePrices, 14)
                });
            }

            Timer.Start(5);
        }

        public override void Calculate(int index) { }

        protected override void OnTimer()
        {
            UpdateDashboard();
        }

        private void UpdateDashboard()
        {
            var pairScores = new List<(string, int)>();
            var currencyStrength = new Dictionary<string, double>();

            foreach (var sc in cache)
            {
                if (sc.Bars.Count < 20) continue;

                int score = 0;

                double emaFast = sc.EmaFast.Result.LastValue;
                double emaSlow = sc.EmaSlow.Result.LastValue;
                double rsi = sc.Rsi.Result.LastValue;

                score += emaFast > emaSlow ? 2 : -2;

                if (rsi > 60) score += 1;
                else if (rsi < 40) score -= 1;

                pairScores.Add((sc.Name, score));

                string baseCur = sc.Name.Substring(0, 3);
                string quoteCur = sc.Name.Substring(3, 3);

                double change = sc.Bars.ClosePrices.LastValue - sc.Bars.ClosePrices.Last(1);

                if (!currencyStrength.ContainsKey(baseCur)) currencyStrength[baseCur] = 0;
                if (!currencyStrength.ContainsKey(quoteCur)) currencyStrength[quoteCur] = 0;

                currencyStrength[baseCur] += change;
                currencyStrength[quoteCur] -= change;
            }

            var sortedPairs = pairScores.OrderByDescending(p => p.Item2).ToList();
            var sortedCurrencies = currencyStrength.OrderByDescending(c => c.Value).ToList();

            DrawPanel(sortedPairs, sortedCurrencies);
        }

        
        private void DrawPanel(List<(string, int)> pairs, List<KeyValuePair<string, double>> currencies)
        {
            // Remove tudo antes de redesenhar
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name.StartsWith(PREFIX))
                    Chart.RemoveObject(obj.Name);
            }

            int line = 0;

            var hAlign = Position == PanelPosition.Left 
                ? HorizontalAlignment.Left 
                : HorizontalAlignment.Right;

            // ===== TÍTULO =====
            Chart.DrawStaticText(
                PREFIX + "title",
                "🔥 TOP PARES",
                VerticalAlignment.Top,
                hAlign,
                Color.White
            );

            line++;

            // ===== PARES =====
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];

                Color color = p.Item2 >= 2 ? Color.Lime :
                              p.Item2 <= -2 ? Color.Red :
                              Color.Yellow;

                string text = $"{p.Item1} [{p.Item2}]";

                Chart.DrawStaticText(
                    PREFIX + "pair_" + i,
                    "\n\n" + string.Join("\n", Enumerable.Repeat("", line)) + text,
                    VerticalAlignment.Top,
                    hAlign,
                    color
                );

                line++;
            }

            line++;

            // ===== MOEDAS =====
            Chart.DrawStaticText(
                PREFIX + "cur_title",
                "\n\n" + string.Join("\n", Enumerable.Repeat("", line)) + "💱 MOEDAS",
                VerticalAlignment.Top,
                hAlign,
                Color.White
            );

            line++;

            for (int i = 0; i < currencies.Count; i++)
            {
                var c = currencies[i];

                Color color = c.Value > 0 ? Color.Lime : Color.Red;

                string text = $"{c.Key}";

                Chart.DrawStaticText(
                    PREFIX + "cur_" + i,
                    "\n\n" + string.Join("\n", Enumerable.Repeat("", line)) + text,
                    VerticalAlignment.Top,
                    hAlign,
                    color
                );

                line++;
            }
        }

    }
}