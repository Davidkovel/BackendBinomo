using BinomoBackend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BinomoBackend.Persistence.Configuration;

public class PositionHistoryConfiguration : IEntityTypeConfiguration<PositionsHistory>
{
    public void Configure(EntityTypeBuilder<PositionsHistory> builder)
    {
        builder.ToTable("PositionsHistory");
        
        // ⚠️ ВАЖНО: ClosedAt должен быть в Primary Key для партиционирования
        builder.HasKey(ph => new { ph.Id, ph.ClosedAt }); // Composite Primary Key 
        //builder.HasKey(ph => ph.Id);
        

        builder.Property(ph => ph.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(ph => ph.Type)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(ph => ph.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ph => ph.OrderType)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(ph => ph.EntryPrice).HasColumnType("decimal(18,8)");
        builder.Property(ph => ph.ExitPrice).HasColumnType("decimal(18,8)");

        builder.Property(ph => ph.Amount).HasColumnType("decimal(18,8)");
        builder.Property(ph => ph.Margin).HasColumnType("decimal(18,8)");

        builder.Property(ph => ph.ProfitLoss).HasColumnType("decimal(18,8)");
        builder.Property(ph => ph.ROI).HasColumnType("decimal(18,8)");

        builder.Property(ph => ph.LimitPrice).HasColumnType("decimal(18,8)");
        builder.Property(ph => ph.StopLoss).HasColumnType("decimal(18,8)");
        builder.Property(ph => ph.TakeProfit).HasColumnType("decimal(18,8)");

        builder.Property(ph => ph.Leverage)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(ph => ph.CloseReason)
            .HasMaxLength(50);

        builder
            .HasOne(ph => ph.User)
            .WithMany(u => u.PositionHistory)
            .HasForeignKey(ph => ph.UserId);

        // Indexes
        // builder.HasIndex(ph => ph.UserId);
        // builder.HasIndex(ph => ph.Symbol);
        // builder.HasIndex(ph => ph.Status);
        // builder.HasIndex(ph => ph.ClosedAt);
        
        builder.HasIndex(ph => ph.UserId)
            .HasDatabaseName("IX_PositionsHistory_UserId");
            
        builder.HasIndex(ph => new { ph.UserId, ph.ClosedAt })
            .HasDatabaseName("IX_PositionsHistory_UserId_ClosedAt")
            .IsDescending(false, true);
    }
}
