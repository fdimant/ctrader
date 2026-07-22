using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class SMCOrderBlock_WithLabel : Indicator
    {
        
        [Parameter("Cor OB Alta", DefaultValue = "RoyalBlue")]
        public Color BullColor { get; set; }

        [Parameter("Cor OB Alta Parcial", DefaultValue = "Gold")]
        public Color BullPartialColor { get; set; }

        [Parameter("Cor OB Baixa", DefaultValue = "Tomato")]
        public Color BearColor { get; set; }

        [Parameter("Cor OB Baixa Parcial", DefaultValue = "OrangeRed")]
        public Color BearPartialColor { get; set; }

        [Parameter("Mostrar Order Blocks", DefaultValue = true)]
        public bool ShowOrderBlocks { get; set; }
        
        [Parameter("Lookback (Velas)", DefaultValue = 8)]
        public int Lookback { get; set; }

        private class OrderBlock
        {
            public string Id;
            public bool IsBull;
            public double Top;
            public double Bottom;
            public double BodyTop; 
            public double BodyBottom;
            public int StartIndex;
            public bool IsMitigated;
            public bool ValidForMitigation;
            public int Stage; 
        }

        private List<OrderBlock> _activeOBs = new List<OrderBlock>();
        private int _lastBullIndex = -1;
        private int _lastBearIndex = -1;

        public override void Calculate(int index)
        {
            if (index < Lookback + 2) return;

            UpdateMitigation(index);
            CheckForNewOBs(index);
        }

        private void CheckForNewOBs(int index)
        {
            double highToBreak = 0;
            double lowToBreak = double.MaxValue;

            for (int i = 1; i <= Lookback; i++)
            {
                highToBreak = Math.Max(highToBreak, Bars.HighPrices[index - i]);
                lowToBreak = Math.Min(lowToBreak, Bars.LowPrices[index - i]);
            }

            if (Bars.ClosePrices[index] > highToBreak && Bars.ClosePrices[index - 1] <= highToBreak && index - _lastBullIndex > Lookback)
            {
                int target = FindExtreme(index, Lookback + 2, true);
                CreateOB(target, index, true);
                _lastBullIndex = index;
            }
            else if (Bars.ClosePrices[index] < lowToBreak && Bars.ClosePrices[index - 1] >= lowToBreak && index - _lastBearIndex > Lookback)
            {
                int target = FindExtreme(index, Lookback + 2, false);
                CreateOB(target, index, false);
                _lastBearIndex = index;
            }
        }

        private void CreateOB(int target, int triggerIndex, bool isBull)
        {
            var ob = new OrderBlock
            {
                Id = (isBull ? "Bull_" : "Bear_") + Bars.OpenTimes[target].Ticks,
                IsBull = isBull,
                Top = Bars.HighPrices[target],
                Bottom = Bars.LowPrices[target],
                BodyTop = Math.Max(Bars.OpenPrices[target], Bars.ClosePrices[target]),
                BodyBottom = Math.Min(Bars.OpenPrices[target], Bars.ClosePrices[target]),
                StartIndex = target,
                IsMitigated = false,
                ValidForMitigation = false,
                Stage = 0
            };
            _activeOBs.Add(ob);
        }

        private void UpdateMitigation(int index)
        {
            var current = Bars[index];

            foreach (var ob in _activeOBs.ToArray())
            {
                if (ob.IsMitigated) continue;

                if (!ob.ValidForMitigation)
                {
                    if (ob.IsBull && current.Close > ob.Top) ob.ValidForMitigation = true;
                    if (!ob.IsBull && current.Close < ob.Bottom) ob.ValidForMitigation = true;
                }

                if (ob.ValidForMitigation)
                {
                    if (ob.IsBull)
                    {
                        if (current.Low <= ob.Bottom) { Mitigate(ob); continue; }
                        else if (current.Low <= ob.BodyTop) { ob.Top = ob.BodyBottom; ob.Stage = 2; }
                        else if (current.Low < ob.Top) { ob.Top = ob.BodyTop; ob.Stage = 1; }
                    }
                    else
                    {
                        if (current.High >= ob.Top) { Mitigate(ob); continue; }
                        else if (current.High >= ob.BodyBottom) { ob.Bottom = ob.BodyTop; ob.Stage = 2; }
                        else if (current.High > ob.Bottom) { ob.Bottom = ob.BodyBottom; ob.Stage = 1; }
                    }
                    
                    // Se após o ajuste de zona acima, o preço já estiver "dentro" do que sobrou no Stage 2, mitiga tudo.
                    if (ob.Stage == 2 && ((ob.IsBull && current.Low < ob.Top) || (!ob.IsBull && current.High > ob.Bottom)))
                    {
                        Mitigate(ob);
                        continue;
                    }
                }

                DrawOBDynamic(ob, index);
            }
        }

        private void Mitigate(OrderBlock ob)
        {
            ob.IsMitigated = true;
            Chart.RemoveObject(ob.Id);
            Chart.RemoveObject("Label_" + ob.Id);
        }

        private void DrawOBDynamic(OrderBlock ob, int currentIndex)
        {
            if (!ShowOrderBlocks)
            {
                Chart.RemoveObject(ob.Id);
                Chart.RemoveObject("Label_" + ob.Id);
                return;
            }
            
            Color color;

            if (ob.Stage > 0)
            {
                color = ob.IsBull ? BullPartialColor : BearPartialColor;
            }
            else
            {
                color = ob.IsBull ? BullColor : BearColor;
            }
            
            // 1. Desenha o Retângulo
            Chart.DrawRectangle(ob.Id, ob.StartIndex, ob.Top, currentIndex, ob.Bottom, color).IsFilled = true;

            // 2. Desenha a Identificação (Label)
            string timeFrame = TimeFrame.ToString();
            int age = currentIndex - ob.StartIndex;
            string labelText = string.Format("OB {0} ({1})", timeFrame, age);
            
            // Posiciona o texto à direita (no currentIndex), no centro vertical da zona atual
            double middleY = (ob.Top + ob.Bottom) / 2;
            Chart.DrawText("Label_" + ob.Id, labelText, currentIndex + 1, middleY, color);
        }

        private int FindExtreme(int index, int range, bool findLowest)
        {
            int extremeIndex = -1;
            double extremeValue = findLowest ? double.MaxValue : 0;
            for (int j = 1; j <= range; j++)
            {
                double val = findLowest ? Bars.LowPrices[index - j] : Bars.HighPrices[index - j];
                if ((findLowest && val < extremeValue) || (!findLowest && val > extremeValue))
                {
                    extremeValue = val;
                    extremeIndex = index - j;
                }
            }
            return extremeIndex;
        }
    }
}