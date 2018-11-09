﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MoreLinq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Convolution;
using SixLabors.ImageSharp.Processing.Dithering;
using SixLabors.ImageSharp.Processing.Dithering.Ordered;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Pens;
using SixLabors.ImageSharp.Processing.Text;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.Primitives;
using SixLabors.Shapes;
using Tadmor.Extensions;
using Tadmor.Resources;

namespace Tadmor.Services.Imaging
{
    public class ImagingService
    {
        private static readonly FontFamily Arial = SystemFonts.Find("Arial");
        private static readonly FontFamily TimesNewRoman = SystemFonts.Find("Times New Roman");
        private static readonly FontFamily MsSansSerif = new FontCollection().Install(Resource.Load("micross.ttf"));

        private static readonly FontFamily GothamRoundedLight =
            new FontCollection().Install(Resource.Load("GothamRoundedLight.ttf"));

        private static readonly FontFamily HelveticaNeue =
            new FontCollection().Install(Resource.Load("HelveticaNeue.ttf"));

        private static readonly FontFamily HelveticaNeueMedium =
            new FontCollection().Install(Resource.Load("HelveticaNeueMedium.ttf"));

        public MemoryStream AlignmentChart(IEnumerable<(Random rng, byte[] avatarData)> rngAndAvatarDatas,
            string[] options)
        {
            //constants
            const int cellW = 500;
            const int cellH = 300;
            const int horMargin = 30;
            const int verMargin = 20;
            const int textHeight = 80;
            const int textMargin = 10;
            var color = Rgba32.LightGray;

            if (options.Length % 2 != 0) throw new Exception("need an even number of options");
            if (options.Length > 16) throw new Exception("please no");
            //computed variables
            var axisLength = options.Length / 2;
            var cells = options.Take(axisLength)
                .Cartesian(options.Skip(axisLength), (s1, s2) => s1 == s2 ? $"true {s1}" : $"{s1} {s2}")
                .Batch(axisLength)
                .SelectMany((col, x) => col.Select((cell, y) => (cell: cell.ToUpper(), x, y)))
                .ToList();
            var alignmentString = string.Concat(cells.Select(t => t.cell));
            var rows = cells.Max(t => t.y) + 1;
            var cols = cells.Max(t => t.x) + 1;
            var avatarsByCell = rngAndAvatarDatas
                .Select(tuple => (rng: (alignmentString, tuple.rng.Next()).ToRandom(), tuple.avatarData))
                .ToLookup(
                    tuple => (tuple.rng.Next(cols), tuple.rng.Next(rows)),
                    tuple => CropCircle(tuple.avatarData));
            var font = TimesNewRoman.CreateFont(40);
            var textOptions = new TextGraphicsOptions(true) {HorizontalAlignment = HorizontalAlignment.Center};
            var output = new MemoryStream();
            using (var canvas = new Image<Rgba32>(cellW * cols, cellH * rows))
            {
                canvas.Mutate(c =>
                {
                    //background
                    c.Fill(Rgba32.Black);

                    foreach (var (text, x, y) in cells)
                    {
                        //alignment cell
                        var cellRect = new RectangleF(cellW * x, cellH * y, cellW, cellH - textHeight);
                        cellRect.Inflate(-horMargin, -verMargin);
                        var cellCenter = cellRect.Location + cellRect.Size / 2;

                        //alignment text
                        var textPosition = cellCenter + new PointF(0, cellRect.Height / 2 + textMargin);
                        c.DrawText(textOptions, text, font, color, textPosition);

                        //avatars
                        var avatarsForCell = avatarsByCell[(x, y)].ToList();
                        if (avatarsForCell.Any())
                        {
                            //draw all the avatars on one image, then resize if necessary
                            var avatarWidth = avatarsForCell.First().Width;
                            var avatars = new Image<Rgba32>(avatarWidth * avatarsForCell.Count, avatarWidth);
                            avatars.Mutate(ac =>
                            {
                                for (var i = 0; i < avatarsForCell.Count; i++)
                                    ac.DrawImage(avatarsForCell[i], 1, new Point(i * avatarWidth, 0));
                                if (avatars.Width > cellRect.Width)
                                    ac.Resize((Size) (avatars.Size() * (cellRect.Width / avatars.Width)));
                            });
                            //use the average color from the avatars as cell background
                            var blurryAvatars = avatars.Clone();
                            blurryAvatars.Mutate(i => i.GaussianBlur(10));
                            var averageColor = blurryAvatars[blurryAvatars.Width / 2, blurryAvatars.Height / 2];
                            c.Fill(averageColor, cellRect);
                            var position = cellCenter - avatars.Size() / 2;
                            c.DrawImage(avatars, 1, (Point) position);
                        }
                        else
                        {
                            c.Fill(color, cellRect);
                        }
                    }
                });
                canvas.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public MemoryStream UpDownGif(string text, byte[] avatarData, string baseFilename)
        {
            //credits to https://twitter.com/reedjeyy for base images, font and constants
            //constants
            const int textRightMargin = 1037;
            const int avatarLeftMargin = 1145;
            const int fontHeightCorrection = 22;
            const float fadeDuration = 11;
            const int fadeStartIndex = 65;
            var font = GothamRoundedLight.CreateFont(94);

            var output = new MemoryStream();
            using (var resource = Resource.Load(baseFilename))
            using (var baseImage = Image.Load<Rgba32>(resource))
            using (var textImage = new Image<Rgba32>(baseImage.Width, baseImage.Height))
            {
                //text overlay
                var heightExtent = textImage.Height / 2;
                textImage.Mutate(c =>
                {
                    var avatarImage = avatarData == null ? null : CropCircle(avatarData);
                    var t = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        WrapTextWidth = textRightMargin
                    };
                    var textPosition = new PointF(0, heightExtent - fontHeightCorrection);
                    c.DrawText(t, text, font, Rgba32.White, textPosition);
                    if (avatarImage == null) return;
                    c.DrawImage(avatarImage, 1, new Point(avatarLeftMargin, heightExtent - avatarImage.Height / 2));
                });

                //apply overlay to each frame with proper opacity
                for (var i = 0; i < baseImage.Frames.Count; i++)
                {
                    var frame = baseImage.Frames.ExportFrame(i);
                    var opacity = 1 - Math.Clamp(1 / fadeDuration * (i - fadeStartIndex), 0, 1);
                    frame.Mutate(c => c.DrawImage(textImage, opacity));
                    baseImage.Frames.InsertFrame(i, frame.Frames.Single());
                }

                baseImage.SaveAsGif(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public MemoryStream Ok(string text, byte[] avatarData)
        {
            const float leftMargin = 4;
            const float topMargin = 4;
            const float avatarWidth = 70;
            const float avatarHeight = 74;
            const float textX = 80;
            const float textY = 12;
            var avatarSize = new SizeF(avatarWidth, avatarHeight);
            var avatarPosition = new PointF(leftMargin, topMargin);
            var textPosition = new PointF(textX, textY);
            var font = MsSansSerif.CreateFont(10);

            var output = new MemoryStream();
            using (var resource = Resource.Load("angry.png"))
            using (var baseImage = Image.Load<Rgba32>(resource))
            using (var avatar = Image.Load(avatarData))
            {
                var textOptions = new TextGraphicsOptions
                {
                    WrapTextWidth = baseImage.Width - textPosition.X
                };
                avatar.Mutate(a => a.Resize((Size) avatarSize).Dither(new BayerDither2x2()));
                baseImage.Mutate(i =>
                {
                    i.DrawImage(avatar, 1, (Point) avatarPosition);
                    i.DrawText(textOptions, text, font, Rgba32.Black, textPosition);
                    i.Resize(baseImage.Size() * 3);
                });
                baseImage.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public MemoryStream Text(string name, string text)
        {
            const int textX = 19;
            const int nameY = 252;
            const int textY = 269;
            const int textRightMargin = 20;
            var namePosition = new PointF(textX, nameY);
            var textPosition = new PointF(textX, textY);
            var nameFont = HelveticaNeueMedium.CreateFont(14);
            var textFont = HelveticaNeue.CreateFont(14.75F);
            var textColor = new Rgba32(4, 4, 4);

            var output = new MemoryStream();
            using (var resource = Resource.Load("text1.png"))
            using (var baseImage = Image.Load<Rgba32>(resource))
            {
                var textOptions = new TextGraphicsOptions
                {
                    WrapTextWidth = baseImage.Width - textPosition.X - textRightMargin
                };
                baseImage.Mutate(i =>
                {
                    i.DrawText(textOptions, name, nameFont, textColor, namePosition);
                    i.DrawText(textOptions, text, textFont, textColor, textPosition);
                    i.Resize(baseImage.Size() * 3);
                });
                baseImage.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        private Image<Rgba32> CropCircle(byte[] imageData)
        {
            var image = Image.Load(imageData);
            var circle = new EllipsePolygon(new PointF(image.Size() / 2), image.Size());
            var rectangle = new RectangularPolygon(circle.Bounds);
            var exceptCircle = rectangle.Clip(circle);
            image.Mutate(i =>
            {
                i.Fill(
                    new GraphicsOptions(true) {BlenderMode = PixelBlenderMode.Src}, //use overlay colors
                    Rgba32.Transparent, //overlay with transparency
                    exceptCircle); //outside of the circle's bounds
                i.Resize(128, 128);
            });
            return image;
        }

        public MemoryStream Quadrant((Random rng, byte[])[] rngAndAvatarDatas, string opt1, string opt2, string opt3,
            string opt4)
        {
            //constants
            const int s = 1280; //picture size
            const int margin = s / 25;
            const int textMargin = s / 100;
            var color = Rgba32.Black;

            //computed variables
            var parametersSeed = $"{opt1}{opt2}{opt3}{opt4}".ToLower();
            const int med = s / 2;
            var rendererOptions = new RendererOptions(Arial.CreateFont(35, FontStyle.Bold));
            var opt3Extent = TextMeasurer.Measure(opt3, rendererOptions).Width / 2;
            var opt4Extent = TextMeasurer.Measure(opt4, rendererOptions).Width / 2;
            var output = new MemoryStream();
            using (var canvas = new Image<Rgba32>(s, s))
            {
                canvas.Mutate(c =>
                {
                    //background
                    c.Fill(Rgba32.White);
                    var lineSegment = new LinearLineSegment(new PointF(med, margin), new PointF(med, s - margin));
                    var verticalLine = new Polygon(lineSegment);
                    var horizontalLine = verticalLine.Rotate((float)(Math.PI / 2));
                    c.Draw(new Pen<Rgba32>(color, 5), new PathCollection(verticalLine, horizontalLine));

                    //avatars
                    foreach (var (rng, avatarData) in rngAndAvatarDatas)
                    {
                        var saltedRng = (parametersSeed, rng.Next()).ToRandom();
                        using (var avatar = CropCircle(avatarData))
                        {
                            var avatarPosition = new Point(saltedRng.Next(s - avatar.Width),
                                saltedRng.Next(s - avatar.Height));
                            c.DrawImage(avatar, 1, avatarPosition);
                        }
                    }

                    //text
                    var t = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    c.DrawText(t, opt1, rendererOptions.Font, color, new PointF(med, textMargin));
                    c.DrawText(t, opt2, rendererOptions.Font, color, new PointF(med, s - textMargin * 2));
                    c.DrawText(t, opt3, rendererOptions.Font, color,
                        new PointF(opt3Extent + textMargin, med + textMargin));
                    c.DrawText(t, opt4, rendererOptions.Font, color,
                        new PointF(s - (opt4Extent + textMargin), med + textMargin));
                });
                canvas.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public MemoryStream Poly((Random rng, byte[])[] rngAndAvatars, string[] options)
        {
            //constants
            const int s = 1280; //picture size
            const float extent = s * .5F;
            var color = Rgba32.Black;
            const double polyRadius = s * 0.45;
            var center = new PointF(extent, extent);
            const float textMargin = 10;

            //computed variables
            var smallArial = Arial.CreateFont(28);
            var rendererOptions = new RendererOptions(smallArial);
            var poly = (IPath) new RegularPolygon(center, options.Length, (int) polyRadius, (float) Math.PI);
            var polyBounds = poly.Bounds.Location + poly.Bounds.Size / 2;
            var polyCenter = 2 * center - polyBounds;
            poly = poly.Translate(polyCenter - center);
            var vertices = poly.Flatten().Single().Points;
            var output = new MemoryStream();
            using (var canvas = new Image<Rgba32>(s, s))
            {
                canvas.Mutate(c =>
                {
                    //background
                    c.Fill(Rgba32.White);
                    c.Draw(new Pen<Rgba32>(color, 5), poly);
                    var t = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var optionsAndVertices = options.EquiZip(vertices, (o, v) => (o, v));
                    foreach (var (option, vertex) in optionsAndVertices)
                    {
                        var tPosition = vertex + (vertex - polyCenter) * 0.1F;
                        var tExtent = TextMeasurer.Measure(option, rendererOptions) / 2;
                        var clampedPosition = new PointF(
                            Math.Clamp(tPosition.X, tExtent.Width + textMargin, s - tExtent.Width - textMargin),
                            Math.Clamp(tPosition.Y, tExtent.Height + textMargin, s - tExtent.Height - textMargin));
                        c.DrawText(t, option, smallArial, color, clampedPosition);
                    }

                    //avatars
                    foreach (var (rng, avatarData) in rngAndAvatars)
                    {
                        var saltedRng = (options, rng.Next()).ToRandom();
                        using (var avatar = CropCircle(avatarData))
                        {
                            var vIndex = saltedRng.Next(vertices.Count);
                            var vA = polyCenter;
                            var vB = vertices[vIndex];
                            var vC = vertices[(vIndex + 1) % vertices.Count];
                            var aPosition = RandomPointInTriangle(saltedRng, vA, vB, vC) - avatar.Size() / 2;
                            c.DrawImage(avatar, 1, new Point((int) aPosition.X, (int) aPosition.Y));
                        }
                    }
                });
                canvas.SaveAsPng(output);
            }

            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        private static PointF RandomPointInTriangle(Random rng, PointF a, PointF b, PointF c)
        {
            var r1 = (float) rng.NextDouble();
            var r2 = (float) rng.NextDouble();
            var result = (1 - (float) Math.Sqrt(r1)) * a +
                         (float) Math.Sqrt(r1) * (1 - r2) * b +
                         r2 * (float) Math.Sqrt(r1) * c;
            return result;
        }
    }
}