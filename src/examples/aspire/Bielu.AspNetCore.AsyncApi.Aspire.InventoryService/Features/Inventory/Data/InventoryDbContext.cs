// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;

/// <summary>
/// Entity Framework Core DbContext for the Inventory Service.
/// </summary>
public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Inventory items table.
    /// </summary>
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("inventory");
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.QuantityAvailable).HasColumnName("quantity_available").HasDefaultValue(0);
            entity.Property(e => e.QuantityReserved).HasColumnName("quantity_reserved").HasDefaultValue(0);
            entity.Property(e => e.WarehouseLocation).HasColumnName("warehouse_location").HasDefaultValue(string.Empty);

            entity.HasData(
                new InventoryItem { ProductId = "PROD-001", Name = "Widget A", QuantityAvailable = 100, WarehouseLocation = "WH-1" },
                new InventoryItem { ProductId = "PROD-002", Name = "Widget B", QuantityAvailable = 50, WarehouseLocation = "WH-1" },
                new InventoryItem { ProductId = "PROD-003", Name = "Gadget X", QuantityAvailable = 200, WarehouseLocation = "WH-2" }
            );
        });
    }
}
