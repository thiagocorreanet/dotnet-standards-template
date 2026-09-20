namespace Module.Events.Domain;

/// <summary>
/// Ciclo de vida do evento. Transições válidas:
/// Rascunho → Publicado; Publicado → EmAndamento; Publicado|EmAndamento → Encerrado; Rascunho|Publicado|EmAndamento → Cancelado.
/// </summary>
public enum EventStatus
{
    Draft,
    Published,
    InProgress,
    Closed,
    Canceled,
}
