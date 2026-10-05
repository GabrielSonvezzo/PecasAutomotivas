using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Data;
using PecasAutomotivas.Helpers;
using PecasAutomotivas.Models;
using PecasAutomotivas.ViewModels;

namespace PecasAutomotivas.Services;

public interface IHistoricoService
{
    Task<EstatisticasHistoricoViewModel> ObterEstatisticasAsync(CancellationToken ct = default);
    Task<ResultadoPaginado<HistoricoEntregaViewModel>> ListarEntregasAsync(int pagina, int tamanhoPagina = 9, CancellationToken ct = default);
}

/// <summary>
/// Aba pública de prova social. Alimentada exclusivamente por pedidos reais marcados como entregues no painel.
/// Nome e depoimento só aparecem com consentimento (AutorizaPublicacao) e o nome é mascarado.
/// </summary>
public sealed class HistoricoService(ApplicationDbContext db) : IHistoricoService
{
    private IQueryable<Pedido> Entregues => db.Pedidos.AsNoTracking()
        .Where(p => p.StatusPedido == StatusPedido.Entregue && p.PublicadoNoHistorico);

    public async Task<EstatisticasHistoricoViewModel> ObterEstatisticasAsync(CancellationToken ct = default)
    {
        var pedidosEntregues = await Entregues.CountAsync(ct);

        var pecasDespachadas = await db.ItensPedido.AsNoTracking()
            .Where(i => i.Pedido!.StatusPedido == StatusPedido.Entregue && i.Pedido.PublicadoNoHistorico)
            .SumAsync(i => i.Quantidade, ct);

        var economia = await Entregues.SumAsync(p => p.EconomiaGerada, ct);

        var avaliados = Entregues.Where(p => p.AvaliacaoCliente != null);
        var totalAvaliacoes = await avaliados.CountAsync(ct);
        var media = totalAvaliacoes == 0 ? 0 : await avaliados.AverageAsync(p => (double)p.AvaliacaoCliente!.Value, ct);

        var cidades = await Entregues
            .Select(p => new { p.CidadeEntrega, p.EstadoEntrega })
            .Distinct()
            .CountAsync(ct);

        var fabricantes = await db.ItensPedido.AsNoTracking()
            .Where(i => i.Pedido!.StatusPedido == StatusPedido.Entregue && i.Pedido.PublicadoNoHistorico)
            .Select(i => i.Peca!.FabricanteFabricaId)
            .Distinct()
            .CountAsync(ct);

        return new EstatisticasHistoricoViewModel
        {
            PedidosEntregues = pedidosEntregues,
            PecasDespachadas = pecasDespachadas,
            EconomiaGerada = economia,
            MediaAvaliacao = Math.Round(media, 1),
            TotalAvaliacoes = totalAvaliacoes,
            CidadesAtendidas = cidades,
            FabricantesAtendidos = fabricantes
        };
    }

    public async Task<ResultadoPaginado<HistoricoEntregaViewModel>> ListarEntregasAsync(int pagina, int tamanhoPagina = 9, CancellationToken ct = default)
    {
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, 30);
        var total = await Entregues.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)tamanhoPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var brutos = await Entregues
            .OrderByDescending(p => p.DataEntrega)
            .ThenByDescending(p => p.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(p => new
            {
                p.Id,
                p.DataEntrega,
                p.CidadeEntrega,
                p.EstadoEntrega,
                p.VeiculoAplicado,
                p.AvaliacaoCliente,
                p.EconomiaGerada,
                Depoimento = p.AutorizaPublicacao ? p.DepoimentoCliente : null,
                Nome = p.AutorizaPublicacao ? p.Usuario!.NomeCompleto : null,
                Itens = p.Itens.OrderBy(i => i.Id).Select(i => new HistoricoItemViewModel
                {
                    Nome = i.Peca!.Nome,
                    CodigoOEM = i.Peca.CodigoOEM_PartNumber,
                    Fabricante = i.Peca.Fabricante!.NomeFabrica,
                    Categoria = i.Peca.Categoria,
                    Quantidade = i.Quantidade
                }).ToList()
            })
            .ToListAsync(ct);

        var itens = brutos.Select(b => new HistoricoEntregaViewModel
        {
            PedidoId = b.Id,
            DataEntrega = b.DataEntrega ?? DateTime.UtcNow,
            Cidade = b.CidadeEntrega,
            Estado = b.EstadoEntrega,
            Veiculo = b.VeiculoAplicado,
            Nota = b.AvaliacaoCliente,
            Depoimento = b.Depoimento,
            ClientePublico = b.Nome is null ? null : Documento.MascararNomePublico(b.Nome),
            Economia = b.EconomiaGerada,
            Itens = b.Itens
        }).ToList();

        return new ResultadoPaginado<HistoricoEntregaViewModel> { Itens = itens, Pagina = pagina, TamanhoPagina = tamanhoPagina, Total = total };
    }
}
