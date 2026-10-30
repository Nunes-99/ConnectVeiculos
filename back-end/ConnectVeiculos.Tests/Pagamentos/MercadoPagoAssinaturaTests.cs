using System.Security.Cryptography;
using System.Text;
using ConnectVeiculos.Core.Entities.Tenants;
using ConnectVeiculos.Core.Interfaces.Services;
using ConnectVeiculos.Infrastructure.Services.Pagamentos;
using FluentAssertions;
using Xunit;
using static ConnectVeiculos.Infrastructure.Services.Pagamentos.MercadoPagoAssinaturaService;

namespace ConnectVeiculos.Tests.Pagamentos
{
    public class MercadoPagoAssinaturaTests
    {
        private const string Segredo = "segredo-do-painel";

        private static string Assinar(string manifesto)
            => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Segredo), Encoding.UTF8.GetBytes(manifesto))).ToLowerInvariant();

        [Fact]
        public void Aviso_com_assinatura_certa_e_autentico()
        {
            var v1 = Assinar("id:2c938084726fca480172750000000000;request-id:abc-123;ts:1704908010;");

            AvisoAutentico($"ts=1704908010,v1={v1}", "abc-123", "2C938084726FCA480172750000000000", Segredo)
                .Should().BeTrue("o Mercado Pago assina o id alfanumerico em minusculas");
        }

        [Theory]
        [InlineData("ts=1704908010,v1=00ff", "abc-123", "123")]   // hash errado
        [InlineData(null, "abc-123", "123")]                      // sem cabecalho
        [InlineData("v1=abc", "abc-123", "123")]                  // sem ts
        [InlineData("ts=1,v1=nao-e-hex", "abc-123", "123")]
        public void Aviso_sem_assinatura_valida_e_recusado(string? xSignature, string? requestId, string id)
        {
            AvisoAutentico(xSignature, requestId, id, Segredo).Should().BeFalse();
        }

        [Fact]
        public void Sem_segredo_configurado_nenhum_aviso_passa()
        {
            var v1 = Assinar("id:123;request-id:abc;ts:1;");
            AvisoAutentico($"ts=1,v1={v1}", "abc", "123", "").Should().BeFalse();
        }

        [Fact]
        public void Referencia_vai_e_volta()
        {
            LerReferencia(ReferenciaExterna(7, 3), out var tenant, out var plano).Should().BeTrue();
            tenant.Should().Be(7);
            plano.Should().Be(3);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("pedido-123")]
        [InlineData("cv:abc:3")]
        [InlineData("cv:-1:3")]
        public void Referencia_de_outra_venda_e_ignorada(string? referencia)
        {
            LerReferencia(referencia, out _, out _).Should().BeFalse();
        }

        [Fact]
        public void Pix_conta_30_dias_e_renovar_antes_soma()
        {
            var t = new Tenant("loja", "Loja");
            var hoje = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

            t.RegistrarPagamentoPix("p1", 3, 30, hoje).Should().BeTrue();
            t.TenPlaId.Should().Be(3);
            t.TenPlanoPagoAte.Should().Be(hoje.AddDays(30));

            // Renovou 5 dias antes de vencer: nao perde os 5 dias.
            t.RegistrarPagamentoPix("p2", 3, 30, hoje.AddDays(25)).Should().BeTrue();
            t.TenPlanoPagoAte.Should().Be(hoje.AddDays(60));
        }

        [Fact]
        public void Mesmo_aviso_de_Pix_duas_vezes_conta_uma()
        {
            var t = new Tenant("loja", "Loja");
            var hoje = DateTime.UtcNow;
            t.RegistrarPagamentoPix("p1", 3, 30, hoje);

            t.RegistrarPagamentoPix("p1", 3, 30, hoje).Should().BeFalse();
            t.TenPlanoPagoAte.Should().Be(hoje.AddDays(30));
        }

        [Fact]
        public void Trocar_de_plano_no_Pix_comeca_o_periodo_do_zero()
        {
            var t = new Tenant("loja", "Loja");
            var hoje = DateTime.UtcNow;
            t.RegistrarPagamentoPix("p1", 2, 30, hoje);

            t.RegistrarPagamentoPix("p2", 3, 30, hoje.AddDays(10));
            t.TenPlaId.Should().Be(3);
            t.TenPlanoPagoAte.Should().Be(hoje.AddDays(40));
        }

        [Fact]
        public void Pix_vencido_sem_cartao_volta_ao_gratuito()
        {
            var t = new Tenant("loja", "Loja");
            var hoje = DateTime.UtcNow;
            t.RegistrarPagamentoPix("p1", 3, 30, hoje.AddDays(-31));

            PeriodoPixVenceu(t, hoje).Should().BeTrue();

            t.RegistrarAssinatura("a1", StatusAssinatura.Ativa, 3);
            PeriodoPixVenceu(t, hoje).Should().BeFalse("o cartao ativo segura o plano");
        }

        [Fact]
        public void Pix_dentro_do_periodo_nao_vence()
        {
            var t = new Tenant("loja", "Loja");
            t.RegistrarPagamentoPix("p1", 3, 30, DateTime.UtcNow);
            PeriodoPixVenceu(t, DateTime.UtcNow).Should().BeFalse();
        }

        [Fact]
        public void Referencia_do_Pix_e_separada_da_assinatura()
        {
            LerReferenciaPix(ReferenciaPix(7, 3), out var tenant, out var plano).Should().BeTrue();
            (tenant, plano).Should().Be((7, 3));
            LerReferencia(ReferenciaPix(7, 3), out _, out _).Should().BeFalse();
            LerReferenciaPix(ReferenciaExterna(7, 3), out _, out _).Should().BeFalse();
        }

        [Fact]
        public void Cartao_pode_ser_devolvido_ate_7_dias_da_confirmacao()
        {
            var t = new Tenant("loja", "Loja");
            t.RegistrarAssinatura("a1", StatusAssinatura.Ativa, 3);
            var inicio = t.TenAssinaturaInicio!.Value;

            PrazoReembolso(t, inicio.AddDays(5)).Should().Be(inicio.AddDays(7));
            PrazoReembolso(t, inicio.AddDays(8)).Should().BeNull();
        }

        [Fact]
        public void Aviso_repetido_da_mesma_assinatura_nao_reinicia_os_7_dias()
        {
            var t = new Tenant("loja", "Loja");
            t.RegistrarAssinatura("a1", StatusAssinatura.Ativa, 3);
            var inicio = t.TenAssinaturaInicio;

            t.RegistrarAssinatura("a1", StatusAssinatura.Ativa, 3);
            t.TenAssinaturaInicio.Should().Be(inicio);
        }

        [Fact]
        public void Pix_pode_ser_devolvido_ate_7_dias_do_pagamento()
        {
            var t = new Tenant("loja", "Loja");
            var pago = DateTime.UtcNow.AddDays(-3);
            t.RegistrarPagamentoPix("p1", 3, 30, pago);

            PrazoReembolso(t, DateTime.UtcNow).Should().Be(pago.AddDays(7));
            PrazoReembolso(t, pago.AddDays(10)).Should().BeNull();
        }

        [Fact]
        public void Devolver_o_unico_Pix_volta_ao_gratuito()
        {
            var t = new Tenant("loja", "Loja");
            var agora = DateTime.UtcNow;
            t.RegistrarPagamentoPix("p1", 3, 30, agora.AddDays(-2));

            t.DesfazerPagamentoPix(30, agora).Should().BeTrue();
            t.TenPlanoPagoAte.Should().BeNull();
        }

        [Fact]
        public void Devolver_a_renovacao_mantem_o_periodo_anterior()
        {
            var t = new Tenant("loja", "Loja");
            var agora = DateTime.UtcNow;
            t.RegistrarPagamentoPix("p1", 3, 30, agora.AddDays(-20));   // vale ate +10
            t.RegistrarPagamentoPix("p2", 3, 30, agora.AddDays(-1));    // renovou: ate +40

            t.DesfazerPagamentoPix(30, agora).Should().BeFalse();
            t.TenPlanoPagoAte.Should().Be(agora.AddDays(-20).AddDays(30));
        }

        [Fact]
        public void Cartao_cancelado_depois_dos_7_dias_mantem_o_plano_ate_o_fim_do_periodo()
        {
            var t = new Tenant("loja", "Loja");
            t.RegistrarAssinatura("a1", StatusAssinatura.Ativa, 3);
            var fim = DateTime.UtcNow.AddDays(12);
            t.RegistrarAssinatura("a1", StatusAssinatura.Cancelada, 3);
            t.ManterPlanoAte(fim);

            PeriodoPixVenceu(t, DateTime.UtcNow).Should().BeFalse();
            PeriodoPixVenceu(t, fim.AddMinutes(1)).Should().BeTrue();
        }

        private static Tenant Loja(string? assinaturaId = null, string? status = null)
        {
            var t = new Tenant("loja", "Loja");
            if (assinaturaId != null) t.RegistrarAssinatura(assinaturaId, status!, 3);
            return t;
        }

        [Fact]
        public void Assinatura_autorizada_ativa_o_plano()
        {
            Decidir(Loja(), "a1", StatusAssinatura.Ativa, 99.99m, 99.99m).Should().Be(Decisao.Ativar);
        }

        [Fact]
        public void Aviso_repetido_da_assinatura_ja_ativa_nao_faz_nada()
        {
            Decidir(Loja("a1", StatusAssinatura.Ativa), "a1", StatusAssinatura.Ativa, 99.99m, 99.99m).Should().Be(Decisao.Nada);
        }

        [Fact]
        public void Troca_de_plano_ativa_a_nova_assinatura()
        {
            Decidir(Loja("antiga", StatusAssinatura.Ativa), "nova", StatusAssinatura.Ativa, 199.99m, 199.99m).Should().Be(Decisao.Ativar);
        }

        [Fact]
        public void Valor_abaixo_do_preco_do_plano_nao_ativa()
        {
            Decidir(Loja(), "a1", StatusAssinatura.Ativa, 1m, 99.99m).Should().Be(Decisao.ValorAbaixoDoPlano);
        }

        [Theory]
        [InlineData(StatusAssinatura.Cancelada)]
        [InlineData(StatusAssinatura.Pausada)]
        public void Assinatura_atual_cancelada_ou_pausada_volta_ao_gratuito(string status)
        {
            Decidir(Loja("a1", StatusAssinatura.Ativa), "a1", status, 99.99m, 99.99m).Should().Be(Decisao.VoltarAoGratuito);
        }

        [Fact]
        public void Cancelar_a_assinatura_antiga_da_troca_nao_derruba_o_plano_novo()
        {
            Decidir(Loja("nova", StatusAssinatura.Ativa), "antiga", StatusAssinatura.Cancelada, 99.99m, 99.99m).Should().Be(Decisao.Nada);
        }

        [Fact]
        public void Assinatura_pendente_nao_muda_nada()
        {
            Decidir(Loja(), "a1", StatusAssinatura.Pendente, 99.99m, 99.99m).Should().Be(Decisao.Nada);
        }
    }
}
