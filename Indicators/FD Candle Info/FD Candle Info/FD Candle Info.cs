using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum DisplayPosition
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        NextToMouse
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MouseOverOHLC : Indicator
    {
        // --- Display Parameters ---
        [Parameter("Text Color", Group = "Display", DefaultValue = "Yellow")]
        public Color TextColor { get; set; }

        [Parameter("Background Color", Group = "Display", DefaultValue = "Black")]
        public Color BackgroundColor { get; set; }

        [Parameter("Font Size", Group = "Display", DefaultValue = 11, MinValue = 8, MaxValue = 24)]
        public int FontSize { get; set; }

        [Parameter("Display Position", Group = "Display", DefaultValue = DisplayPosition.TopLeft)]
        public DisplayPosition PosicaoDisplay { get; set; }

        // --- 3 Candle Feature Parameters ---
        [Parameter("Enable 3-Candle High/Low", Group = "3 Candle Pattern", DefaultValue = false)]
        public bool Enable3CandlePattern { get; set; }

        [Parameter("Pivot Low Color", Group = "3 Candle Pattern", DefaultValue = "Red")]
        public Color PivotLowColor { get; set; }

        [Parameter("Pivot High Color", Group = "3 Candle Pattern", DefaultValue = "Green")]
        public Color PivotHighColor { get; set; }


        private bool _exibirPainel = false;
        private DateTime _ultimoClique = DateTime.MinValue;

        private Canvas _canvasContainer;
        private Border _painelFundo;
        private TextBlock _textoDisplay;

        protected override void Initialize()
        {
            Chart.MouseMove += Chart_MouseMove;
            Chart.MouseDown += Chart_MouseDown;

            _textoDisplay = new TextBlock
            {
                Text = "",
                ForegroundColor = TextColor,
                Margin = new Thickness(6, 6, 6, 6),
                FontSize = FontSize,
                FontFamily = "Consolas" 
            };

            _painelFundo = new Border
            {
                BackgroundColor = Color.FromArgb(200, BackgroundColor.R, BackgroundColor.G, BackgroundColor.B),
                BorderColor = TextColor,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = _textoDisplay,
                IsVisible = false 
            };

            _canvasContainer = new Canvas
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };

            _canvasContainer.AddChild(_painelFundo);
            Chart.AddControl(_canvasContainer);

            UpdateFixedPosition(VerticalAlignment.Top, HorizontalAlignment.Left);
        }

        private void Chart_MouseDown(ChartMouseEventArgs obj)
        {
            DateTime agora = DateTime.Now;
            TimeSpan diferencaTempo = agora - _ultimoClique;

            if (diferencaTempo.TotalMilliseconds < 300)
            {
                _exibirPainel = !_exibirPainel;
                _painelFundo.IsVisible = _exibirPainel;

                // Aplica as cores de pivô ou restaura as cores nativas (Bull/Bear)
                ApplyOrClearCandleColors();

                _ultimoClique = DateTime.MinValue;
            }
            else
            {
                _ultimoClique = agora;
            }
        }

        private void Chart_MouseMove(ChartMouseEventArgs obj)
        {
            if (!_exibirPainel)
                return;

            int index = (int)Math.Round(obj.BarIndex);

            if (index >= 0 && index < Bars.Count)
            {
                var open = Bars.OpenPrices[index];
                var high = Bars.HighPrices[index];
                var low = Bars.LowPrices[index];
                var close = Bars.ClosePrices[index];
                
                DateTime baseTime = Bars.OpenTimes[index];
                DateTime localTime = baseTime.Add(Application.UserTimeOffset);

                _textoDisplay.Text = string.Format(
                    "Date:  {0:dd/MM/yyyy HH:mm}\n" +
                    "Open:  {1}\n" +
                    "High:  {2}\n" +
                    "Low:   {3}\n" +
                    "Close: {4}", 
                    localTime, open, high, low, close);

                if (PosicaoDisplay == DisplayPosition.NextToMouse)
                {
                    _painelFundo.HorizontalAlignment = HorizontalAlignment.Left;
                    _painelFundo.VerticalAlignment = VerticalAlignment.Top;
                    _painelFundo.Margin = new Thickness(0);

                    _painelFundo.Left = obj.MouseX + 15;
                    _painelFundo.Top = obj.MouseY + 15;
                }
                else
                {
                    switch (PosicaoDisplay)
                    {
                        case DisplayPosition.TopLeft:
                            UpdateFixedPosition(VerticalAlignment.Top, HorizontalAlignment.Left);
                            break;
                        case DisplayPosition.TopRight:
                            UpdateFixedPosition(VerticalAlignment.Top, HorizontalAlignment.Right);
                            break;
                        case DisplayPosition.BottomLeft:
                            UpdateFixedPosition(VerticalAlignment.Bottom, HorizontalAlignment.Left);
                            break;
                        case DisplayPosition.BottomRight:
                            UpdateFixedPosition(VerticalAlignment.Bottom, HorizontalAlignment.Right);
                            break;
                    }
                }
            }
        }

        private void UpdateFixedPosition(VerticalAlignment vAlign, HorizontalAlignment hAlign)
        {
            _painelFundo.Left = double.NaN;
            _painelFundo.Top = double.NaN;

            _painelFundo.VerticalAlignment = vAlign;
            _painelFundo.HorizontalAlignment = hAlign;
            _painelFundo.Margin = new Thickness(10, 10, 10, 10);
        }

        public override void Calculate(int index)
        {
            if (!_exibirPainel || !Enable3CandlePattern)
                return;

            CheckAndColorCandle(index);
        }

        private void CheckAndColorCandle(int index)
        {
            int targetIndex = index - 1;

            if (targetIndex > 0 && index < Bars.Count)
            {
                double prevLow = Bars.LowPrices[targetIndex - 1];
                double currentLow = Bars.LowPrices[targetIndex];
                double nextLow = Bars.LowPrices[index];

                double prevHigh = Bars.HighPrices[targetIndex - 1];
                double currentHigh = Bars.HighPrices[targetIndex];
                double nextHigh = Bars.HighPrices[index];

                if (currentLow < prevLow && currentLow < nextLow)
                {
                    Chart.SetBarOutlineColor(targetIndex, PivotLowColor);
                }
                else if (currentHigh > prevHigh && currentHigh > nextHigh)
                {
                    Chart.SetBarOutlineColor(targetIndex, PivotHighColor);
                }
            }
        }

        private void ApplyOrClearCandleColors()
        {
            if (!Enable3CandlePattern)
                return;

            if (_exibirPainel)
            {
                for (int i = 1; i < Bars.Count; i++)
                {
                    CheckAndColorCandle(i);
                }
            }
            else
            {
                // Restaura cada candle especificamente para a cor de contorno Bull ou Bear original do tema
                for (int i = 0; i < Bars.Count; i++)
                {
                    bool isBull = Bars.ClosePrices[i] >= Bars.OpenPrices[i];
                    Color defaultColor = isBull ? Chart.ColorSettings.BullOutlineColor : Chart.ColorSettings.BearOutlineColor;
                    
                    Chart.SetBarOutlineColor(i, defaultColor);
                }
            }
        }
    }
}