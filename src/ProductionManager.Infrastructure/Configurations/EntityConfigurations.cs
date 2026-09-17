using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductionManager.Domain.Entities;

namespace ProductionManager.Infrastructure.Configurations;

public sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.PN).IsRequired();
        b.Property(x => x.Nome).IsRequired();
        b.HasIndex(x => x.PN).IsUnique();
    }
}
public sealed class MaquinaConfiguration : IEntityTypeConfiguration<Maquina>
{
    public void Configure(EntityTypeBuilder<Maquina> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).IsRequired();
        b.Property(x => x.Nome).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
    }
}
public sealed class OrdemConfiguration : IEntityTypeConfiguration<OrdemProducao>
{
    public void Configure(EntityTypeBuilder<OrdemProducao> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.OF).IsRequired();
        b.HasIndex(x => x.OF).IsUnique();
        b.Property(x => x.Versao).IsConcurrencyToken();
        // O índice parcial fecha a corrida entre duas OFs distintas na mesma máquina.
        b.HasIndex(x => x.MaquinaId).IsUnique().HasFilter("\"Status\" = 1").HasDatabaseName("UX_Maquina_EmProducao");
        b.HasOne(x => x.Produto).WithMany().HasForeignKey(x => x.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Maquina).WithMany().HasForeignKey(x => x.MaquinaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.MetasHora).WithOne().HasForeignKey(x => x.OrdemProducaoId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.MetasHora).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Ignore(x => x.QuantidadeProduzida);
        b.Ignore(x => x.RefugoTotal);
        b.Ignore(x => x.MetaApontada);
        b.Ignore(x => x.MetasPendentes);
        b.Ignore(x => x.Qualidade);
        b.Ignore(x => x.Eficiencia);
        b.ToTable("Ordens", t =>
        {
            t.HasCheckConstraint("CK_Ordem_Quantidade", "\"QuantidadePlanejada\" > 0");
            t.HasCheckConstraint("CK_Ordem_Status", "\"Status\" IN (0, 1, 2, 3)");
        });
    }
}
public sealed class MetaConfiguration : IEntityTypeConfiguration<MetaHora>
{
    public void Configure(EntityTypeBuilder<MetaHora> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Apontamento).WithOne().HasForeignKey<ApontamentoMetaHora>(x => x.MetaHoraId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.Apontada);
        b.Ignore(x => x.Qualidade);
        b.Ignore(x => x.Eficiencia);
        b.ToTable("MetasHora", t => t.HasCheckConstraint("CK_Meta_Planejada", "\"MetaPlanejada\" > 0"));
    }
}
public sealed class ApontamentoConfiguration : IEntityTypeConfiguration<ApontamentoMetaHora>
{
    public void Configure(EntityTypeBuilder<ApontamentoMetaHora> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.MetaHoraId).IsUnique();
        b.ToTable("Apontamentos", t =>
        {
            t.HasCheckConstraint("CK_Apontamento_Boas", "\"QuantidadeBoa\" >= 0");
            t.HasCheckConstraint("CK_Apontamento_Refugo", "\"Refugo\" >= 0");
        });
    }
}
public sealed class ProgramacaoConfiguration : IEntityTypeConfiguration<ProgramacaoSemanal>
{
    public void Configure(EntityTypeBuilder<ProgramacaoSemanal> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Versao).IsConcurrencyToken();
        b.HasIndex(x => new { x.MaquinaId, x.InicioSemana }).IsUnique();
        b.HasOne(x => x.Maquina).WithMany().HasForeignKey(x => x.MaquinaId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.ProgramacaoSemanalId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
public sealed class ItemConfiguration : IEntityTypeConfiguration<ItemProgramacao>
{
    public void Configure(EntityTypeBuilder<ItemProgramacao> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne(x => x.OrdemProducao).WithMany().HasForeignKey(x => x.OrdemProducaoId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ProgramacaoSemanalId, x.OrdemProducaoId }).IsUnique();
        b.ToTable("ItensProgramacao", t => t.HasCheckConstraint("CK_Item_Sequencia", "\"Sequencia\" > 0"));
    }
}
