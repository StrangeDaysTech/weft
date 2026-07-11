namespace Weft.Concurrency;

/// <summary>
/// Opciones de ciclo de vida del <see cref="DocumentBroker"/>: cuándo desalojar documentos inactivos,
/// cuántos mantener activos a la vez (desalojo LRU al superarlo) y cómo persistir antes de desalojar.
/// Inmutable tras construir el broker.
/// </summary>
public sealed class DocumentBrokerOptions
{
    /// <summary>
    /// Tiempo sin actividad tras el cual un documento activo es candidato a desalojo. Un documento se
    /// reabre después desde su estado persistido (vía el <c>loader</c> de <see cref="DocumentBroker.OpenAsync"/>).
    /// </summary>
    public TimeSpan IdleEviction { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Máximo de documentos activos simultáneos. Al superarse, se desaloja el menos recientemente usado
    /// (LRU) para acotar la memoria (SC-006).
    /// </summary>
    public int MaxActiveDocuments { get; init; } = 1024;

    /// <summary>
    /// Hook invocado antes de liberar un documento desalojado, con su estado exportado, para persistirlo.
    /// El desalojo espera a que termine. No se invoca cuando el documento se desaloja por fallo del actor
    /// (estado potencialmente inválido). <c>null</c> = no persistir (los cambios no guardados se pierden
    /// al desalojar).
    /// </summary>
    public Func<string, byte[], CancellationToken, ValueTask>? OnEvicting { get; init; }

    /// <summary>
    /// Cadencia del barrido de inactividad. El broker revisa periódicamente si hay documentos que superan
    /// <see cref="IdleEviction"/>. Por defecto, un tercio de <see cref="IdleEviction"/> (acotado a [1s, 60s]).
    /// </summary>
    public TimeSpan? IdleSweepInterval { get; init; }

    internal TimeSpan ResolveSweepInterval()
    {
        if (IdleSweepInterval is { } explicitInterval)
        {
            return explicitInterval;
        }
        TimeSpan third = IdleEviction / 3;
        if (third < TimeSpan.FromSeconds(1))
        {
            return TimeSpan.FromSeconds(1);
        }
        return third > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : third;
    }
}
