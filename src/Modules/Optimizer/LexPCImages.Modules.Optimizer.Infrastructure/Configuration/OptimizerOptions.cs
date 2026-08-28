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

    /// <summary>
    /// Ruta al modelo ONNX de segmentación. Si es relativa, se resuelve contra el directorio
    /// de la aplicación (<see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ModelPath { get; set; } = Path.Combine("models", "rmbg-1.4-fp16.onnx");

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

    /// <summary>Ruta absoluta del modelo, resuelta contra el directorio de la aplicación si hace falta.</summary>
    public string ResolveModelPath() => Path.IsPathRooted(ModelPath)
        ? ModelPath
        : Path.Combine(AppContext.BaseDirectory, ModelPath);
}
