using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace USER.Model.Constrains_on_table
{
    public class RefreshTokenConstraints : IEntityTypeConfiguration<RefreshTable>
    {
        public void Configure(EntityTypeBuilder<RefreshTable> builder)
        {
           builder.HasKey(x=>x.Id);
           builder.Property(x=>x.refreshToken).HasMaxLength(100);
           builder.Property(x=>x.name).HasMaxLength(40);
        }
    }
}