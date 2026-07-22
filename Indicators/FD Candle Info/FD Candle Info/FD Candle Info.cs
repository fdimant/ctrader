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
        // --- User Parameters ---
        [Parameter("Text Color", DefaultValue = "Yellow")]
        public Color TextColor { get; set; }

        [Parameter("Background Color", DefaultValue = "Black")]
        public Color BackgroundColor { get; set; }

        [Parameter("Font Size", DefaultValue = 11, MinValue = 8, MaxValue = 24)]
        public int FontSize { get; set; }

        [Parameter("Display Position", DefaultValue = DisplayPosition.TopLeft)]
        public DisplayPosition PosicaoDisplay { get; set; }


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
                
                // Pegamos o tempo bruto diretamente da barra
                DateTime baseTime = Bars.OpenTimes[index];
                
                // Aplicamos o fuso horário oficial configurado na plataforma cTrader
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
        }
    }
}