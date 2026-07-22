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
        //private string _lastReadContent = "";
        private readonly object _fileLock = new object();

        protected override void OnStart()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _filePath = Path.Combine(docPath, "cTrader_Unified_Bridge.txt");
            Print("CAMINHO REAL DO ARQUIVO: " + _filePath);

            if (Role == AccountRole.Master)
            {
                Print("Modo MASTER Ativado. Monitorando ordens...");
                PendingOrders.Created += PendingOrders_Created;
                PendingOrders.Modified += PendingOrders_Modified;
                PendingOrders.Cancelled += PendingOrders_Cancelled;
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
                        // Sobrescreve o arquivo deixando-o completamente em branco
                        System.IO.File.WriteAllText(_filePath, string.Empty);
                        Print("-> [PROTEÇÃO] Arquivo de ponte antigo limpo com sucesso para evitar ordens fantasmas.");
                    }
                }
                catch (Exception ex)
                {
                    Print("-> [AVISO] Não foi possível limpar o arquivo no início: " + ex.Message);
                }
                // ================================================================

                
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
                    // Lê todas as linhas acumuladas no arquivo
                    using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        var content = sr.ReadToEnd();
                        if (string.IsNullOrEmpty(content)) return;
                        
                        lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    // Se leu comandos com sucesso, limpa o arquivo imediatamente para os próximos sinais
                    System.IO.File.WriteAllText(_filePath, string.Empty);
                }

                // Processa cada sinal na ordem exata em que eles aconteceram
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
        // EVENTOS DO MASTER
        // ==========================================
        private void PendingOrders_Created(PendingOrderCreatedEventArgs args)
        {
            try
            {
                var ord = args.PendingOrder;
                Print("-> Master detectou NOVA ORDEM PENDENTE criada! ID: {0}, Ativo: {1}", ord.Id, ord.SymbolName);
                
                SendSignal("PENDING_CREATE|" + ord.Id + "|" + ord.SymbolName + "|" + ord.OrderType + "|" + ord.TradeType + "|" + ord.TargetPrice + "|" + ord.VolumeInUnits + "|" + Account.Balance);
                TriggerPendingModification(ord);
            }
            catch (Exception ex)
            {
                Print("Erro no evento PendingOrders_Created: " + ex.Message);
            }
        }

        private void PendingOrders_Modified(PendingOrderModifiedEventArgs args)
        {
            try
            {
                Print("-> Master detectou MODIFICAÇÃO na ordem pendente ID: {0}", args.PendingOrder.Id);
                TriggerPendingModification(args.PendingOrder);
            }
            catch (Exception ex)
            {
                Print("Erro no evento PendingOrders_Modified: " + ex.Message);
            }
        }

        private void PendingOrders_Cancelled(PendingOrderCancelledEventArgs args)
        {
            try
            {
                Print("-> Master detectou CANCELAMENTO da ordem pendente ID: {0}", args.PendingOrder.Id);
                SendSignal("PENDING_CANCEL|" + args.PendingOrder.Id);
            }
            catch (Exception ex)
            {
                Print("Erro no evento PendingOrders_Cancelled: " + ex.Message);
            }
        }

        private void TriggerPendingModification(PendingOrder ord)
        {
            var symbol = Symbols.GetSymbol(ord.SymbolName);
            double slPips = ord.StopLoss.HasValue ? Math.Round(Math.Abs(ord.TargetPrice - ord.StopLoss.Value) / symbol.PipSize, 1) : 0;
            double tpPips = ord.TakeProfit.HasValue ? Math.Round(Math.Abs(ord.TargetPrice - ord.TakeProfit.Value) / symbol.PipSize, 1) : 0;
            SendSignal("PENDING_MODIFY|" + ord.Id + "|" + ord.SymbolName + "|" + ord.TargetPrice + "|" + slPips + "|" + tpPips);
        }

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
                    // Mudamos para Append: o Master agora ADICIONA linhas no final, sem apagar o que veio antes
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
        // PROCESSAMENTO NO SLAVE
        // ==========================================
        private void ProcessSignal(string signal)
        {
            string[] parts = signal.Split('|');
            if (parts.Length < 2) return;

            string action = parts[0];
            string masterId = parts[1];

            switch (action)
            {
                case "PENDING_CREATE":
                    string symName = parts[2];
                    PendingOrderType ordType = (PendingOrderType)Enum.Parse(typeof(PendingOrderType), parts[3]);
                    TradeType tType = (TradeType)Enum.Parse(typeof(TradeType), parts[4]);
                    double targetPrice = double.Parse(parts[5]);
                    double pUnits = CalculateAndValidateUnits(double.Parse(parts[6]), double.Parse(parts[7]), symName);
                    
                    if (pUnits > 0) PlacePendingOrder(ordType, symName, pUnits, targetPrice, masterId, tType);
                    break;

              case "PENDING_MODIFY":
                    // 1. Declarar e ler todas as variáveis do sinal primeiro
                    string pendingSymName = parts[2];
                    double newTargetPrice = double.Parse(parts[3]);
                    double pSlPips = double.Parse(parts[4]);
                    double pTpPips = double.Parse(parts[5]);
                    
                    // 2. Localizar a ordem pendente no Slave usando o ID do Master como Label
                    var pendingOrd = PendingOrders.FirstOrDefault(o => o.Label == masterId);
                    
                    if (pendingOrd != null)
                    {
                        // Passamos apenas o valor puro dos pips (ou nulo se for 0)
                        double? slParam = pSlPips > 0 ? pSlPips : (double?)null;
                        double? tpParam = pTpPips > 0 ? pTpPips : (double?)null;
                        
                        // Usamos a assinatura moderna oficial com o Tipo de Proteção explícito (cAlgo.API.ProtectionType.Pips)
                        ModifyPendingOrder(
                            pendingOrd, 
                            newTargetPrice, 
                            slParam, 
                            tpParam, 
                            ProtectionType.Relative, 
                            pendingOrd.ExpirationTime
                        );
                    }
                    break;

                case "PENDING_CANCEL":
                    var ordToCancel = PendingOrders.FirstOrDefault(o => o.Label == masterId);
                    if (ordToCancel != null) CancelPendingOrder(ordToCancel);
                    break;

                case "POSITION_OPEN":
                    string masterPosId = parts[1];
                    symName = parts[2];
                    tType = (TradeType)Enum.Parse(typeof(TradeType), parts[3]);
                    double mUnits = double.Parse(parts[4]);
                    double mBalance = double.Parse(parts[5]);

                    // ================================================================
                    // TRAVA ANTIDUPLICAÇÃO: 
                    // Verifica se já existe uma ordem (pendente ou executada) com esse ID no Label
                    // ================================================================
                    var existingPos = Positions.FirstOrDefault(p => p.Label == masterPosId);
                    var existingPend = PendingOrders.FirstOrDefault(o => o.Label == masterPosId);

                    if (existingPos != null || existingPend != null)
                    {
                        Print("-> [INFO] Posição/Ordem para o ID {0} já existe no Slave. Ignorando abertura duplicada.", masterPosId);
                        break; // Aborta a abertura de uma nova ordem a mercado
                    }
                    // ================================================================

                    // Se não existir, aí sim o Slave abre a ordem normalmente
                    double sUnits = CalculateAndValidateUnits(mUnits, mBalance, symName);
                    ExecuteMarketOrder(tType, symName, sUnits, masterPosId, null, null);
                    break;

                case "POSITION_MODIFY":
                    // 1. Declarar e ler todas as variáveis do sinal primeiro
                    string posSymName = parts[2];
                    double slPips = double.Parse(parts[3]);
                    double tpPips = double.Parse(parts[4]);
                    double masterCurrentUnits = double.Parse(parts[5]);
                    double masterCurrentBalance = double.Parse(parts[6]);
                    
                    // 2. Localizar a posição no Slave usando o ID do Master como Label
                    var posToMod = Positions.FirstOrDefault(p => p.Label == masterId);
                    
                    if (posToMod != null)
                    {
                        var sym = Symbols.GetSymbol(posSymName);
                        double expectedSlaveUnits = CalculateAndValidateUnits(masterCurrentUnits, masterCurrentBalance, posSymName);
                        
                        // Executa Saída Parcial se necessário
                        if (posToMod.VolumeInUnits > expectedSlaveUnits && expectedSlaveUnits > 0)
                        {
                            double unitsToClose = posToMod.VolumeInUnits - expectedSlaveUnits;
                            unitsToClose = sym.NormalizeVolumeInUnits(unitsToClose);
                            if (unitsToClose > 0) ClosePosition(posToMod, unitsToClose);
                        }
                        
                        // Cálculo corrigido com o cast explícito para (double?)
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

            // Se o usuário digitou um valor maior que 0 (ex: 3 ou 0.5)
            if (VolumeMultiplier > 0)
            {
                // Calcula o lote estritamente baseado no multiplicador fixo
                finalUnits = masterUnits * VolumeMultiplier;
                Print("-> [CÁLCULO] Usando Multiplicador Fixo: {0}x. Unidades Master: {1} -> Unidades Slave: {2}", VolumeMultiplier, masterUnits, finalUnits);
            }
            else
            {
                // Se for 0, mantém o cálculo clássico automático baseado na proporção dos saldos
                double ratio = Account.Balance / masterBalance;
                finalUnits = masterUnits * ratio;
                Print("-> [CÁLCULO] Usando Proporção por Saldo (Auto). Proporção: {0:F2}x. Unidades Slave: {1}", ratio, finalUnits);
            }

            // Normaliza o valor final de acordo com o lote mínimo e passos permitidos pela corretora do Slave
            return symbol.NormalizeVolumeInUnits(finalUnits);
        }

        private void PlacePendingOrder(PendingOrderType oType, string sym, double units, double price, string label, TradeType tType)
        {
            if (oType == PendingOrderType.Limit) 
                PlaceLimitOrder(tType, sym, units, price, label);
            else if (oType == PendingOrderType.Stop) 
                PlaceStopOrder(tType, sym, units, price, label);
        }
    }
}