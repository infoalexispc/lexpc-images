namespace LexPCImages.Modules.Optimizer.Infrastructure.Configuration;

/// <summary>
/// Filtro de remuestreo con el que se reducen las imágenes. Solo interviene cuando el destino es
/// más pequeño que el origen: al ampliar siempre se usa Lanczos3, porque promediar áreas de menos
/// de un píxel devuelve bloques.
/// </summary>
public enum DownscaleFilter
{
    /// <summary>
    /// Promedio de área. Conserva la textura fina —rejillas, mallados, tramas— porque no la filtra
    /// del todo: lo que queda es un patrón de interferencia (muaré) que el ojo lee como detalle.
    /// Es el aspecto de las referencias hechas a mano con las que se comparó el catálogo.
    /// </summary>
    Box,

    /// <summary>
    /// Lanczos de tres lóbulos. Es el filtro formalmente correcto: elimina toda frecuencia que no
    /// cabe en el destino, así que una rejilla más fina que dos píxeles de salida desaparece y el
    /// panel queda liso. Más fiel al máster, pero visiblemente más plano en tramas finas.
    /// </summary>
    Lanczos3,
}
