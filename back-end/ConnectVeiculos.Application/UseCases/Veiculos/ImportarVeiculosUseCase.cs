using ConnectVeiculos.Application.InputModels.Veiculos;
using ConnectVeiculos.Application.Interfaces.Veiculos;
using ConnectVeiculos.Core.Entities.Veiculos;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Categorias;
using ConnectVeiculos.Core.Interfaces.Database.Repositories.Veiculos;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace ConnectVeiculos.Application.UseCases.Veiculos
{
    public class ImportarVeiculosUseCase : IImportarVeiculosUseCase
    {
        private readonly IVeiculoRepository _veiculoRepository;
        private readonly ICategoriaRepository _categoriaRepository;

        public ImportarVeiculosUseCase(
            IVeiculoRepository veiculoRepository,
            ICategoriaRepository categoriaRepository)
        {
            _veiculoRepository = veiculoRepository;
            _categoriaRepository = categoriaRepository;
        }

        public async Task<ImportacaoResultado> Execute(IFormFile arquivo, int lojaId)
        {
            var resultado = new ImportacaoResultado();
            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();

            List<ImportacaoVeiculoInputModel> veiculos;

            // Parse do arquivo baseado na extensao
            if (extensao == ".csv")
            {
                veiculos = await ParseCsv(arquivo);
            }
            else if (extensao == ".xml")
            {
                veiculos = await ParseXml(arquivo);
            }
            else
            {
                resultado.Erros = 1;
                resultado.Detalhes.Add(new ImportacaoVeiculoResultado
                {
                    Linha = 0,
                    Sucesso = false,
                    Mensagem = "Formato de arquivo não suportado. Use CSV ou XML."
                });
                return resultado;
            }

            resultado.TotalLinhas = veiculos.Count;

            // Buscar categorias para mapear nome -> id
            var categorias = await _categoriaRepository.GetAllAsync();
            var categoriasDict = categorias.ToDictionary(c => c.CatNome.ToLowerInvariant(), c => c.CatId);

            // Processar cada veiculo
            int linha = 1;
            foreach (var veiculoInput in veiculos)
            {
                linha++;
                var detalhe = new ImportacaoVeiculoResultado
                {
                    Linha = linha,
                    Placa = veiculoInput.Placa,
                    Modelo = veiculoInput.Modelo
                };

                try
                {
                    // Validar campos obrigatorios
                    if (string.IsNullOrWhiteSpace(veiculoInput.Marca))
                    {
                        detalhe.Sucesso = false;
                        detalhe.Mensagem = "Marca é obrigatória";
                        resultado.Erros++;
                        resultado.Detalhes.Add(detalhe);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(veiculoInput.Modelo))
                    {
                        detalhe.Sucesso = false;
                        detalhe.Mensagem = "Modelo é obrigatório";
                        resultado.Erros++;
                        resultado.Detalhes.Add(detalhe);
                        continue;
                    }

                    // Buscar categoria pelo nome ou usar padrao
                    int categoriaId = 1; // Categoria padrao
                    if (!string.IsNullOrWhiteSpace(veiculoInput.Categoria))
                    {
                        var categoriaNome = veiculoInput.Categoria.ToLowerInvariant();
                        if (categoriasDict.TryGetValue(categoriaNome, out int catId))
                        {
                            categoriaId = catId;
                        }
                    }

                    // Verificar se placa ja existe
                    if (!string.IsNullOrWhiteSpace(veiculoInput.Placa))
                    {
                        var existente = await _veiculoRepository.GetByPlacaAsync(veiculoInput.Placa);
                        if (existente != null)
                        {
                            detalhe.Sucesso = false;
                            detalhe.Mensagem = $"Veículo com placa {veiculoInput.Placa} já existe";
                            resultado.Erros++;
                            resultado.Detalhes.Add(detalhe);
                            continue;
                        }
                    }

                    // Criar veiculo
                    var veiculo = new Veiculo(
                        veiId: 0,
                        rLojId: lojaId,
                        rCatId: categoriaId,
                        veiMarca: veiculoInput.Marca?.Trim() ?? "",
                        veiModelo: veiculoInput.Modelo?.Trim() ?? "",
                        veiAno: veiculoInput.Ano > 0 ? veiculoInput.Ano : (short)DateTime.Now.Year,
                        veiPlaca: veiculoInput.Placa?.Trim().ToUpperInvariant() ?? "",
                        veiChassi: veiculoInput.Chassi?.Trim().ToUpperInvariant() ?? "",
                        veiCor: veiculoInput.Cor?.Trim() ?? "",
                        veiKm: veiculoInput.Km >= 0 ? veiculoInput.Km : 0,
                        veiPreco: veiculoInput.Preco >= 0 ? veiculoInput.Preco : 0,
                        veiDtEntrada: veiculoInput.DataEntrada ?? DateTime.Now,
                        veiSts: !string.IsNullOrWhiteSpace(veiculoInput.Status) ? veiculoInput.Status : "Disponivel",
                        veiSitSts: !string.IsNullOrWhiteSpace(veiculoInput.Situacao) ? veiculoInput.Situacao : "Ativo",
                        veiPrecoCompra: veiculoInput.PrecoCompra >= 0 ? veiculoInput.PrecoCompra : 0
                    );

                    var novoId = await _veiculoRepository.CreateAsync(veiculo);

                    detalhe.Sucesso = true;
                    detalhe.Mensagem = "Importado com sucesso";
                    detalhe.VeiculoId = novoId;
                    resultado.Importados++;
                }
                catch (Exception ex)
                {
                    detalhe.Sucesso = false;
                    detalhe.Mensagem = $"Erro: {ex.Message}";
                    resultado.Erros++;
                }

                resultado.Detalhes.Add(detalhe);
            }

            return resultado;
        }

        private async Task<List<ImportacaoVeiculoInputModel>> ParseCsv(IFormFile arquivo)
        {
            var veiculos = new List<ImportacaoVeiculoInputModel>();

            using var reader = new StreamReader(arquivo.OpenReadStream(), Encoding.UTF8);

            // Ler cabecalho
            var header = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(header))
                return veiculos;

            var colunas = header.Split(';', ',')
                .Select(c => c.Trim().ToLowerInvariant().Replace("\"", ""))
                .ToArray();

            // Mapear indices das colunas
            var colIndex = new Dictionary<string, int>();
            for (int i = 0; i < colunas.Length; i++)
            {
                colIndex[colunas[i]] = i;
            }

            // Ler linhas de dados
            while (!reader.EndOfStream)
            {
                var linha = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                var valores = ParseCsvLine(linha);
                if (valores.Length == 0)
                    continue;

                var veiculo = new ImportacaoVeiculoInputModel
                {
                    Marca = GetCsvValue(valores, colIndex, "marca"),
                    Modelo = GetCsvValue(valores, colIndex, "modelo"),
                    Ano = ParseShort(GetCsvValue(valores, colIndex, "ano")),
                    Placa = GetCsvValue(valores, colIndex, "placa"),
                    Chassi = GetCsvValue(valores, colIndex, "chassi"),
                    Cor = GetCsvValue(valores, colIndex, "cor"),
                    Km = ParseInt(GetCsvValue(valores, colIndex, "km", "quilometragem")),
                    Preco = ParseDecimal(GetCsvValue(valores, colIndex, "preco", "preço", "valor")),
                    PrecoCompra = ParseDecimal(GetCsvValue(valores, colIndex, "precocompra", "preco_compra", "custo")),
                    Categoria = GetCsvValue(valores, colIndex, "categoria", "tipo"),
                    DataEntrada = ParseDate(GetCsvValue(valores, colIndex, "dataentrada", "data_entrada", "data")),
                    Status = GetCsvValue(valores, colIndex, "status"),
                    Situacao = GetCsvValue(valores, colIndex, "situacao", "situação")
                };

                veiculos.Add(veiculo);
            }

            return veiculos;
        }

        private string[] ParseCsvLine(string linha)
        {
            var valores = new List<string>();
            var atual = new StringBuilder();
            bool dentroAspas = false;
            char separador = linha.Contains(';') ? ';' : ',';

            foreach (char c in linha)
            {
                if (c == '"')
                {
                    dentroAspas = !dentroAspas;
                }
                else if (c == separador && !dentroAspas)
                {
                    valores.Add(atual.ToString().Trim());
                    atual.Clear();
                }
                else
                {
                    atual.Append(c);
                }
            }
            valores.Add(atual.ToString().Trim());

            return valores.ToArray();
        }

        private string GetCsvValue(string[] valores, Dictionary<string, int> colIndex, params string[] nomesColunas)
        {
            foreach (var nome in nomesColunas)
            {
                if (colIndex.TryGetValue(nome, out int index) && index < valores.Length)
                {
                    return valores[index].Trim().Replace("\"", "");
                }
            }
            return "";
        }

        private async Task<List<ImportacaoVeiculoInputModel>> ParseXml(IFormFile arquivo)
        {
            var veiculos = new List<ImportacaoVeiculoInputModel>();

            using var stream = arquivo.OpenReadStream();
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

            // Suporta diferentes estruturas XML
            var elementos = doc.Descendants("veiculo")
                .Concat(doc.Descendants("Veiculo"))
                .Concat(doc.Descendants("VEICULO"))
                .Concat(doc.Descendants("item"))
                .Concat(doc.Descendants("Item"));

            foreach (var elem in elementos)
            {
                var veiculo = new ImportacaoVeiculoInputModel
                {
                    Marca = GetXmlValue(elem, "marca", "Marca", "MARCA"),
                    Modelo = GetXmlValue(elem, "modelo", "Modelo", "MODELO"),
                    Ano = ParseShort(GetXmlValue(elem, "ano", "Ano", "ANO")),
                    Placa = GetXmlValue(elem, "placa", "Placa", "PLACA"),
                    Chassi = GetXmlValue(elem, "chassi", "Chassi", "CHASSI"),
                    Cor = GetXmlValue(elem, "cor", "Cor", "COR"),
                    Km = ParseInt(GetXmlValue(elem, "km", "Km", "KM", "quilometragem")),
                    Preco = ParseDecimal(GetXmlValue(elem, "preco", "Preco", "PRECO", "valor", "Valor")),
                    PrecoCompra = ParseDecimal(GetXmlValue(elem, "precoCompra", "PrecoCompra", "custo", "Custo")),
                    Categoria = GetXmlValue(elem, "categoria", "Categoria", "CATEGORIA", "tipo"),
                    DataEntrada = ParseDate(GetXmlValue(elem, "dataEntrada", "DataEntrada", "data")),
                    Status = GetXmlValue(elem, "status", "Status", "STATUS"),
                    Situacao = GetXmlValue(elem, "situacao", "Situacao", "SITUACAO")
                };

                veiculos.Add(veiculo);
            }

            return veiculos;
        }

        private string GetXmlValue(XElement elem, params string[] nomes)
        {
            foreach (var nome in nomes)
            {
                // Tentar como elemento filho
                var child = elem.Element(nome);
                if (child != null)
                    return child.Value.Trim();

                // Tentar como atributo
                var attr = elem.Attribute(nome);
                if (attr != null)
                    return attr.Value.Trim();
            }
            return "";
        }

        private short ParseShort(string valor)
        {
            if (short.TryParse(valor, out short result))
                return result;
            return 0;
        }

        private int ParseInt(string valor)
        {
            // Remove pontos e virgulas usados como separador de milhar
            valor = valor.Replace(".", "").Replace(",", "");
            if (int.TryParse(valor, out int result))
                return result;
            return 0;
        }

        private decimal ParseDecimal(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return 0;

            // Remove simbolos de moeda
            valor = valor.Replace("R$", "").Replace("$", "").Trim();

            // Tenta parse com cultura brasileira primeiro
            if (decimal.TryParse(valor, NumberStyles.Any, new CultureInfo("pt-BR"), out decimal resultBr))
                return resultBr;

            // Tenta parse com cultura americana
            if (decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultUs))
                return resultUs;

            return 0;
        }

        private DateTime? ParseDate(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            // Formatos suportados
            string[] formatos = {
                "dd/MM/yyyy",
                "yyyy-MM-dd",
                "dd-MM-yyyy",
                "dd/MM/yyyy HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss"
            };

            if (DateTime.TryParseExact(valor, formatos, new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime result))
                return result;

            if (DateTime.TryParse(valor, out DateTime result2))
                return result2;

            return null;
        }
    }
}
