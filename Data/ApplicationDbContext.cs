using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PecasAutomotivas.Models;

namespace PecasAutomotivas.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<FabricanteFabrica> Fabricantes => Set<FabricanteFabrica>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.CPF_CNPJ).IsUnique();
            e.Property(u => u.Estado).IsFixedLength();
        });

        builder.Entity<FabricanteFabrica>(e =>
        {
            e.ToTable("Fabricantes");
            e.HasIndex(f => f.NomeFabrica).IsUnique();
        });

        builder.Entity<Peca>(e =>
        {
            e.ToTable("Pecas");
            e.HasIndex(p => p.CodigoOEM_PartNumber).IsUnique();
            e.HasIndex(p => p.SKU_Fabrica);
            e.HasIndex(p => new { p.MontadoraCompativel, p.ModeloVeiculoCompativel });
            e.HasIndex(p => p.Categoria);

            e.Property(p => p.PrecoTabelaConcessionaria).HasPrecision(18, 2);
            e.Property(p => p.PrecoDiretoFabrica).HasPrecision(18, 2);
            e.Property(p => p.Categoria).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

            // Concorrência otimista: dois admins confirmando entregas ao mesmo tempo não corrompem o estoque.
            e.Property(p => p.QuantidadeEstoque).IsConcurrencyToken();

            e.HasOne(p => p.Fabricante)
                .WithMany(f => f.Pecas)
                .HasForeignKey(p => p.FabricanteFabricaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Pedido>(e =>
        {
            e.ToTable("Pedidos");
            e.HasIndex(p => p.CodigoRastreio).IsUnique();
            e.HasIndex(p => new { p.StatusPedido, p.DataEntrega });
            e.HasIndex(p => p.UsuarioId);

            e.Property(p => p.ValorTotal).HasPrecision(18, 2);
            e.Property(p => p.EconomiaGerada).HasPrecision(18, 2);
            e.Property(p => p.StatusPedido).HasConversion<string>().HasMaxLength(30);
            e.Property(p => p.EstadoEntrega).IsFixedLength();

            e.HasOne(p => p.Usuario)
                .WithMany(u => u.Pedidos)
                .HasForeignKey(p => p.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Itens)
                .WithOne(i => i.Pedido)
                .HasForeignKey(i => i.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ItemPedido>(e =>
        {
            e.ToTable("ItensPedido");
            e.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
            e.Property(i => i.PrecoConcessionariaUnitario).HasPrecision(18, 2);

            e.HasOne(i => i.Peca)
                .WithMany(p => p.ItensPedido)
                .HasForeignKey(i => i.PecaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // SQLite não tem tipo decimal nativo (não ordena nem agrega). Em desenvolvimento com SQLite,
        // os decimais são persistidos como REAL. No SQL Server permanecem decimal(18,2).
        if (Database.IsSqlite())
        {
            foreach (var entidade in builder.Model.GetEntityTypes())
            {
                foreach (var propriedade in entidade.GetProperties()
                             .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                {
                    propriedade.SetProviderClrType(typeof(double));
                }
            }
        }
    }
}
