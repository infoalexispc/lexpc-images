namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

/// <summary>
/// Quita el marco completamente transparente que rodea al contenido.
/// <para>
/// Los recortes que llegan del diseñador traen aire alrededor del producto: en el máster medido
/// el PC ocupaba 1631×1451 de un lienzo de 1964×1562. Al encajar ese lienzo entero en un slot,
/// una quinta parte de los píxeles disponibles se gasta en alfa cero y el producto se dibuja más
/// pequeño de lo que cabe, que es exactamente la nitidez que se pierde de más.
/// </para>
/// </summary>
public interface IImageTrimmer
{
    /// <summary>
    /// Devuelve la imagen recortada al rectángulo que contiene todo píxel con algo de opacidad.
    /// Si no hay nada que quitar —imagen opaca, o enteramente transparente— devuelve la misma
    /// instancia: recortar más allá del contenido sería inventar un encuadre.
    /// </summary>
    DecodedImage TrimTransparentBorder(DecodedImage image);
}
