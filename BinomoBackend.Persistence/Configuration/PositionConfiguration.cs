using BinomoBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BinomoBackend.Persistence.Configuration;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Symbol)
            .IsRequired()
            .HasMaxLength(20);
            
        builder.Property(p => p.EntryPrice)
            .IsRequired()
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.Amount)
            .IsRequired()
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.Margin)
            .IsRequired()
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.ProfitLoss)
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.LimitPrice)
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.StopLoss)
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.TakeProfit)
            .HasColumnType("decimal(18,8)");
            
        builder.Property(p => p.ExitPrice)
            .HasColumnType("decimal(18,8)");
        
        builder.Property(p => p.LiquidationPrice)
            .HasPrecision(18, 8); // 18 цифр всего, 8 после запятой
            
        builder.Property(p => p.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);
            
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
            
        builder.Property(p => p.OrderType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Positions) // У User есть коллекция Positions
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade); // При удалении User удаляются его позиции

        // indexes
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.Symbol);
        builder.HasIndex(p => p.CreatedAt);
        
        // Composite index
        builder.HasIndex(p => new { p.UserId, p.Status });
    }
}