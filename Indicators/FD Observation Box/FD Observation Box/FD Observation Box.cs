using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum ChartCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CustomTextBoxPlugin : Indicator
    {
        // Unique and unified object name per asset
        private string _uniqueObjectName;

        [Parameter("Screen Position", DefaultValue = ChartCorner.BottomLeft, Group = "Layout")]
        public ChartCorner ButtonPosition { get; set; }

        [Parameter("Box Width", DefaultValue = 200, Group = "Text Box Options")]
        public int BoxWidth { get; set; }

        [Parameter("Box Height", DefaultValue = 100, Group = "Text Box Options")]
        public int BoxHeight { get; set; }

        [Parameter("Font Size", DefaultValue = 12, Group = "Text Box Options")]
        public int MyFontSize { get; set; }

        [Parameter("Default Text", DefaultValue = "Type here...", Group = "Text Box Options")]
        public string DefaultText { get; set; }

        [Parameter("Background Color", DefaultValue = "#D01C1B21",  Group = "Colors")]
        public Color BackgroundColorSetting { get; set; }

        [Parameter("Font Color", DefaultValue = "#FFFFFFFF",Group = "Colors")]
        public Color FontColorSetting { get; set; }

        private Button _toggleButton;
        private TextBox _myTextBox;
        private bool _isBoxVisible = false;

        protected override void Initialize()
        {
            // Set fallback colors if user leaves them empty
            if (BackgroundColorSetting == null || BackgroundColorSetting == Color.Transparent)
                BackgroundColorSetting = Color.FromHex("#D01C1B21");
                
            if (FontColorSetting == null || FontColorSetting == Color.Transparent)
                FontColorSetting = Color.FromHex("#FFFFFFFF");

            // Unified object name per asset symbol
            _uniqueObjectName = "TextBoxData_Common_" + Symbol.Name;
            
            string textoInicial = DefaultText;
            bool objetoEncontrado = false;

            // Scans chart objects looking for our specific common object
            foreach (var obj in Chart.Objects)
            {
                if (obj.Name == _uniqueObjectName && obj is ChartText textObj)
                {
                    if (!string.IsNullOrEmpty(textObj.Text))
                    {
                        textoInicial = textObj.Text;
                        objetoEncontrado = true;
                    }
                    break;
                }
            }

            // If it's the first time running on this asset, create the hidden database object
            if (!objetoEncontrado)
            {
                SalvarTextoNoGrafico(DefaultText);
            }

            HorizontalAlignment horizontalAlign = HorizontalAlignment.Left;
            VerticalAlignment verticalAlign = VerticalAlignment.Top;
            Thickness textBoxMargin = new Thickness(10, 45, 10, 10);

            switch (ButtonPosition)
            {
                case ChartCorner.TopLeft:
                    horizontalAlign = HorizontalAlignment.Left; verticalAlign = VerticalAlignment.Top; textBoxMargin = new Thickness(10, 45, 10, 10); break;
                case ChartCorner.TopRight:
                    horizontalAlign = HorizontalAlignment.Right; verticalAlign = VerticalAlignment.Top; textBoxMargin = new Thickness(10, 45, 10, 10); break;
                case ChartCorner.BottomLeft:
                    horizontalAlign = HorizontalAlignment.Left; verticalAlign = VerticalAlignment.Bottom; textBoxMargin = new Thickness(10, 10, 10, 45); break;
                case ChartCorner.BottomRight:
                    horizontalAlign = HorizontalAlignment.Right; verticalAlign = VerticalAlignment.Bottom; textBoxMargin = new Thickness(10, 10, 10, 45); break;
            }

            _toggleButton = new Button
            {
                Text = "Open Note", Width = 80, Height = 30, HorizontalAlignment = horizontalAlign, VerticalAlignment = verticalAlign, Margin = new Thickness(10, 10, 10, 10)
            };
            _toggleButton.Click += OnButtonClick;

            _myTextBox = new TextBox
            {
                Text = textoInicial, Width = BoxWidth, Height = BoxHeight, FontSize = MyFontSize,
                BackgroundColor = BackgroundColorSetting, ForegroundColor = FontColorSetting,
                HorizontalAlignment = horizontalAlign, VerticalAlignment = verticalAlign, Margin = textBoxMargin,
                IsVisible = false, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap
            };

            _myTextBox.TextChanged += OnTextBoxTextChanged;

            Chart.AddControl(_toggleButton);
            Chart.AddControl(_myTextBox);
        }

        private void OnTextBoxTextChanged(TextChangedEventArgs obj)
        {
            SalvarTextoNoGrafico(_myTextBox.Text);
        }

        private void OnButtonClick(ButtonClickEventArgs obj)
        {
            _isBoxVisible = !_isBoxVisible;
            _myTextBox.IsVisible = _isBoxVisible;
            _toggleButton.Text = _isBoxVisible ? "Close Note" : "Open Note";

            // Enforces data persistence when the box is closed
            if (!_isBoxVisible)
            {
                SalvarTextoNoGrafico(_myTextBox.Text);
            }
        }

        private void SalvarTextoNoGrafico(string texto)
        {
            if (Bars.Count > 0)
            {
                // Anchors the object at bar zero (remote past) to keep the current price action clean
                DateTime tempoPassado = Bars.OpenTimes[0];
                double precoInofensivo = Bars.LowPrices[0];

                // Drawn as Transparent so it works invisibly in the background, but Interactive so cTrader serializes it
                var box = Chart.DrawText(_uniqueObjectName, texto, tempoPassado, precoInofensivo, Color.Transparent);
                box.IsInteractive = true;
            }
        }

        public override void Calculate(int index) 
        {
            // Left empty for maximum chart performance
        }
    }
}