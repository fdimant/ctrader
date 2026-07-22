using System;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum ChartCorner
    {
        SuperiorEsquerdo,
        SuperiorDireito,
        InferiorEsquerdo,
        InferiorDireito
    }

    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CustomTextBoxPlugin : Indicator
    {
        // Uma string base para identificar nosso plugin no armazenamento
        private const string BaseStorageKey = "CustomTextBoxPlugin_Data_";
        
        // Esta variável vai guardar a chave única deste gráfico específico
        private string _uniqueStorageKey;

        // --- Parâmetro de Localização ---
        [Parameter("Posição na Tela", DefaultValue = ChartCorner.SuperiorEsquerdo, Group = "Layout")]
        public ChartCorner ButtonPosition { get; set; }

        // --- Parâmetros de Configuração da Caixa de Texto ---
        [Parameter("Largura da Caixa", DefaultValue = 200, Group = "Caixa de Texto")]
        public int BoxWidth { get; set; }

        [Parameter("Altura da Caixa", DefaultValue = 100, Group = "Caixa de Texto")]
        public int BoxHeight { get; set; }

        [Parameter("Tamanho da Fonte", DefaultValue = 12, Group = "Caixa de Texto")]
        public int MyFontSize { get; set; }

        [Parameter("Texto Padrão", DefaultValue = "Digite aqui...", Group = "Caixa de Texto")]
        public string DefaultText { get; set; }

        // --- Parâmetros de Cores ---
        [Parameter("Cor de Fundo (Hex)", DefaultValue = "#2A2A2A", Group = "Cores")]
        public string BgColorHex { get; set; }

        [Parameter("Cor da Fonte (Hex)", DefaultValue = "#FFFFFF", Group = "Cores")]
        public string FontColorHex { get; set; }

        // --- Elementos de Interface ---
        private Button _toggleButton;
        private TextBox _myTextBox;
        private bool _isBoxVisible = false;

        protected override void Initialize()
        {
            // Criamos a chave única usando o nome do ativo (Ex: "CustomTextBoxPlugin_Data_EURUSD")
            // Removido o TimeFrame daqui para que se você mudar o TimeFrame NO MESMO ATIVO, o texto continue o mesmo!
            _uniqueStorageKey = BaseStorageKey + Symbol.Name;

            string textoInicial = DefaultText;
            try
            {
                // Tenta carregar o texto salvo no disco para este ativo específico
                string guardado = LocalStorage.GetString(_uniqueStorageKey);
                if (!string.IsNullOrEmpty(guardado))
                {
                    textoInicial = guardado;
                }
            }
            catch
            {
                textoInicial = DefaultText;
            }

            HorizontalAlignment horizontalAlign = HorizontalAlignment.Left;
            VerticalAlignment verticalAlign = VerticalAlignment.Top;
            Thickness textBoxMargin = new Thickness(10, 45, 10, 10);

            switch (ButtonPosition)
            {
                case ChartCorner.SuperiorEsquerdo:
                    horizontalAlign = HorizontalAlignment.Left;
                    verticalAlign = VerticalAlignment.Top;
                    textBoxMargin = new Thickness(10, 45, 10, 10);
                    break;
                case ChartCorner.SuperiorDireito:
                    horizontalAlign = HorizontalAlignment.Right;
                    verticalAlign = VerticalAlignment.Top;
                    textBoxMargin = new Thickness(10, 45, 10, 10);
                    break;
                case ChartCorner.InferiorEsquerdo:
                    horizontalAlign = HorizontalAlignment.Left;
                    verticalAlign = VerticalAlignment.Bottom;
                    textBoxMargin = new Thickness(10, 10, 10, 45);
                    break;
                case ChartCorner.InferiorDireito:
                    horizontalAlign = HorizontalAlignment.Right;
                    verticalAlign = VerticalAlignment.Bottom;
                    textBoxMargin = new Thickness(10, 10, 10, 45);
                    break;
            }

            // Inicializar o Botão
            _toggleButton = new Button
            {
                Text = "Abrir Caixa de Texto",
                Width = 150,
                Height = 30,
                HorizontalAlignment = horizontalAlign,
                VerticalAlignment = verticalAlign,
                Margin = new Thickness(10, 10, 10, 10)
            };

            _toggleButton.Click += OnButtonClick;

            // Inicializar a Caixa de Texto
            _myTextBox = new TextBox
            {
                Text = textoInicial,
                Width = BoxWidth,
                Height = BoxHeight,
                FontSize = MyFontSize,
                BackgroundColor = Color.FromHex(BgColorHex),
                ForegroundColor = Color.FromHex(FontColorHex),
                HorizontalAlignment = horizontalAlign,
                VerticalAlignment = verticalAlign,
                Margin = textBoxMargin,
                IsVisible = false,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap
            };

            // Monitora a digitação
            _myTextBox.TextChanged += OnTextBoxTextChanged;

            Chart.AddControl(_toggleButton);
            Chart.AddControl(_myTextBox);
        }

        private void OnTextBoxTextChanged(TextChangedEventArgs obj)
        {
            try
            {
                // Salva no LocalStorage usando a chave exclusiva do Ativo atual
                LocalStorage.SetString(_uniqueStorageKey, _myTextBox.Text);
            }
            catch
            {
                // Proteção contra falhas de escrita
            }
        }

        private void OnButtonClick(ButtonClickEventArgs obj)
        {
            _isBoxVisible = !_isBoxVisible;
            _myTextBox.IsVisible = _isBoxVisible;
            _toggleButton.Text = _isBoxVisible ? "Fechar Caixa" : "Abrir Caixa de Texto";
        }

        public override void Calculate(int index)
        {
        }
    }
}