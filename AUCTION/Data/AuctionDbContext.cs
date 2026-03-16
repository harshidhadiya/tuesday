using AUCTION.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AUCTION.Data;

public class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions<AuctionDbContext> options) : base(options) { }

    public DbSet<Auction>   Auctions   => Set<Auction>();
    public DbSet<Bid>       Bids       => Set<Bid>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Auction>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.StartingPrice).HasPrecision(18, 2);
            e.Property(x => x.ReservePrice).HasPrecision(18, 2);
            e.Property(x => x.MinBidIncrement).HasPrecision(18, 2);
            e.Property(x => x.FinalPrice).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.StartDate);
            e.HasIndex(x => x.EndDate);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ProductId);
            e.HasIndex(x => x.CreatedByUserId);
        });

        mb.Entity<Bid>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.AuctionId);
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Auction)
             .WithMany(x => x.Bids)
             .HasForeignKey(x => x.AuctionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Watchlist>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityColumn();
            e.HasIndex(x => new { x.UserId, x.AuctionId }).IsUnique();
            e.HasOne(x => x.Auction)
             .WithMany(x => x.Watchlists)
             .HasForeignKey(x => x.AuctionId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
