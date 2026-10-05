using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Data;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Services;

public sealed record ItemSolicitado(int PecaId, int Quantidade);

public sealed record DadosEntrega(string Cidade, string Estado, string? VeiculoAplicado);

public interface IPedidoService
{
    Task<ResultadoOperacao<int>> CriarPedidoAsync(string usuarioId, IReadOnlyCollection<ItemSolicitado> itens, DadosEntrega entrega, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoResumoViewModel>> ListarDoUsuarioAsync(string usuarioId, CancellationToken ct = default);
    Task<PedidoDetalhesViewModel?> ObterDoUsuarioAsync(int pedidoId, string usuarioId, CancellationToken ct = default);
    Task<ResultadoOperacao> AvaliarAsync(string usuarioId, AvaliacaoViewModel avaliacao, CancellationToken ct = default);
    Task<ResultadoOperacao> AtualizarStatusAsync(int pedidoId, StatusPedido novoStatus, string? codigoRastreio, CancellationToken ct = default);
    Task<ResultadoOperacao> ConfirmarEntregaAsync(int pedidoId, CancellationToken ct = default);
}

public sealed class PedidoService(ApplicationDbContext db, ILogger<PedidoService> logger) : IPedidoService
{
    private const int MaxItensPorPedido = 20;
    private const int MaxQuantidadePorItem = 99;

    public async Task<ResultadoOperacao<int>> CriarPedidoAsync(
        string usuarioId, IReadOnlyCollection<ItemSolicitado> itens, DadosEntrega entrega, CancellationToken ct = default)
    {
        if (itens.Count == 0 || itens.Count > MaxItensPorPedido)
            return ResultadoOperacao<int>.Falha($"O pedido deve ter de 1 a {MaxItensPorPedido} itens.");

        var agrupados = itens
            .GroupBy(i => i.PecaId)
            .Select(g => new ItemSolicitado(g.Key, g.Sum(x => x.Quantidade)))
            .ToList();

        if (agrupados.Any(i => i.Quantidade < 1 || i.Quantidade > MaxQuantidadePorItem))
            return ResultadoOperacao<int>.Falha($"A quantidade de cada peça deve ser de 1 a {MaxQuantidadePorItem}.");

        var ids = agrupados.Select(i => i.PecaId).ToList();
        var pecas = await db.Pecas.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var pedido = new Pedido
        {
            UsuarioId = usuarioId,
            StatusPedido = StatusPedido.Pendente,
            DataPedido = DateTime.UtcNow,
            CidadeEntrega = entrega.Cidade.Trim(),
            EstadoEntrega = entrega.Estado.Trim().ToUpperInvariant(),
            VeiculoAplicado = string.IsNullOrWhiteSpace(entrega.VeiculoAplicado) ? null : entrega.VeiculoAplicado.Trim()
        };

        foreach (var item in agrupados)
        {
            if (!pecas.TryGetValue(item.PecaId, out var peca))
                return ResultadoOperacao<int>.Falha("Uma das peças selecionadas não existe mais no catálogo.");

            if (peca.Status != StatusPeca.Disponivel || peca.QuantidadeEstoque < item.Quantidade)
                return ResultadoOperacao<int>.Falha($"Estoque insuficiente para \"{peca.Nome}\". Disponível: {peca.QuantidadeEstoque}.");

            // Preços SEMPRE lidos do banco — nunca do cliente.
            pedido.Itens.Add(new ItemPedido
            {
                PecaId = peca.Id,
                Quantidade = item.Quantidade,
                PrecoUnitario = peca.PrecoDiretoFabrica,
                PrecoConcessionariaUnitario = peca.PrecoTabelaConcessionaria
            });
        }

        pedido.ValorTotal = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        pedido.EconomiaGerada = pedido.Itens.Sum(i => CalculadoraEconomia.Valor(i.PrecoConcessionariaUnitario, i.PrecoUnitario) * i.Quantidade);

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Pedido {PedidoId} criado pelo usuário {UsuarioId} — total {Total}", pedido.Id, usuarioId, pedido.ValorTotal);
        return ResultadoOperacao<int>.Ok(pedido.Id, $"Pedido {Formatacao.NumeroPedido(pedido.Id)} registrado. A fábrica vai confirmar a separação do lote.");
    }

    public async Task<IReadOnlyList<PedidoResumoViewModel>> ListarDoUsuarioAsync(string usuarioId, CancellationToken ct = default)
        => await db.Pedidos.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.DataPedido)
            .Select(p => new PedidoResumoViewModel
            {
                Id = p.Id,
                DataPedido = p.DataPedido,
                DataEntrega = p.DataEntrega,
                Status = p.StatusPedido,
                ValorTotal = p.ValorTotal,
                Economia = p.EconomiaGerada,
                QuantidadeItens = p.Itens.Sum(i => i.Quantidade),
                PrimeiraPeca = p.Itens.OrderBy(i => i.Id).Select(i => i.Peca!.Nome).FirstOrDefault() ?? string.Empty,
                CodigoRastreio = p.CodigoRastreio
            })
            .ToListAsync(ct);

    public async Task<PedidoDetalhesViewModel?> ObterDoUsuarioAsync(int pedidoId, string usuarioId, CancellationToken ct = default)
        => await db.Pedidos.AsNoTracking()
            .Where(p => p.Id == pedidoId && p.UsuarioId == usuarioId) // anti-IDOR: só o dono enxerga
            .Select(p => new PedidoDetalhesViewModel
            {
                Id = p.Id,
                DataPedido = p.DataPedido,
                DataEntrega = p.DataEntrega,
                Status = p.StatusPedido,
                CodigoRastreio = p.CodigoRastreio,
                ValorTotal = p.ValorTotal,
                Economia = p.EconomiaGerada,
                VeiculoAplicado = p.VeiculoAplicado,
                Cidade = p.CidadeEntrega,
                Estado = p.EstadoEntrega,
                Nota = p.AvaliacaoCliente,
                Depoimento = p.DepoimentoCliente,
                AutorizaPublicacao = p.AutorizaPublicacao,
                Itens = p.Itens.OrderBy(i => i.Id).Select(i => new ItemPedidoViewModel
                {
                    PecaId = i.PecaId,
                    Nome = i.Peca!.Nome,
                    CodigoOEM = i.Peca.CodigoOEM_PartNumber,
                    Fabricante = i.Peca.Fabricante!.NomeFabrica,
                    NumeroLote = i.Peca.NumeroLoteFabrica,
                    Quantidade = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario,
                    PrecoConcessionariaUnitario = i.PrecoConcessionariaUnitario
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);

    public async Task<ResultadoOperacao> AvaliarAsync(string usuarioId, AvaliacaoViewModel avaliacao, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == avaliacao.PedidoId && p.UsuarioId == usuarioId, ct);
        if (pedido is null) return ResultadoOperacao.Falha("Pedido não encontrado.");
        if (pedido.StatusPedido != StatusPedido.Entregue) return ResultadoOperacao.Falha("Só é possível avaliar pedidos entregues.");
        if (pedido.AvaliacaoCliente is not null) return ResultadoOperacao.Falha("Este pedido já foi avaliado.");

        pedido.AvaliacaoCliente = avaliacao.Nota;
        pedido.DepoimentoCliente = string.IsNullOrWhiteSpace(avaliacao.Depoimento) ? null : avaliacao.Depoimento.Trim();
        pedido.AutorizaPublicacao = avaliacao.AutorizaPublicacao;
        pedido.DataAvaliacao = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ResultadoOperacao.Ok("Avaliação registrada. Obrigado!");
    }

    public async Task<ResultadoOperacao> AtualizarStatusAsync(int pedidoId, StatusPedido novoStatus, string? codigoRastreio, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(novoStatus)) return ResultadoOperacao.Falha("Status inválido.");
        if (novoStatus == StatusPedido.Entregue) return await ConfirmarEntregaAsync(pedidoId, ct);

        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct);
        if (pedido is null) return ResultadoOperacao.Falha("Pedido não encontrado.");

        if (pedido.StatusPedido is StatusPedido.Entregue or StatusPedido.Cancelado)
            return ResultadoOperacao.Falha("Pedidos entregues ou cancelados não podem mudar de status.");

        var transicaoValida = novoStatus == StatusPedido.Cancelado || (int)novoStatus > (int)pedido.StatusPedido;
        if (!transicaoValida)
            return ResultadoOperacao.Falha($"Não é possível voltar de \"{pedido.StatusPedido.NomeExibicao()}\" para \"{novoStatus.NomeExibicao()}\".");

        pedido.StatusPedido = novoStatus;
        if (novoStatus == StatusPedido.Enviado)
        {
            pedido.CodigoRastreio = string.IsNullOrWhiteSpace(codigoRastreio)
                ? pedido.CodigoRastreio ?? GerarCodigoRastreio()
                : codigoRastreio.Trim().ToUpperInvariant();
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
                                           || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ResultadoOperacao.Falha("Este código de rastreio já está em uso em outro pedido.");
        }

        return ResultadoOperacao.Ok($"Pedido {Formatacao.NumeroPedido(pedido.Id)} atualizado para \"{novoStatus.NomeExibicao()}\".");
    }

    /// <summary>
    /// Ação de 1 clique do painel: dá baixa no estoque, soma em TotalVendidos, marca como entregue
    /// e publica o pedido no histórico — tudo num único SaveChanges (atômico) com concorrência otimista.
    /// </summary>
    public async Task<ResultadoOperacao> ConfirmarEntregaAsync(int pedidoId, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos
            .Include(p => p.Itens).ThenInclude(i => i.Peca)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);

        if (pedido is null) return ResultadoOperacao.Falha("Pedido não encontrado.");
        if (pedido.StatusPedido == StatusPedido.Entregue) return ResultadoOperacao.Falha("Este pedido já foi entregue.");
        if (pedido.StatusPedido == StatusPedido.Cancelado) return ResultadoOperacao.Falha("Pedidos cancelados não podem ser entregues.");

        foreach (var item in pedido.Itens)
        {
            var peca = item.Peca!;
            if (peca.QuantidadeEstoque < item.Quantidade)
                return ResultadoOperacao.Falha($"Estoque insuficiente de \"{peca.Nome}\" ({peca.QuantidadeEstoque} un.) para concluir a entrega.");

            peca.QuantidadeEstoque -= item.Quantidade;
            peca.TotalVendidos += item.Quantidade;
            peca.DataAtualizacao = DateTime.UtcNow;
            peca.AtualizarStatusPorEstoque();
        }

        pedido.StatusPedido = StatusPedido.Entregue;
        pedido.DataEntrega = DateTime.UtcNow;
        pedido.CodigoRastreio ??= GerarCodigoRastreio();
        pedido.PublicadoNoHistorico = true;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ResultadoOperacao.Falha("O estoque foi alterado por outra operação ao mesmo tempo. Recarregue e tente novamente.");
        }

        logger.LogInformation("Pedido {PedidoId} confirmado como entregue e publicado no histórico.", pedidoId);
        return ResultadoOperacao.Ok($"Entrega do pedido {Formatacao.NumeroPedido(pedidoId)} confirmada. Estoque atualizado e pedido publicado no histórico.");
    }

    /// <summary>Código interno PA + data + 6 caracteres aleatórios criptograficamente seguros.</summary>
    public static string GerarCodigoRastreio()
        => $"PA{DateTime.UtcNow:yyMMdd}{RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 6)}BR";
}
