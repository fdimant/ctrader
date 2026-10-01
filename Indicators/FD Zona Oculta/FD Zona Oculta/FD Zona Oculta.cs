using cAlgo.API;
using System.Collections.Generic;
using System;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC)]
    public class HiddenOrderBlocks : Indicator
    {
        [Parameter("Min FVG (%)", DefaultValue = 30)]
        public double MinFvgPercent { get; set; }

        [Parameter("Cor do Bloco", DefaultValue = "DodgerBlue")]
        public Color BlockColor { get; set; }

        [Parameter("Mostrar Mitigados", DefaultValue = true)]
        public bool ShowMitigated { get; set; }

        private List<HiddenBlock> blocks = new List<HiddenBlock>();

        public override void Calculate(int index)
        {
            if (index < 2)
                return;

            DetectBlock(index);
            UpdateBlocks(index);
        }

        public int EndIndex;

        private void DetectBlock(int index)
        {
            if (index < 3 )
                return;

            int i1 = index - 3;
            int i2 = index - 2;

            // Direção
            bool bullish = Bars.ClosePrices[i1] > Bars.OpenPrices[i1] &&
                           Bars.ClosePrices[i2] > Bars.OpenPrices[i2];

            bool bearish = Bars.ClosePrices[i1] < Bars.OpenPrices[i1] &&
                           Bars.ClosePrices[i2] < Bars.OpenPrices[i2];

            if (!bullish && !bearish)
                return;

            // ===== FVG 1 =====
            double fvg1Top, fvg1Bottom, fvg1Size, c1Size;

            if (bullish)
            {
                if (!(Bars.LowPrices[i1 + 1] > Bars.HighPrices[i1 - 1]))
                    return;

                fvg1Top = Bars.LowPrices[i1 + 1];
                fvg1Bottom = Bars.HighPrices[i1 - 1];
            }
            else
            {
                if (!(Bars.HighPrices[i1 + 1] < Bars.LowPrices[i1 - 1]))
                    return;

                fvg1Top = Bars.LowPrices[i1 - 1];
                fvg1Bottom = Bars.HighPrices[i1 + 1];
            }

            fvg1Size = Math.Abs(fvg1Top - fvg1Bottom);
            c1Size = Bars.HighPrices[i1] - Bars.LowPrices[i1];

            if ((fvg1Size / c1Size) * 100 < MinFvgPercent)
                return;

            // ===== FVG 2 =====
            double fvg2Top, fvg2Bottom, fvg2Size, c2Size;

            if (bullish)
            {
                if (!(Bars.LowPrices[i2 + 1] > Bars.HighPrices[i2 - 1]))
                    return;

                fvg2Top = Bars.LowPrices[i2 + 1];
                fvg2Bottom = Bars.HighPrices[i2 - 1];
            }
            else
            {
                if (!(Bars.HighPrices[i2 + 1] < Bars.LowPrices[i2 - 1]))
                    return;

                fvg2Top = Bars.LowPrices[i2 - 1];
                fvg2Bottom = Bars.HighPrices[i2 + 1];
            }

            fvg2Size = Math.Abs(fvg2Top - fvg2Bottom);
            c2Size = Bars.HighPrices[i2] - Bars.LowPrices[i2];

            if ((fvg2Size / c2Size) * 100 < MinFvgPercent)
                return;

            // passou em todas as validações dos 2 FVGs
            // 👉 COLOQUE O DEBUG AQUI

            //Chart.DrawRectangle("FVG1_" + index, i1, fvg1Top, index, fvg1Bottom, Color.Blue);
           // Chart.DrawRectangle("FVG2_" + index, i2, fvg2Top, index, fvg2Bottom, Color.Yellow);


            // ===== BLOCO OCULTO (CORRETO) =====
            double top = Math.Min(fvg1Top, fvg2Top);
            double bottom = Math.Max(fvg1Bottom, fvg2Bottom);

            var block = new HiddenBlock
            {
                StartIndex = i2,
                Top = top,
                Bottom = bottom,
                IsBullish = bullish,
                Mitigated = false,
                ObjectName = "HOB_" + index
            };

            blocks.Add(block);
        }

        private void CreateBlock(int index, bool bullish)
        {
            double top, bottom;

            if (bullish)
            {
                top = Bars.LowPrices[index - 1];
                bottom = Bars.HighPrices[index - 2];
            }
            else
            {
                top = Bars.LowPrices[index - 2];
                bottom = Bars.HighPrices[index - 1];
            }

            var block = new HiddenBlock
            {
                StartIndex = index - 1,
                Top = top,
                Bottom = bottom,
                IsBullish = bullish,
                Mitigated = false,
                ObjectName = "HOB_" + index
            };

            blocks.Add(block);
        }

        private void UpdateBlocks(int index)
        {
            foreach (var block in blocks)
            {
                bool mitigatedNow = false;

                if (!block.Mitigated)
                {
                    if (block.IsBullish)
                        mitigatedNow = Bars.ClosePrices[index] <= block.Bottom;
                    else
                        mitigatedNow = Bars.ClosePrices[index] >= block.Top;

                    if (mitigatedNow)
                    {
                        block.Mitigated = true;
                        block.EndIndex = index; // 👈 guardar onde mitigou
                    }
                }

                // 👉 Se não quer mostrar mitigados → remove
                if (block.Mitigated && !ShowMitigated)
                {
                    Chart.RemoveObject(block.ObjectName);
                    Chart.RemoveObject(block.ObjectName + "_label");
                    continue;
                }

                int endIndex = block.Mitigated ? block.EndIndex : Bars.Count - 1;

                
                var rect = Chart.DrawRectangle(
                    block.ObjectName,
                    block.StartIndex,
                    block.Top,
                    endIndex,
                    block.Bottom,
                    BlockColor
                );

                // 👇 ESSENCIAL
                rect.IsFilled = true;

                // transparência do preenchimento
                rect.Color = Color.FromArgb(80, BlockColor);

                // borda praticamente invisível
                rect.Thickness = 1;
                string label = $"OC {TimeFrame} ({index - block.StartIndex})";

                Chart.DrawText(
                    block.ObjectName + "_label",
                    label,
                    endIndex,
                    block.Top,
                    BlockColor
                );
            }
        }

        class HiddenBlock
        {
            public int StartIndex;
            public double Top;
            public double Bottom;
            public bool IsBullish;
            public bool Mitigated;
            public string ObjectName;
            public int EndIndex;
        }
    }
}