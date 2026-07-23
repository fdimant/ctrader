using cAlgo.API;
using System;
using System.IO;
using System.Linq;

namespace cAlgo.Robots
{
    public enum AccountRole
    {
        Master,
        Slave
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class UnifiedTradeCopier : Robot
    {
        [Parameter("Função da Conta", DefaultValue = AccountRole.Slave)]
        public AccountRole Role { get; set; }

        [Parameter("Volume Mínimo Permitido (Lotes)", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double MinVolumeInLots { get; set; }

        [Parameter("Multiplicador Fixo (0 = Auto)", DefaultValue = 0.0, MinValue = 0.0, Step = 0.1)]
        public double VolumeMultiplier { get; set; }

        private string _filePath;
        private readonly object _fileLock = new object();

        protected override void OnStart()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _filePath = Path.Combine(docPath, "cTrader_Unified_Bridge.txt");
            Print("CAMINHO REAL DO ARQUIVO: " + _filePath);

            if (Role == AccountRole.Master)
            {
                Print("Modo MASTER Ativado. Monitorando posições a mercado...");
                Positions.Opened += Positions_Opened;
                Positions.Modified += Positions_Modified;
                Positions.Closed += Positions_Closed;
            }
            else
            {
                Print("Modo SLAVE Ativado. Monitorando sinais via Timer...");
                Timer.Start(TimeSpan.FromMilliseconds(100));
                
                // ================================================================
                // TRAVA DE SEGURANÇA: Limpa a fila antiga ao iniciar o Slave
                // ================================================================
                try
                {
                    if (System.IO.File.Exists(_filePath))
                    {
                        System.IO.File.WriteAllText(_filePath, string.Empty);
                        Print("-> [PROTEÇÃO] Arquivo de ponte antigo limpo com sucesso para evitar ordens fantasmas.");
                    }
                }
                catch (Exception ex)
                {
                    Print("-> [AVISO] Não foi possível limpar o arquivo no início: " + ex.Message);
                }
            }
        }

        protected override void OnTick()
        {
        }

        protected override void OnTimer()
        {
            if (Role == AccountRole.Master) return;

            try
            {
                if (!System.IO.File.Exists(_filePath)) return;

                string[] lines;
                lock (_fileLock)
                {
                    using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        var content = sr.ReadToEnd();
                        if (string.IsNullOrEmpty(content)) return;
                        
                        lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    // Limpa o arquivo imediatamente após ler
                    System.IO.File.WriteAllText(_filePath, string.Empty);
                }

                // Processa os sinais recebidos
                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    Print("-> Slave processando sinal da fila: " + line);
                    ProcessSignal(line);
                }
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("process cannot access the file"))
                {
                    Print("Erro ao processar fila no Slave: " + ex.Message);
                }
            }
        }

        // ==========================================
        // EVENTOS DO MASTER (Apenas Posições Abertas)
        // ==========================================
        private void Positions_Opened(PositionOpenedEventArgs args)
        {
            try
            {
                var pos = args.Position;
                Print("-> Master detectou NOVA POSIÇÃO ABERTA! ID: {0}, Ativo: {1}", pos.Id, pos.SymbolName);
                
                SendSignal("POSITION_OPEN|" + pos.Id + "|" + pos.SymbolName + "|" + pos.TradeType + "|" + pos.VolumeInUnits + "|" + Account.Balance);
                TriggerPositionModification(pos);
            }
            catch (Exception ex)
            {
                Print("Erro no evento Positions_Opened: " + ex.Message);
            }
        }

        private void Positions_Modified(PositionModifiedEventArgs args)
        {
            try
            {
                Print("-> Master detectou MODIFICAÇÃO na posição ID: {0}", args.Position.Id);
                TriggerPositionModification(args.Position);
            }
            catch (Exception ex)
            {
                Print("Erro no evento Positions_Modified: " + ex.Message);
            }
        }

        private void TriggerPositionModification(Position pos)
        {
            var symbol = Symbols.GetSymbol(pos.SymbolName);
            double slPips = pos.StopLoss.HasValue ? Math.Round(Math.Abs(pos.EntryPrice - pos.StopLoss.Value) / symbol.PipSize, 1) : 0;
            double tpPips = pos.TakeProfit.HasValue ? Math.Round(Math.Abs(pos.EntryPrice - pos.TakeProfit.Value) / symbol.PipSize, 1) : 0;
            SendSignal("POSITION_MODIFY|" + pos.Id + "|" + pos.SymbolName + "|" + slPips + "|" + tpPips + "|" + pos.VolumeInUnits + "|" + Account.Balance);
        }

        private void Positions_Closed(PositionClosedEventArgs args)
        {
            try
            {
                Print("-> Master detectou FECHAMENTO da posição ID: {0}", args.Position.Id);
                SendSignal("POSITION_CLOSE|" + args.Position.Id);
            }
            catch (Exception ex)
            {
                Print("Erro no evento Positions_Closed: " + ex.Message);
            }
        }

        private void SendSignal(string line)
        {
            lock (_fileLock)
            {
                try
                {
                    using (var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(line);
                    }
                }
                catch (Exception ex)
                {
                    Print("Erro ao gravar sinal: " + ex.Message);
                }
            }
        }

        // ==========================================
        // PROCESSAMENTO NO SLAVE (Apenas Posições)
        // ==========================================
        private void ProcessSignal(string signal)
        {
            string[] parts = signal.Split('|');
            if (parts.Length < 2) return;

            string action = parts[0];
            string masterId = parts[1];

            switch (action)
            {
                case "POSITION_OPEN":
                    string symName = parts[2];
                    TradeType tType = (TradeType)Enum.Parse(typeof(TradeType), parts[3]);
                    double mUnits = double.Parse(parts[4]);
                    double mBalance = double.Parse(parts[5]);

                    // Verifica se essa posição já foi criada no Slave
                    var existingPos = Positions.FirstOrDefault(p => p.Label == masterId);

                    if (existingPos != null)
                    {
                        Print("-> [INFO] Posição para o ID {0} já existe no Slave. Ignorando abertura duplicada.", masterId);
                        break;
                    }

                    double sUnits = CalculateAndValidateUnits(mUnits, mBalance, symName);
                    if (sUnits > 0)
                    {
                        // Adicionamos o cast explícito (double?)null para remover a ambiguidade do compilador
                        ExecuteMarketOrder(tType, symName, sUnits, masterId, (double?)null, (double?)null);
                    }
                    break;

                case "POSITION_MODIFY":
                    string posSymName = parts[2];
                    double slPips = double.Parse(parts[3]);
                    double tpPips = double.Parse(parts[4]);
                    double masterCurrentUnits = double.Parse(parts[5]);
                    double masterCurrentBalance = double.Parse(parts[6]);
                    
                    var posToMod = Positions.FirstOrDefault(p => p.Label == masterId);
                    
                    if (posToMod != null)
                    {
                        var sym = Symbols.GetSymbol(posSymName);
                        double expectedSlaveUnits = CalculateAndValidateUnits(masterCurrentUnits, masterCurrentBalance, posSymName);
                        
                        // Executa Saída Parcial se o Master realizou parcial
                        if (posToMod.VolumeInUnits > expectedSlaveUnits && expectedSlaveUnits > 0)
                        {
                            double unitsToClose = posToMod.VolumeInUnits - expectedSlaveUnits;
                            unitsToClose = sym.NormalizeVolumeInUnits(unitsToClose);
                            if (unitsToClose > 0) ClosePosition(posToMod, unitsToClose);
                        }
                        
                        // Atualiza Stop Loss e Take Profit
                        double? sl = slPips > 0 ? (posToMod.TradeType == TradeType.Buy ? posToMod.EntryPrice - (slPips * sym.PipSize) : posToMod.EntryPrice + (slPips * sym.PipSize)) : (double?)null;
                        double? tp = tpPips > 0 ? (posToMod.TradeType == TradeType.Buy ? posToMod.EntryPrice + (tpPips * sym.PipSize) : posToMod.EntryPrice - (tpPips * sym.PipSize)) : (double?)null;
                        
                        ModifyPosition(posToMod, sl, tp, ProtectionType.Relative);
                    }
                    break;

                case "POSITION_CLOSE":
                    var posToClose = Positions.FirstOrDefault(p => p.Label == masterId);
                    if (posToClose != null) ClosePosition(posToClose);
                    break;
            }
        }

        private double CalculateAndValidateUnits(double masterUnits, double masterBalance, string symbolName)
        {
            var symbol = Symbols.GetSymbol(symbolName);
            double finalUnits = 0;

            if (VolumeMultiplier > 0)
            {
                finalUnits = masterUnits * VolumeMultiplier;
                Print("-> [CÁLCULO] Usando Multiplicador Fixo: {0}x. Unidades Master: {1} -> Unidades Slave: {2}", VolumeMultiplier, masterUnits, finalUnits);
            }
            else
            {
                double ratio = Account.Balance / masterBalance;
                finalUnits = masterUnits * ratio;
                Print("-> [CÁLCULO] Usando Proporção por Saldo (Auto). Proporção: {0:F2}x. Unidades Slave: {1}", ratio, finalUnits);
            }

            return symbol.NormalizeVolumeInUnits(finalUnits);
        }
    }
}