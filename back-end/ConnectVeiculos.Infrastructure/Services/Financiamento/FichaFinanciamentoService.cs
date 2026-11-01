using System.Net.Mail;
using System.Text.Json;
using ConnectVeiculos.Core.Entities.Financiamentos;
using ConnectVeiculos.Core.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Financiamentos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using ConnectVeiculos.Core.Interfaces.Services;

namespace ConnectVeiculos.Infrastructure.Services.Financiamento
{
    /// <summary>
    /// Ficha unica de financiamento. A estimativa usa so as tabelas de taxa
    /// que a loja cadastrou: nao consulta banco nem bureau de credito.
    /// </summary>
    public class FichaFinanciamentoService : IFichaFinanciamentoService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private const int PrazoMaximo = 72;

        private readonly IFinanciamentoLojaRepository _repo;
        private readonly IVeiculoRepository _veiculos;

        public FichaFinanciamentoService(IFinanciamentoLojaRepository repo, IVeiculoRepository veiculos)
        {
            _repo = repo;
            _veiculos = veiculos;
        }

        // ================================================================
        // Bancos da loja
        // ================================================================

        public async Task<IReadOnlyList<BancoLojaInfo>> ListarBancosAsync()
            => (await _repo.ListarBancosAsync()).Select(Info).ToList();

        public async Task<BancoLojaInfo> SalvarBancoAsync(BancoLojaInfo input)
        {
            var nome = (input.Nome ?? "").Trim();
            if (nome.Length is < 2 or > 80) throw new FinanciamentoException("Informe o nome do banco.");
            if (input.TarifaCadastro < 0 || input.RegistroContrato < 0) throw new FinanciamentoException("Tarifas não podem ser negativas.");
            var portal = string.IsNullOrWhiteSpace(input.PortalUrl) ? null : input.PortalUrl.Trim();
            if (portal != null && !(Uri.TryCreate(portal, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
                throw new FinanciamentoException("O endereço do portal precisa começar com https://.");

            var faixas = (input.Faixas ?? new()).Select(LimparFaixa).ToList();

            var banco = input.Id > 0
                ? await _repo.ObterBancoAsync(input.Id) ?? throw new FinanciamentoException("Banco não encontrado.")
                : new BancoFinanciamentoLoja();
            banco.Atualizar(nome, portal, input.Ativo, input.TarifaCadastro, input.RegistroContrato, input.TarifasFinanciadas,
                JsonSerializer.Serialize(faixas, Json), string.IsNullOrWhiteSpace(input.Observacao) ? null : input.Observacao.Trim());
            await _repo.SalvarBancoAsync(banco);
            return Info(banco);
        }

        private static FaixaTaxaBanco LimparFaixa(FaixaTaxaBanco f)
        {
            if (f.AnoDe is < 1950 or > 2100 || f.AnoAte is < 1950 or > 2100 || (f.AnoDe > f.AnoAte))
                throw new FinanciamentoException("Ano da faixa inválido.");
            if (f.EntradaMinimaPct is < 0 or >= 100) throw new FinanciamentoException("Entrada mínima deve ficar entre 0% e 99%.");
            var taxas = (f.Taxas ?? new())
                .Where(t => t.Meses > 0)
                .GroupBy(t => t.Meses)
                .Select(g => g.Last())
                .OrderBy(t => t.Meses)
                .ToList();
            foreach (var t in taxas)
            {
                if (t.Meses > PrazoMaximo) throw new FinanciamentoException($"Prazo máximo de {PrazoMaximo} meses.");
                if (t.TaxaMensal is < 0 or > 15) throw new FinanciamentoException("Taxa mensal deve ficar entre 0% e 15%.");
            }
            return new FaixaTaxaBanco { AnoDe = f.AnoDe, AnoAte = f.AnoAte, EntradaMinimaPct = f.EntradaMinimaPct, Taxas = taxas };
        }

        public async Task RemoverBancoAsync(int bancoId)
        {
            var banco = await _repo.ObterBancoAsync(bancoId) ?? throw new FinanciamentoException("Banco não encontrado.");
            await _repo.RemoverBancoAsync(banco);
        }

        // ================================================================
        // Estimativa em todos os bancos
        // ================================================================

        public async Task<IReadOnlyList<EstimativaBanco>> EstimarAsync(EstimativaInput input)
        {
            if (input.ValorVeiculo <= 0) throw new FinanciamentoException("Informe o valor do veículo.");
            if (input.Entrada < 0 || input.Entrada >= input.ValorVeiculo)
                throw new FinanciamentoException("A entrada precisa ser menor que o valor do veículo.");
            if (input.AnoVeiculo is < 1950 or > 2100) throw new FinanciamentoException("Informe o ano do veículo.");

            var bancos = (await _repo.ListarBancosAsync()).Where(b => b.BflAtivo).ToList();
            if (bancos.Count == 0) throw new FinanciamentoException("Cadastre os bancos da loja e as tabelas de taxa primeiro.");

            var liquido = input.ValorVeiculo - input.Entrada;
            var resultado = new List<EstimativaBanco>();
            foreach (var b in bancos)
            {
                var e = new EstimativaBanco { BancoId = b.BflId, Banco = b.BflNome, PortalUrl = b.BflPortalUrl };
                var (faixa, motivo) = CalculoFinanciamento.EscolherFaixa(Faixas(b), input.AnoVeiculo, input.ValorVeiculo, input.Entrada);
                if (faixa == null)
                {
                    e.Motivo = motivo;
                }
                else
                {
                    var tarifas = b.BflTarifaCadastro + b.BflRegistroContrato;
                    e.Prazos = faixa.Taxas
                        .Select(t => CalculoFinanciamento.Estimar(liquido, t.TaxaMensal, t.Meses, tarifas, b.BflTarifasFinanciadas))
                        .ToList();
                }
                resultado.Add(e);
            }

            // Quem financia vem primeiro, do menor CET para o maior.
            return resultado
                .OrderBy(r => r.Prazos.Count == 0)
                .ThenBy(r => r.Prazos.Count == 0 ? decimal.MaxValue : r.Prazos.Min(p => p.CetMensal))
                .ToList();
        }

        // ================================================================
        // Fichas
        // ================================================================

        public async Task<IReadOnlyList<FichaInfo>> ListarFichasAsync(int? vendedorId)
        {
            var fichas = await _repo.ListarFichasAsync(vendedorId);
            var nomes = await NomesVeiculosAsync(fichas.Where(f => f.R_VeiId.HasValue).Select(f => f.R_VeiId!.Value));
            return fichas.Select(f => Info(f, f.R_VeiId is { } v && nomes.TryGetValue(v, out var n) ? n : null)).ToList();
        }

        public async Task<FichaInfo> ObterFichaAsync(int fichaId, int? vendedorId)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            return await InfoComVeiculoAsync(ficha);
        }

        public async Task<FichaInfo> CriarFichaAsync(FichaInput input, int usuarioId)
        {
            if (!input.Consentimento)
                throw new FinanciamentoException("Sem a autorização do cliente não dá para consultar o crédito dele.");
            var ficha = new FichaFinanciamento(usuarioId, DateTime.UtcNow);
            await PreencherAsync(ficha, input);
            await _repo.SalvarFichaAsync(ficha);
            return await InfoComVeiculoAsync(ficha);
        }

        public async Task<FichaInfo> AtualizarFichaAsync(int fichaId, FichaInput input, int? vendedorId)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            await PreencherAsync(ficha, input);
            await _repo.SalvarFichaAsync(ficha);
            return await InfoComVeiculoAsync(ficha);
        }

        public async Task<FichaInfo> AlterarStatusAsync(int fichaId, string status, int? vendedorId)
        {
            status = (status ?? "").Trim().ToUpperInvariant();
            if (status is not (FichaFinanciamento.Aberta or FichaFinanciamento.Aprovada or FichaFinanciamento.Vendida or FichaFinanciamento.Perdida))
                throw new FinanciamentoException("Situação inválida.");
            var ficha = await FichaAsync(fichaId, vendedorId);
            ficha.AlterarStatus(status);
            await _repo.SalvarFichaAsync(ficha);
            return await InfoComVeiculoAsync(ficha);
        }

        public async Task<FichaInfo> RegistrarPropostaAsync(int fichaId, PropostaBanco proposta, int? vendedorId)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            var banco = await _repo.ObterBancoAsync(proposta.BancoId) ?? throw new FinanciamentoException("Banco não encontrado.");
            var status = (proposta.Status ?? "").Trim().ToUpperInvariant();
            if (!StatusProposta.Todos.Contains(status)) throw new FinanciamentoException("Situação da proposta inválida.");
            if (proposta.Meses is < 1 or > PrazoMaximo) throw new FinanciamentoException("Prazo inválido.");
            if (proposta.TaxaMensal is < 0 or > 15) throw new FinanciamentoException("Taxa mensal inválida.");
            if (proposta.ValorParcela < 0 || proposta.EntradaExigida < 0) throw new FinanciamentoException("Valores não podem ser negativos.");

            var lista = Propostas(ficha).Where(p => p.BancoId != banco.BflId).ToList();
            lista.Add(new PropostaBanco
            {
                BancoId = banco.BflId,
                Banco = banco.BflNome,
                Status = status,
                Meses = proposta.Meses,
                TaxaMensal = proposta.TaxaMensal,
                ValorParcela = proposta.ValorParcela,
                EntradaExigida = proposta.EntradaExigida,
                Observacao = Limitar(proposta.Observacao, 500),
                AtualizadoEm = DateTime.UtcNow
            });
            ficha.AtualizarPropostas(JsonSerializer.Serialize(lista.OrderBy(p => p.Banco).ToList(), Json));
            if (status == StatusProposta.Aprovada && ficha.FifStatus == FichaFinanciamento.Aberta)
                ficha.AlterarStatus(FichaFinanciamento.Aprovada);
            await _repo.SalvarFichaAsync(ficha);
            return await InfoComVeiculoAsync(ficha);
        }

        public async Task<FichaInfo> RemoverPropostaAsync(int fichaId, int bancoId, int? vendedorId)
        {
            var ficha = await FichaAsync(fichaId, vendedorId);
            ficha.AtualizarPropostas(JsonSerializer.Serialize(Propostas(ficha).Where(p => p.BancoId != bancoId).ToList(), Json));
            await _repo.SalvarFichaAsync(ficha);
            return await InfoComVeiculoAsync(ficha);
        }

        // ================================================================
        // Auxiliares
        // ================================================================

        private async Task<FichaFinanciamento> FichaAsync(int fichaId, int? vendedorId)
        {
            var ficha = await _repo.ObterFichaAsync(fichaId);
            // Vendedor so enxerga as proprias fichas: a de outro "nao existe" para ele.
            if (ficha == null || (vendedorId.HasValue && ficha.R_UsuId != vendedorId.Value))
                throw new FinanciamentoException("Ficha não encontrada.");
            return ficha;
        }

        private async Task PreencherAsync(FichaFinanciamento ficha, FichaInput d)
        {
            var nome = string.Join(' ', (d.Nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (nome.Length < 5 || !nome.Contains(' ')) throw new FinanciamentoException("Informe o nome completo do cliente.");
            if (nome.Length > 150) throw new FinanciamentoException("Nome muito longo.");

            var cpf = SoDigitos(d.Cpf);
            if (cpf.Length != 11 || !Renave.RenaveService.DocumentoValido(cpf)) throw new FinanciamentoException("CPF inválido.");

            var telefone = SoDigitos(d.Telefone);
            if (telefone.Length is < 10 or > 11) throw new FinanciamentoException("Telefone inválido (DDD + número).");

            var email = (d.Email ?? "").Trim();
            if (email.Length > 150 || !MailAddress.TryCreate(email, out var mail) || mail.Address != email || !email.Contains('.'))
                throw new FinanciamentoException("E-mail inválido.");

            var valor = d.ValorVeiculo;
            var ano = d.AnoVeiculo;
            if (d.VeiculoId is { } veiculoId)
            {
                var v = await _veiculos.GetByIdAsync(veiculoId) ?? throw new FinanciamentoException("Veículo não encontrado.");
                if (valor <= 0) valor = v.VeiPreco;
                if (ano <= 0) ano = v.VeiAno;
            }
            if (valor <= 0) throw new FinanciamentoException("Informe o valor do veículo.");
            if (d.Entrada < 0 || d.Entrada >= valor) throw new FinanciamentoException("A entrada precisa ser menor que o valor do veículo.");
            if (ano is < 1950 or > 2100) throw new FinanciamentoException("Informe o ano do veículo.");

            ficha.AtualizarCliente(nome, cpf, telefone, email.ToLowerInvariant());
            ficha.AtualizarNegocio(d.VeiculoId, valor, d.Entrada, ano, Limitar(d.Observacao, 1000));
        }

        private async Task<Dictionary<int, string>> NomesVeiculosAsync(IEnumerable<int> ids)
        {
            var nomes = new Dictionary<int, string>();
            foreach (var id in ids.Distinct())
            {
                var v = await _veiculos.GetByIdAsync(id);
                if (v != null) nomes[id] = $"{v.VeiMarca} {v.VeiModelo} {v.VeiAno}" + (string.IsNullOrWhiteSpace(v.VeiPlaca) ? "" : $" · {v.VeiPlaca}");
            }
            return nomes;
        }

        private async Task<FichaInfo> InfoComVeiculoAsync(FichaFinanciamento f)
        {
            var nomes = f.R_VeiId.HasValue ? await NomesVeiculosAsync(new[] { f.R_VeiId.Value }) : new();
            return Info(f, f.R_VeiId is { } v && nomes.TryGetValue(v, out var n) ? n : null);
        }

        private static List<FaixaTaxaBanco> Faixas(BancoFinanciamentoLoja b)
        {
            try { return JsonSerializer.Deserialize<List<FaixaTaxaBanco>>(b.BflFaixasJson ?? "[]", Json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static List<PropostaBanco> Propostas(FichaFinanciamento f)
        {
            try { return JsonSerializer.Deserialize<List<PropostaBanco>>(f.FifPropostasJson ?? "[]", Json) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private static BancoLojaInfo Info(BancoFinanciamentoLoja b) => new()
        {
            Id = b.BflId,
            Nome = b.BflNome,
            PortalUrl = b.BflPortalUrl,
            Ativo = b.BflAtivo,
            TarifaCadastro = b.BflTarifaCadastro,
            RegistroContrato = b.BflRegistroContrato,
            TarifasFinanciadas = b.BflTarifasFinanciadas,
            Faixas = Faixas(b),
            Observacao = b.BflObservacao
        };

        private static FichaInfo Info(FichaFinanciamento f, string? veiculo) => new()
        {
            Id = f.FifId,
            VeiculoId = f.R_VeiId,
            Veiculo = veiculo,
            VendedorId = f.R_UsuId,
            Nome = f.FifNome,
            Cpf = f.FifCpf,
            Telefone = f.FifTelefone,
            Email = f.FifEmail,
            ValorVeiculo = f.FifValorVeiculo,
            Entrada = f.FifEntrada,
            AnoVeiculo = f.FifAnoVeiculo,
            Status = f.FifStatus,
            Observacao = f.FifObservacao,
            ConsentimentoEm = f.FifConsentimentoEm,
            CriadoEm = f.FifCriadoEm,
            AtualizadoEm = f.FifAtualizadoEm,
            Propostas = Propostas(f)
        };

        private static string SoDigitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

        private static string? Limitar(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            return s.Length > max ? s[..max] : s;
        }
    }
}
