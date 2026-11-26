using ConnectVeiculos.Core.Entities.Financeiro;
using ConnectVeiculos.Core.Exceptions;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Database.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConnectVeiculos.Infrastructure.Services.Financeiro
{
    /// <inheritdoc cref="IFinanceiroService"/>
    public class FinanceiroService : IFinanceiroService
    {
        private readonly ConnectVeiculosDbContext _db;

        /// <summary>Para os testes: "hoje" fixo.</summary>
        public Func<DateTime> Hoje { get; set; } = () => DateTime.Today;

        public FinanceiroService(ConnectVeiculosDbContext db)
        {
            _db = db;
        }

        public async Task<FinanceiroMes> MesAsync(int ano, int mes, int? lojaId)
        {
            if (ano < 2000 || ano > 2100 || mes < 1 || mes > 12) throw new FinanceiroException("Mês inválido.");
            var inicio = new DateTime(ano, mes, 1);
            var fim = inicio.AddMonths(1);
            var hoje = Hoje().Date;

            // SQLite guarda decimal como texto: filtra por data no banco e soma em memoria.
            var lancamentos = (await _db.LancamentosFinanceiros.AsNoTracking()
                    .Where(l => (l.LfiVencimento >= inicio && l.LfiVencimento < fim)
                                || (l.LfiPagoEm != null && l.LfiPagoEm >= inicio && l.LfiPagoEm < fim)
                                || (l.LfiPagoEm == null && l.LfiVencimento < hoje))
                    .ToListAsync())
                .Where(l => lojaId == null || l.R_LojId == null || l.R_LojId == lojaId)
                .ToList();

            var veiculos = (await _db.Veiculos.AsNoTracking()
                    .Select(v => new { v.VeiId, v.R_LojId, v.VeiMarca, v.VeiModelo, v.VeiAno, v.VeiPlaca, v.VeiPrecoCompra, v.VeiDtEntrada })
                    .ToListAsync())
                .ToDictionary(v => v.VeiId);
            bool DaLoja(int veiculoId) => lojaId == null || (veiculos.TryGetValue(veiculoId, out var v) && v.R_LojId == lojaId);
            string Carro(int veiculoId) => veiculos.TryGetValue(veiculoId, out var v)
                ? $"{(v.VeiMarca ?? "").Split(" - ").Last()} {v.VeiModelo} {v.VeiAno}".Trim() + (string.IsNullOrWhiteSpace(v.VeiPlaca) ? "" : $" ({v.VeiPlaca})")
                : $"veículo #{veiculoId}";

            var movimentos = new List<MovimentoCaixa>();

            // Lancamentos: realizado na data do pagamento; previsto no vencimento.
            foreach (var l in lancamentos)
            {
                var data = l.LfiPagoEm ?? l.LfiVencimento;
                if (data < inicio || data >= fim) continue;
                movimentos.Add(new MovimentoCaixa
                {
                    Data = data,
                    Sentido = l.LfiTipo == LancamentoFinanceiro.Receber ? "entrada" : "saida",
                    Descricao = l.LfiDescricao,
                    Categoria = l.LfiCategoria,
                    Valor = l.LfiValor,
                    Origem = "lancamento",
                    Realizado = l.LfiPagoEm != null,
                    LancamentoId = l.LfiId
                });
            }

            // Vendas ativas do mes: entrada.
            var vendas = await _db.Vendas.AsNoTracking()
                .Where(v => v.VenStatus != "E" && v.VenDtVenda >= inicio && v.VenDtVenda < fim)
                .Select(v => new { v.VenId, v.R_VeiId, v.VenDtVenda, v.VenValor, v.VenCompradorNome })
                .ToListAsync();
            movimentos.AddRange(vendas.Where(v => DaLoja(v.R_VeiId)).Select(v => new MovimentoCaixa
            {
                Data = v.VenDtVenda.Date,
                Sentido = "entrada",
                Descricao = $"Venda: {Carro(v.R_VeiId)}" + (string.IsNullOrWhiteSpace(v.VenCompradorNome) ? "" : $" — {v.VenCompradorNome}"),
                Categoria = "Venda de veículo",
                Valor = v.VenValor,
                Origem = "venda",
                Realizado = true,
                VeiculoId = v.R_VeiId
            }));

            // Carros que entraram no mes: saida (preco de compra).
            movimentos.AddRange(veiculos.Values
                .Where(v => v.VeiPrecoCompra > 0 && v.VeiDtEntrada >= inicio && v.VeiDtEntrada < fim && (lojaId == null || v.R_LojId == lojaId))
                .Select(v => new MovimentoCaixa
                {
                    Data = v.VeiDtEntrada.Date,
                    Sentido = "saida",
                    Descricao = $"Compra: {Carro(v.VeiId)}",
                    Categoria = "Compra de veículo",
                    Valor = v.VeiPrecoCompra,
                    Origem = "compra",
                    Realizado = true,
                    VeiculoId = v.VeiId
                }));

            // Despesas dos carros no mes: saida.
            var despesas = await _db.VeiculosDespesas.AsNoTracking()
                .Where(d => d.DesDtDespesa >= inicio && d.DesDtDespesa < fim)
                .ToListAsync();
            movimentos.AddRange(despesas.Where(d => DaLoja(d.R_VeiId)).Select(d => new MovimentoCaixa
            {
                Data = d.DesDtDespesa.Date,
                Sentido = "saida",
                Descricao = $"{(string.IsNullOrWhiteSpace(d.DesDescricao) ? d.DesTipo : d.DesDescricao)}: {Carro(d.R_VeiId)}",
                Categoria = "Despesa de veículo",
                Valor = d.DesValor,
                Origem = "despesa",
                Realizado = true,
                VeiculoId = d.R_VeiId
            }));

            movimentos = movimentos.OrderBy(m => m.Data).ThenBy(m => m.Sentido).ToList();
            var r = new FinanceiroMes
            {
                Ano = ano,
                Mes = mes,
                EntradasRealizadas = movimentos.Where(m => m.Sentido == "entrada" && m.Realizado).Sum(m => m.Valor),
                SaidasRealizadas = movimentos.Where(m => m.Sentido == "saida" && m.Realizado).Sum(m => m.Valor),
                EntradasPrevistas = movimentos.Where(m => m.Sentido == "entrada" && !m.Realizado).Sum(m => m.Valor),
                SaidasPrevistas = movimentos.Where(m => m.Sentido == "saida" && !m.Realizado).Sum(m => m.Valor),
                Movimentos = movimentos,
                SaidasPorCategoria = movimentos.Where(m => m.Sentido == "saida" && m.Realizado)
                    .GroupBy(m => m.Categoria).OrderByDescending(g => g.Sum(m => m.Valor))
                    .ToDictionary(g => g.Key, g => g.Sum(m => m.Valor))
            };
            r.SaldoRealizado = r.EntradasRealizadas - r.SaidasRealizadas;
            r.SaldoPrevisto = r.SaldoRealizado + r.EntradasPrevistas - r.SaidasPrevistas;

            var atrasadas = lancamentos.Where(l => l.EmAberto && l.LfiVencimento < hoje).ToList();
            r.Atrasadas = atrasadas.Count;
            r.ValorAtrasado = atrasadas.Where(l => l.LfiTipo == LancamentoFinanceiro.Pagar).Sum(l => l.LfiValor);
            r.VencemEmBreve = await ContarVencendoAsync(hoje, lojaId);

            r.Lancamentos = lancamentos
                .Where(l => (l.LfiVencimento >= inicio && l.LfiVencimento < fim) || (l.EmAberto && l.LfiVencimento < hoje))
                .OrderBy(l => l.LfiPagoEm != null).ThenBy(l => l.LfiVencimento)
                .Select(l => Info(l, hoje)).ToList();
            return r;
        }

        private async Task<int> ContarVencendoAsync(DateTime hoje, int? lojaId)
        {
            var limite = hoje.AddDays(8);
            return (await _db.LancamentosFinanceiros.AsNoTracking()
                    .Where(l => l.LfiPagoEm == null && l.LfiTipo == LancamentoFinanceiro.Pagar && l.LfiVencimento >= hoje && l.LfiVencimento < limite)
                    .Select(l => l.R_LojId).ToListAsync())
                .Count(loja => lojaId == null || loja == null || loja == lojaId);
        }

        public async Task<LancamentoInfo> CriarAsync(LancamentoInput input, int usuarioId)
        {
            var l = Validar(() => new LancamentoFinanceiro(input.Tipo, input.Descricao, input.Categoria ?? "", input.Valor,
                input.Vencimento, input.LojaId, input.Observacao ?? "", input.Recorrente, usuarioId));
            if (input.PagoEm != null) l.MarcarPago(input.PagoEm);
            _db.LancamentosFinanceiros.Add(l);
            await _db.SaveChangesAsync();
            return Info(l, Hoje().Date);
        }

        public async Task<LancamentoInfo> AlterarAsync(int id, LancamentoInput input)
        {
            var l = await BuscarAsync(id);
            Validar(() => { l.Alterar(input.Tipo, input.Descricao, input.Categoria ?? "", input.Valor, input.Vencimento, input.LojaId, input.Observacao ?? "", input.Recorrente); return l; });
            await _db.SaveChangesAsync();
            return Info(l, Hoje().Date);
        }

        public async Task ExcluirAsync(int id)
        {
            _db.LancamentosFinanceiros.Remove(await BuscarAsync(id));
            await _db.SaveChangesAsync();
        }

        public async Task<LancamentoInfo> PagarAsync(int id, DateTime? pagoEm)
        {
            var l = await BuscarAsync(id);
            var estavaAberto = l.EmAberto;
            l.MarcarPago(pagoEm);
            // Recorrente: pagar o deste mes ja deixa o proximo lancado (uma vez so).
            if (estavaAberto && pagoEm != null && l.LfiRecorrente)
            {
                var proximo = l.ProximoDaRecorrencia();
                var jaExiste = await _db.LancamentosFinanceiros.AnyAsync(x =>
                    x.LfiTipo == proximo.LfiTipo && x.LfiDescricao == proximo.LfiDescricao && x.LfiVencimento == proximo.LfiVencimento);
                if (!jaExiste) _db.LancamentosFinanceiros.Add(proximo);
            }
            await _db.SaveChangesAsync();
            return Info(l, Hoje().Date);
        }

        private async Task<LancamentoFinanceiro> BuscarAsync(int id) =>
            await _db.LancamentosFinanceiros.FirstOrDefaultAsync(l => l.LfiId == id)
            ?? throw new FinanceiroException("Lançamento não encontrado.");

        private static LancamentoFinanceiro Validar(Func<LancamentoFinanceiro> acao)
        {
            try { return acao(); }
            catch (DomainException ex) { throw new FinanceiroException(ex.Message); }
        }

        private static LancamentoInfo Info(LancamentoFinanceiro l, DateTime hoje) => new()
        {
            Id = l.LfiId,
            Tipo = l.LfiTipo,
            Descricao = l.LfiDescricao,
            Categoria = l.LfiCategoria,
            Valor = l.LfiValor,
            Vencimento = l.LfiVencimento,
            PagoEm = l.LfiPagoEm,
            LojaId = l.R_LojId,
            Observacao = l.LfiObservacao,
            Recorrente = l.LfiRecorrente,
            Situacao = l.LfiPagoEm != null ? "pago"
                : l.LfiVencimento < hoje ? "atrasado"
                : l.LfiVencimento == hoje ? "vence_hoje" : "aberto"
        };
    }
}
