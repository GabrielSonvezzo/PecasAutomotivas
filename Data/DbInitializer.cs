using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PecasAutomotivas.Configuration;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.Services;

namespace PecasAutomotivas.Data;

/// <summary>
/// Cria/migra o banco, as Roles e o Admin padrão. Com "Seed:DadosDemonstracao = true" (padrão só em Development)
/// popula fabricantes, catálogo e um histórico de pedidos FICTÍCIOS para testar as telas.
/// Em produção deixe false: o histórico público deve conter apenas entregas reais.
/// </summary>
public static class DbInitializer
{
    public static async Task InicializarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;

        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.Todas)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        await CriarAdminAsync(userManager, seed, logger);

        if (seed.DadosDemonstracao && !await db.Fabricantes.AnyAsync(ct))
        {
            await CriarDadosDemonstracaoAsync(db, userManager, logger, ct);
        }
    }

    private static async Task CriarAdminAsync(UserManager<ApplicationUser> userManager, SeedOptions seed, ILogger logger)
    {
        if (await userManager.FindByEmailAsync(seed.AdminEmail) is not null) return;

        if (string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            logger.LogWarning("Seed:AdminPassword não configurado — usuário Admin padrão NÃO foi criado. " +
                              "Defina via variável de ambiente 'Seed__AdminPassword'.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = seed.AdminEmail,
            Email = seed.AdminEmail,
            EmailConfirmed = true,
            NomeCompleto = "Administrador Peças Automotivas",
            CPF_CNPJ = "11222333000181",
            Telefone = "(41) 3000-0000",
            PhoneNumber = "(41) 3000-0000",
            Cidade = "Curitiba",
            Estado = "PR",
            LockoutEnabled = true
        };

        var resultado = await userManager.CreateAsync(admin, seed.AdminPassword);
        if (!resultado.Succeeded)
        {
            logger.LogError("Falha ao criar Admin: {Erros}", string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Usuário Admin padrão criado: {Email}", seed.AdminEmail);
    }

    // -----------------------------------------------------------------------
    // Dados de demonstração (fictícios — códigos e preços ilustrativos)
    // -----------------------------------------------------------------------
    private static async Task CriarDadosDemonstracaoAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger logger, CancellationToken ct)
    {
        var fabricantes = new Dictionary<string, FabricanteFabrica>
        {
            ["Bosch"] = new() { NomeFabrica = "Bosch", PaisOrigem = "Alemanha", CertificacaoISO = "IATF 16949:2016", SeloHomologado = true },
            ["Cofap"] = new() { NomeFabrica = "Cofap", PaisOrigem = "Brasil", CertificacaoISO = "IATF 16949:2016", SeloHomologado = true },
            ["Magneti Marelli"] = new() { NomeFabrica = "Magneti Marelli", PaisOrigem = "Itália", CertificacaoISO = "ISO 9001:2015", SeloHomologado = true },
            ["Valeo"] = new() { NomeFabrica = "Valeo", PaisOrigem = "França", CertificacaoISO = "IATF 16949:2016", SeloHomologado = true },
            ["Mopar"] = new() { NomeFabrica = "Mopar", PaisOrigem = "Estados Unidos", CertificacaoISO = "ISO 9001:2015", SeloHomologado = true },
            ["VW Genuine"] = new() { NomeFabrica = "VW Genuine Parts", PaisOrigem = "Alemanha", CertificacaoISO = "IATF 16949:2016", SeloHomologado = true },
            ["NGK"] = new() { NomeFabrica = "NGK", PaisOrigem = "Japão", CertificacaoISO = "IATF 16949:2016", SeloHomologado = true },
            ["SKF"] = new() { NomeFabrica = "SKF", PaisOrigem = "Suécia", CertificacaoISO = "ISO 14001:2015", SeloHomologado = false }
        };
        db.Fabricantes.AddRange(fabricantes.Values);

        Peca P(string nome, string oem, string sku, CategoriaPeca cat, string montadora, string modelo, int anoI, int anoF,
               string? motor, decimal tabela, decimal fabrica, int estoque, string lote, int garantia, string fab, string desc)
        {
            var peca = new Peca
            {
                Nome = nome, CodigoOEM_PartNumber = oem, SKU_Fabrica = sku, Categoria = cat,
                MontadoraCompativel = montadora, ModeloVeiculoCompativel = modelo, AnoInicial = anoI, AnoFinal = anoF,
                Motorizacao = motor, PrecoTabelaConcessionaria = tabela, PrecoDiretoFabrica = fabrica,
                QuantidadeEstoque = estoque, NumeroLoteFabrica = lote, GarantiaMeses = garantia,
                Fabricante = fabricantes[fab], DescricaoTecnica = desc,
                DataCadastro = DateTime.UtcNow.AddDays(-Random.Shared.Next(10, 180))
            };
            peca.AtualizarStatusPorEstoque();
            return peca;
        }

        var pecas = new List<Peca>
        {
            P("Pastilha de freio dianteira", "0986BB0736", "BSH-PF-0736", CategoriaPeca.Freios, "Volkswagen", "Gol", 2013, 2023, "1.0 / 1.6 MSI",
              289.90m, 189.90m, 48, "BSH-2026-0412", 12, "Bosch",
              "Composto cerâmico de baixa emissão de pó.\nInclui sensor de desgaste e shims antirruído.\nJogo para as duas rodas dianteiras."),
            P("Amortecedor dianteiro Turbogás", "GP32960", "CFP-TG-32960", CategoriaPeca.Suspensao, "Chevrolet", "Onix", 2020, 2025, "1.0 Turbo",
              689.00m, 459.00m, 30, "CFP-2026-0188", 24, "Cofap",
              "Tecnologia bitubular pressurizada a gás.\nValvulação calibrada conforme especificação da montadora.\nVenda unitária."),
            P("Kit de embreagem completo", "VL826822", "VAL-KE-826822", CategoriaPeca.Transmissao, "Fiat", "Argo", 2018, 2025, "1.3 Firefly",
              1290.00m, 879.00m, 14, "VAL-2026-0077", 12, "Valeo",
              "Platô, disco e rolamento de acionamento.\nDisco com amortecimento torsional para arrancadas suaves."),
            P("Bobina de ignição", "BI0045MM", "MM-BI-0045", CategoriaPeca.Eletrica, "Fiat", "Strada", 2021, 2025, "1.3 Firefly",
              459.00m, 312.00m, 22, "MM-2026-0310", 12, "Magneti Marelli",
              "Bobina tipo caneta por cilindro.\nConector selado IP67 original de linha de montagem."),
            P("Filtro de óleo genuíno", "04E115561H", "VWG-FO-561H", CategoriaPeca.Motor, "Volkswagen", "Polo", 2018, 2025, "1.0 TSI",
              119.00m, 69.90m, 120, "VWG-2026-1102", 6, "VW Genuine",
              "Elemento filtrante com válvula antidreno.\nIntervalo de troca conforme plano de manutenção da montadora."),
            P("Vela de ignição iridium", "ILKAR7L11", "NGK-VI-7L11", CategoriaPeca.Motor, "Honda", "Civic", 2017, 2021, "2.0 i-VTEC",
              179.00m, 118.00m, 64, "NGK-2026-0521", 12, "NGK",
              "Eletrodo central de irídio de 0,6 mm.\nFolga pré-calibrada de fábrica. Venda unitária."),
            P("Bomba de combustível elétrica", "0580200317", "BSH-BC-0317", CategoriaPeca.Motor, "Chevrolet", "Onix", 2013, 2019, "1.4 SPE/4",
              849.00m, 579.00m, 11, "BSH-2026-0398", 12, "Bosch",
              "Módulo completo com boia e pré-filtro.\nVazão e pressão conforme homologação da montadora."),
            P("Pivô de suspensão inferior", "CPV2201", "CFP-PV-2201", CategoriaPeca.Suspensao, "Hyundai", "HB20", 2016, 2022, "1.0 / 1.6",
              239.00m, 149.00m, 36, "CFP-2026-0205", 12, "Cofap",
              "Esfera forjada com tratamento térmico.\nCoifa de poliuretano resistente a óleo e ozônio."),
            P("Farol principal esquerdo", "68245339AA", "MOP-FR-9339", CategoriaPeca.Carroceria, "Jeep", "Renegade", 2019, 2024, "1.3 T270",
              2890.00m, 1990.00m, 6, "MOP-2026-0033", 12, "Mopar",
              "Conjunto óptico completo com DRL em LED.\nLente em policarbonato com proteção UV."),
            P("Disco de freio ventilado", "0986479R12", "BSH-DF-9R12", CategoriaPeca.Freios, "Toyota", "Corolla", 2020, 2025, "2.0 Dynamic Force",
              529.00m, 348.00m, 28, "BSH-2026-0455", 12, "Bosch",
              "Ferro fundido com alto teor de carbono.\nPintura anticorrosiva na área não usinada. Par."),
            P("Kit correia dentada com tensor", "04C198119A", "VWG-KC-119A", CategoriaPeca.Motor, "Volkswagen", "Gol", 2017, 2023, "1.0 12V MPI",
              689.00m, 449.00m, 19, "VWG-2026-1150", 12, "VW Genuine",
              "Correia, tensor automático e polia guia.\nTroca recomendada a cada 60.000 km ou conforme manual."),
            P("Módulo de injeção eletrônica", "IAW7GFE", "MM-ECU-7GFE", CategoriaPeca.Eletrica, "Fiat", "Mobi", 2017, 2023, "1.0 Fire",
              3490.00m, 2390.00m, 0, "MM-2026-0290", 12, "Magneti Marelli",
              "Unidade de comando nova, sem programação de imobilizador.\nRequer codificação em concessionária ou oficina habilitada."),
            P("Rolamento de roda dianteira", "VKBA7045", "SKF-RR-7045", CategoriaPeca.Suspensao, "Toyota", "Corolla", 2020, 2025, "2.0 Dynamic Force",
              619.00m, 419.00m, 17, "SKF-2026-0144", 12, "SKF",
              "Cubo integrado com sensor ABS.\nVedação multilábio para longa durabilidade."),
            P("Alternador 120A", "TG12C181", "VAL-AL-C181", CategoriaPeca.Eletrica, "Jeep", "Compass", 2017, 2024, "2.0 Flex",
              2390.00m, 1649.00m, 8, "VAL-2026-0091", 12, "Valeo",
              "Regulador de tensão integrado e polia de roda livre.\nTestado em bancada antes do despacho.")
        };
        db.Pecas.AddRange(pecas);
        await db.SaveChangesAsync(ct);

        // Clientes fictícios (senha aleatória descartada — não há login para eles)
        var clientesBase = new (string Nome, string Cidade, string Uf, string Base9)[]
        {
            ("Carlos Eduardo Mendes", "Curitiba", "PR", "529982247"),
            ("Ana Paula Ribeiro", "São Paulo", "SP", "418573926"),
            ("Rafael Souza Lima", "Belo Horizonte", "MG", "735184092"),
            ("Juliana Costa Prado", "Porto Alegre", "RS", "602915348"),
            ("Marcos Vinícius Oliveira", "Joinville", "SC", "381746250"),
            ("Fernanda Alves Rocha", "Goiânia", "GO", "274603815")
        };

        var clientes = new List<ApplicationUser>();
        for (var i = 0; i < clientesBase.Length; i++)
        {
            var c = clientesBase[i];
            var email = $"cliente{i + 1}@exemplo.com.br";
            var usuario = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, NomeCompleto = c.Nome,
                CPF_CNPJ = c.Base9 + Documento.CalcularDigitosCpf(c.Base9),
                Telefone = $"(41) 9{8800 + i}-{1000 + i * 37}", Cidade = c.Cidade, Estado = c.Uf,
                DataCadastro = DateTime.UtcNow.AddDays(-200 + i * 5)
            };
            var senha = $"Demo#{RandomNumberGenerator.GetHexString(16)}aA1";
            var r = await userManager.CreateAsync(usuario, senha);
            if (!r.Succeeded)
            {
                logger.LogWarning("Cliente de demonstração não criado: {Erros}", string.Join("; ", r.Errors.Select(e => e.Description)));
                continue;
            }
            await userManager.AddToRoleAsync(usuario, Roles.Cliente);
            clientes.Add(usuario);
        }

        if (clientes.Count == 0) return;

        var porOem = pecas.ToDictionary(p => p.CodigoOEM_PartNumber);

        var entregas = new (int Cliente, (string Oem, int Qtd)[] Itens, string Veiculo, int DiasAtras, int? Nota, string? Depoimento)[]
        {
            (0, [("0986BB0736", 1), ("0986479R12", 1)], "VW Gol 1.6 MSI 2019", 6, 5, "Pastilha chegou lacrada com o número de lote. Montei na oficina do meu cunhado e o mecânico conferiu o código na hora."),
            (1, [("GP32960", 2)], "Chevrolet Onix 1.0 Turbo 2022", 11, 5, "Par de amortecedores pela metade do preço que a concessionária me passou. Entrega em 4 dias."),
            (2, [("VL826822", 1)], "Fiat Argo 1.3 2020", 17, 4, "Kit completo e original. Só demorou um dia a mais que o previsto."),
            (3, [("68245339AA", 1)], "Jeep Renegade 2021", 23, 5, "Farol idêntico ao de fábrica, encaixe perfeito. Embalagem muito bem protegida."),
            (4, [("04E115561H", 3), ("ILKAR7L11", 4)], "VW Polo 1.0 TSI 2021", 30, 5, "Comprei para três revisões. Nota fiscal e certificado do lote vieram junto."),
            (5, [("BI0045MM", 4)], "Fiat Strada 1.3 2023", 38, 4, null),
            (0, [("04C198119A", 1)], "VW Gol 1.0 2018", 45, 5, "Segunda compra aqui. Kit da correia com tensor original, preço muito abaixo da tabela."),
            (1, [("VKBA7045", 2)], "Toyota Corolla 2.0 2022", 52, 5, "Rolamento com sensor ABS funcionando de primeira."),
            (2, [("0580200317", 1)], "Chevrolet Onix 1.4 2016", 60, 4, "Bomba original resolveu a falha de partida. Recomendo."),
            (3, [("CPV2201", 2)], "Hyundai HB20 1.6 2019", 68, null, null),
            (4, [("TG12C181", 1)], "Jeep Compass 2.0 Flex 2019", 75, 5, "Alternador chegou com laudo de bancada. Atendimento respondeu todas as dúvidas técnicas."),
            (5, [("0986BB0736", 2)], "VW Gol 1.0 2020", 83, 5, "Frota da empresa: comprei dois jogos e a economia foi real.")
        };

        var pedidos = new List<Pedido>();
        foreach (var e in entregas)
        {
            var cliente = clientes[e.Cliente % clientes.Count];
            var entrega = DateTime.UtcNow.AddDays(-e.DiasAtras);
            var pedido = MontarPedido(cliente, e.Itens.Select(i => (porOem[i.Oem], i.Qtd)), e.Veiculo, entrega.AddDays(-5));
            pedido.StatusPedido = StatusPedido.Entregue;
            pedido.DataEntrega = entrega;
            pedido.CodigoRastreio = PedidoService.GerarCodigoRastreio();
            pedido.PublicadoNoHistorico = true;
            pedido.AvaliacaoCliente = e.Nota;
            pedido.DepoimentoCliente = e.Depoimento;
            pedido.DataAvaliacao = e.Nota is null ? null : entrega.AddDays(1);
            pedido.AutorizaPublicacao = e.Depoimento is not null;
            pedidos.Add(pedido);

            foreach (var item in pedido.Itens) item.Peca!.TotalVendidos += item.Quantidade;
        }

        // Pedidos em aberto para o painel administrativo
        var abertos = new (int Cliente, string Oem, int Qtd, string Veiculo, StatusPedido Status, int DiasAtras)[]
        {
            (0, "GP32960", 2, "Chevrolet Onix 1.0 Turbo 2021", StatusPedido.Pendente, 0),
            (2, "0986479R12", 1, "Toyota Corolla 2.0 2023", StatusPedido.SeparacaoFabrica, 1),
            (4, "BI0045MM", 2, "Fiat Strada 1.3 2022", StatusPedido.Enviado, 3)
        };
        foreach (var a in abertos)
        {
            var pedido = MontarPedido(clientes[a.Cliente % clientes.Count], [(porOem[a.Oem], a.Qtd)], a.Veiculo, DateTime.UtcNow.AddDays(-a.DiasAtras));
            pedido.StatusPedido = a.Status;
            if (a.Status == StatusPedido.Enviado) pedido.CodigoRastreio = PedidoService.GerarCodigoRastreio();
            pedidos.Add(pedido);
        }

        db.Pedidos.AddRange(pedidos);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dados de demonstração criados: {Pecas} peças, {Pedidos} pedidos.", pecas.Count, pedidos.Count);
    }

    private static Pedido MontarPedido(ApplicationUser cliente, IEnumerable<(Peca Peca, int Qtd)> itens, string veiculo, DateTime data)
    {
        var pedido = new Pedido
        {
            UsuarioId = cliente.Id,
            CidadeEntrega = cliente.Cidade,
            EstadoEntrega = cliente.Estado,
            VeiculoAplicado = veiculo,
            DataPedido = data
        };

        foreach (var (peca, qtd) in itens)
        {
            pedido.Itens.Add(new ItemPedido
            {
                Peca = peca,
                Quantidade = qtd,
                PrecoUnitario = peca.PrecoDiretoFabrica,
                PrecoConcessionariaUnitario = peca.PrecoTabelaConcessionaria
            });
        }

        pedido.ValorTotal = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        pedido.EconomiaGerada = pedido.Itens.Sum(i => CalculadoraEconomia.Valor(i.PrecoConcessionariaUnitario, i.PrecoUnitario) * i.Quantidade);
        return pedido;
    }
}
