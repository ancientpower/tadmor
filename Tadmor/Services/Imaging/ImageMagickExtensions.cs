﻿using System;
using System.Drawing;
using ImageMagick;

namespace Tadmor.Services.Imaging
{
    public static class ImageMagickExtensions
    {
        public static Drawables Composite(
            this Drawables drawables, 
            Rectangle rectangle, 
            CompositeOperator composite, 
            IMagickImage image, 
            Gravity gravity,
            Size offset = default)
        {
            var rectW = rectangle.Size.Width;
            var rectH = rectangle.Size.Height;
            var imgW = image.Width;
            var imgH = image.Height;
            var imgSize = new Size(imgW, imgH);
            var posDelta = gravity switch
            {
                Gravity.Undefined => new Size(),
                Gravity.Northwest => new Size(),
                Gravity.North => new Size((rectW - imgW) / 2, 0),
                Gravity.Northeast => new Size(rectW - imgW, 0),
                Gravity.West => new Size(0, (rectH - imgH) / 2),
                Gravity.Center => (rectangle.Size - imgSize) / 2,
                Gravity.East => new Size(rectW - imgW, (rectH - imgH) / 2),
                Gravity.Southwest => new Size(0, rectH - imgH),
                Gravity.South => new Size((rectW - imgW) / 2, rectH - imgH),
                Gravity.Southeast => rectangle.Size - imgSize,
                _ => throw new ArgumentOutOfRangeException(nameof(gravity))
            };
            var pos = rectangle.Location + posDelta + offset;
            return drawables.Composite(pos.X, pos.Y, composite, image);
        }

        public static Drawables Text(this Drawables drawables, string text, Rectangle textRectangle, string font,
            MagickColor? textColor = default, Gravity textGravity = Gravity.Center, bool wordWrap = false, double fontPointSize = 0)
        {
            var textType = wordWrap ? "caption" : "label";
            var textCanvas = new MagickImage($"{textType}:{text}", new MagickReadSettings
            {
                FontFamily = font,
                Font = font,
                Width = textRectangle.Width,
                Height = textRectangle.Height,
                FillColor = textColor,
                TextGravity = textGravity,
                FontPointsize = fontPointSize,
                BackgroundColor = MagickColors.Transparent
            });
            drawables.Composite(textRectangle.X, textRectangle.Y, CompositeOperator.Over, textCanvas);
            return drawables;
        }

        public static void SetOpacity(this MagickImage image, float opacity)
        {
            image.Alpha(AlphaOption.Set);
            image.Evaluate(Channels.Alpha, EvaluateOperator.Multiply, opacity);
        }
    }
}