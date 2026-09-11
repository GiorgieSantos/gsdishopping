using GSDIShoppingApi.Models;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Data;

public class GSDIShoppingDbContext(DbContextOptions<GSDIShoppingDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<PointsAdjustment> PointsAdjustments => Set<PointsAdjustment>();
    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            // CPF é documento único por pessoa — o índice garante isso no
            // banco mesmo sob cadastros concorrentes (mesma ideia do
            // AccessKey único em PointTransactions).
            entity.HasIndex(u => u.Cpf).IsUnique();
        });

        modelBuilder.Entity<Store>(entity =>
        {
            // CNPJ é o que a checagem de elegibilidade usa para decidir se
            // uma nota pontua — precisa ser único.
            entity.HasIndex(s => s.Cnpj).IsUnique();
        });

        modelBuilder.Entity<PointTransaction>(entity =>
        {
            // Garante no nível do banco que a mesma nota fiscal nunca
            // gera pontos duas vezes, mesmo sob requisições concorrentes.
            entity.HasIndex(t => t.AccessKey).IsUnique();

            entity.HasOne(t => t.User)
                  .WithMany(u => u.PointTransactions)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict (não Cascade): apagar uma loja não pode apagar o
            // histórico de pontos de quem comprou nela.
            entity.HasOne(t => t.Store)
                  .WithMany(s => s.PointTransactions)
                  .HasForeignKey(t => t.StoreId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(t => t.TotalValue).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<Coupon>(entity =>
        {
            // Cascade (diferente de PointTransactions→Store): cupom é só
            // dado de catálogo, não histórico auditável — se a campanha
            // some, os cupons dela deixam de fazer sentido sozinhos.
            entity.HasOne(c => c.Campaign)
                  .WithMany(cm => cm.Coupons)
                  .HasForeignKey(c => c.CampaignId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            // SetNull: apagar uma loja não deveria apagar a promoção,
            // só deixá-la sem loja associada (vira uma promoção "do
            // shopping" em vez de "da loja").
            entity.HasOne(p => p.Store)
                  .WithMany(s => s.Promotions)
                  .HasForeignKey(p => p.StoreId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(a => a.Email).IsUnique();
        });

        modelBuilder.Entity<PointsAdjustment>(entity =>
        {
            // Restrict (igual PointTransactions→Store): preserva o
            // histórico auditável do ajuste mesmo que o usuário ou o
            // admin responsável sejam removidos depois.
            entity.HasOne(p => p.User)
                  .WithMany(u => u.PointsAdjustments)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.AdminUser)
                  .WithMany(a => a.PointsAdjustments)
                  .HasForeignKey(p => p.AdminUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemParameter>(entity =>
        {
            // Cada chave só pode existir uma vez — é como o código acha o
            // parâmetro certo (ver ISystemParametersService).
            entity.HasIndex(p => p.Key).IsUnique();

            // SetNull (não Restrict): diferente de PointsAdjustments, aqui
            // não faz sentido travar a exclusão de um admin por causa de um
            // parâmetro que ele editou uma vez — o parâmetro continua
            // valendo, só perde o rastro de quem foi o último a mexer.
            entity.HasOne(p => p.UpdatedByAdmin)
                  .WithMany()
                  .HasForeignKey(p => p.UpdatedByAdminId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
