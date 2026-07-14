using Microsoft.EntityFrameworkCore;

namespace Weft.Server.Persistence.EFCore;

/// <summary>Clase de record persistido: distingue el snapshot consolidado de un update incremental.</summary>
internal enum RecordKind : byte
{
    /// <summary>Snapshot consolidado del documento (a lo sumo uno por doc, es la base del framing).</summary>
    Snapshot = 0,

    /// <summary>Update incremental acumulado sobre el snapshot, en orden de <see cref="DocumentRecord.Seq"/>.</summary>
    Update = 1,
}

/// <summary>
/// Fila de estado durable de un documento. El <see cref="Payload"/> es un blob <b>opaco</b> (P-IV): EF Core
/// nunca interpreta bytes de yrs. El orden de reconstrucción lo fija <see cref="Seq"/> (monotónico por doc).
/// </summary>
internal sealed class DocumentRecord
{
    /// <summary>Clave primaria sustituta generada por la base de datos.</summary>
    public long Id { get; set; }

    /// <summary>Identificador opaco del documento (puede contener cualquier carácter).</summary>
    public string DocId { get; set; } = default!;

    /// <summary>Índice de orden dentro del documento; el snapshot es la base y los updates crecen desde ahí.</summary>
    public long Seq { get; set; }

    /// <summary>Snapshot o update.</summary>
    public RecordKind Kind { get; set; }

    /// <summary>Bytes opacos del record (snapshot consolidado o un update incremental).</summary>
    public byte[] Payload { get; set; } = default!;
}

/// <summary>
/// <see cref="DbContext"/> del adaptador <see cref="EFCoreDocumentStore"/>. Mapea una única tabla de records
/// (<c>WeftDocumentRecords</c>) indexada por <c>(DocId, Seq)</c>. El provider (SQLite, PostgreSQL, …) lo elige
/// el consumidor vía <see cref="DbContextOptions"/>; este contexto es provider-agnóstico.
/// </summary>
public sealed class WeftDocumentStoreContext : DbContext
{
    /// <summary>Crea el contexto con las opciones (provider + cadena de conexión) del consumidor.</summary>
    public WeftDocumentStoreContext(DbContextOptions<WeftDocumentStoreContext> options)
        : base(options)
    {
    }

    internal DbSet<DocumentRecord> Records => Set<DocumentRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var record = modelBuilder.Entity<DocumentRecord>();
        record.ToTable("WeftDocumentRecords");
        record.HasKey(r => r.Id);
        record.Property(r => r.DocId).IsRequired();
        record.Property(r => r.Payload).IsRequired();
        // El acceso siempre es "todos los records de un doc, en orden de Seq": índice compuesto que lo cubre.
        record.HasIndex(r => new { r.DocId, r.Seq });
    }
}
