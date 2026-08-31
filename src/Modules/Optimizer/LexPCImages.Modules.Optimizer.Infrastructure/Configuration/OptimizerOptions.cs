using System.ComponentModel.DataAnnotations;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Configuration;

/// <summary>
/// Configuración del módulo, enlazada desde la sección <c>Optimizer</c>. Se valida al arrancar
/// (<c>ValidateOnStart</c>): un valor mal escrito detiene el proceso en vez de degradarse en
/// silencio a un valor por defecto, como ocurría al leer la configuración con cadenas sueltas.
/// </summary>
public sealed class OptimizerOptions
{
    public const string SectionName = "Optimizer";

    /// <summary>Trabajos que caben en la cola de procesado antes de rechazar nuevas peticiones.</summary>
    [Range(1, 10_000)]
    public int QueueCapacity { get; set; } = 100;

    /// <summary>
    /// Tiempo que se conserva un trabajo terminado antes de descartarlo. Acota la memoria del
    /// repositorio en memoria, que de lo contrario crece sin límite con los bytes de cada imagen.
    /// </summary>
    [Range(typeof(TimeSpan), "00:01:00", "24:00:00")]
    public TimeSpan JobRetention { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Tope duro de trabajos vivos; al superarlo se descartan primero los más antiguos ya terminados.</summary>
    [Range(1, 100_000)]
    public int MaxTrackedJobs { get; set; } = 500;

    /// <summary>
    /// Calidad visual del WebP con pérdida, de 1 a 100. El salto de peso está en la parte baja de
    /// la escala: sobre las imágenes del catálogo, 75 deja los archivos en torno al 8% de lo que
    /// ocupa la codificación sin pérdida, con una diferencia que no se aprecia en fotografía de
    /// producto. Solo aplica cuando <see cref="WebpLossless"/> es <c>false</c>.
    /// </summary>
    [Range(1, 100)]
    public int WebpQuality { get; set; } = 75;

    /// <summary>
    /// Codifica sin pérdida, píxel a píxel exacto, a cambio de multiplicar el peso por ocho.
    /// Reservado para contenido de bordes duros y color plano —logotipos, capturas, texto—, donde
    /// la codificación con pérdida deja halos y además suele pesar más que la exacta.
    /// <para>
    /// En este modo <see cref="WebpQuality"/> no se usa: el parámetro de calidad de WebP deja de
    /// significar calidad visual y pasa a ser esfuerzo de compresión, así que se fija al máximo.
    /// </para>
    /// </summary>
    public bool WebpLossless { get; set; }
}
