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
        // Nome único e fixo para o objeto deste indicador no gráfico atual
        private const string ObjectName = "Hidden_TextBox_Data_Object_Persist";

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
            string textoInicial = DefaultText;

            // 1. Procurar o objeto persistente no gráfico
            var hiddenTextObject = Chart.FindObject(ObjectName) as ChartText;

            if (hiddenTextObject != null)
            {
                // Se o objeto geométrico existe (mesmo após fechar/abrir ou mudar timeframe), recupera o texto
                textoInicial = hiddenTextObject.Text;
            }
            else
            {
                // Se não existe, cria ele escondido no passado remoto (índice 0 do gráfico)
                SalvarTextoNoGrafico(DefaultText);
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

            // Salva sempre que houver alteração
            _myTextBox.TextChanged += OnTextBoxTextChanged;

            Chart.AddControl(_toggleButton);
            Chart.AddControl(_myTextBox);
        }

        private void OnTextBoxTextChanged(TextChangedEventArgs obj)
        {
            // Salva o texto no objeto do gráfico a cada caractere digitado
            SalvarTextoNoGrafico(_myTextBox.Text);
        }

        private void SalvarTextoNoGrafico(string texto)
        {
            // Pegamos o tempo da primeira barra carregada (índice 0) para ancorar o objeto bem longe no passado
            DateTime tempoPassado = Bars.OpenTimes[0];
            double precoInofensivo = Bars.LowPrices[0];

            // Desenhamos um texto real atrelado ao gráfico, mas com cor transparente (Hidden)
            Chart.DrawText(ObjectName, texto, tempoPassado, precoInofensivo, Color.Transparent);
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