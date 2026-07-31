using BlogGraphQlApp.Services.Interfaces;
using SkiaSharp;

namespace BlogGraphQlApp.Services.Implementations
{
    public class AvatarGeneratorService : IAvatarGeneratorService
    {
        public byte[] GenerateAvatar(string initials, int size = 200, bool useGradient = true, bool addBorderRing = true, AvatarShape shape = AvatarShape.Circle, int borderThickness = 6, string fontFamily = "Arial")
        {
            var hash = initials.GetHashCode();

            var rand = new Random(hash);

            var color1 = new SKColor(
                (byte)rand.Next(100,256),
                (byte)rand.Next(100,256),
                (byte)rand.Next(100, 256));

            var color2 = new SKColor(
                (byte)rand.Next(100, 256),
                (byte)rand.Next(100, 256),
                (byte)rand.Next(100, 256));

            using (var bitmap = new SKBitmap(size, size))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);

                    using (var paint = new SKPaint
                    {
                        IsAntialias = true
                    })
                    {
                        if(useGradient)
                        {
                            paint.Shader = SKShader.CreateLinearGradient(
                                new SKPoint(0, 0),
                                new SKPoint(size, size),
                                new SKColor[] { color1, color2 },
                                null,
                                SKShaderTileMode.Clamp);
                        }
                        else
                        {
                            paint.Color = color1;
                        }

                        switch (shape)
                        {
                            case AvatarShape.Circle:
                                canvas.DrawCircle(size / 2, size / 2, size / 2, paint);
                                break;
                            case AvatarShape.RoundedSquare:
                                var rect = new SKRect(0, 0, size, size);
                                var radius = size * 0.2f;
                                canvas.DrawRoundRect(rect, radius, radius, paint);
                                break;
                            case AvatarShape.Square:
                                canvas.DrawRect(0, 0, size, size, paint);
                                break;
                            default:
                                break;
                        }

                        if (addBorderRing)
                        {
                            using (var borderPaint = new SKPaint
                            {
                                IsAntialias = true,
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = borderThickness,
                                Color = SKColors.White
                            })
                            {
                                switch (shape)
                                {
                                    case AvatarShape.Circle:
                                        canvas.DrawCircle(size / 2, size / 2, (size - borderThickness) / 2, borderPaint);
                                        break;
                                    case AvatarShape.RoundedSquare:
                                        var rect = new SKRect(borderThickness / 2, borderThickness / 2, size - borderThickness / 2, size - borderThickness / 2);
                                        var radius = size * 0.2f;
                                        canvas.DrawRoundRect(rect, radius, radius, borderPaint);
                                        break;
                                    case AvatarShape.Square:
                                        canvas.DrawRect(borderThickness / 2, borderThickness / 2, size - borderThickness, size - borderThickness, borderPaint);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }

#pragma warning disable CS0618 // Type or member is obsolete
                        using (var textPaint = new SKPaint
                        {
                            IsAntialias = true,
                            Color = SKColors.White,
                            TextAlign = SKTextAlign.Center,
                            Typeface = SKTypeface.FromFamilyName(fontFamily, SKFontStyle.Bold),
                            TextSize = size  * 0.33f
                        })
                        {
                            var textBounds = new SKRect();
                            textPaint.MeasureText(initials, ref textBounds);
                            var xText = size / 2;
                            var yText = size / 2 - textBounds.MidY;
                            canvas.DrawText(initials, xText, yText, textPaint);


                            float textX = size / 2;
                            var textY = size / 2 + textPaint.TextSize / 3;

                            canvas.DrawText(initials, textX, textY, textPaint);

                            using (var image = SKImage.FromBitmap(bitmap))
                            {
                                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                                {
                                    return data.ToArray();
                                }
                            }
                        }
#pragma warning restore CS0618 // Type or member is obsolete


                    }
                }
            }
        }
    }

    public enum AvatarShape
    {
        Circle,
        RoundedSquare,
        Square
    }

}
