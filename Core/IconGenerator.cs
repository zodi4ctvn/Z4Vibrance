using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace VibranceZ.Core
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Bitmap CreateAppBitmap(int size)
        {
            // Render at 4x supersampling for razor-sharp edges at small sizes (16x16, 20x20, 32x32)
            int scale = size <= 32 ? 4 : 2;
            int hiSize = size * scale;

            using (var hiBmp = new Bitmap(hiSize, hiSize, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(hiBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                float s = hiSize;
                float pad = s * 0.05f;
                float rectSize = s - pad * 2f;
                float radius = s * 0.24f;

                // Matte dark squircle base
                using (var bgPath = CreateRoundedRect(pad, pad, rectSize, rectSize, radius))
                {
                    using (var bgBrush = new LinearGradientBrush(
                        new PointF(0, pad),
                        new PointF(0, pad + rectSize),
                        Color.FromArgb(30, 34, 43),
                        Color.FromArgb(18, 21, 27)))
                    {
                        g.FillPath(bgBrush, bgPath);
                    }

                    using (var borderPen = new Pen(Color.FromArgb(56, 63, 77), Math.Max(1.5f, s * 0.025f)))
                    {
                        g.DrawPath(borderPen, bgPath);
                    }
                }

                // Subtle precision inner dial ring
                float cx = s * 0.5f;
                float cy = s * 0.5f;
                float ringR = s * 0.34f;
                using (var ringPen = new Pen(Color.FromArgb(40, 255, 255, 255), Math.Max(1.2f, s * 0.018f)))
                {
                    g.DrawEllipse(ringPen, cx - ringR, cy - ringR, ringR * 2f, ringR * 2f);
                }

                // Modern geometric layered "V" prism mark
                // Left wing: crisp steel-blue
                var leftWing = new[]
                {
                    new PointF(s * 0.24f, s * 0.29f),
                    new PointF(s * 0.38f, s * 0.29f),
                    new PointF(s * 0.53f, s * 0.72f),
                    new PointF(s * 0.43f, s * 0.72f)
                };
                using (var bLeft = new SolidBrush(Color.FromArgb(66, 135, 245)))
                {
                    g.FillPolygon(bLeft, leftWing);
                }

                // Right wing: crisp matte silver-white
                var rightWing = new[]
                {
                    new PointF(s * 0.62f, s * 0.29f),
                    new PointF(s * 0.76f, s * 0.29f),
                    new PointF(s * 0.57f, s * 0.72f),
                    new PointF(s * 0.47f, s * 0.72f)
                };
                using (var bRight = new SolidBrush(Color.FromArgb(234, 238, 245)))
                {
                    g.FillPolygon(bRight, rightWing);
                }

                // Center prism intersection diamond (muted emerald accent)
                var centerFacet = new[]
                {
                    new PointF(s * 0.50f, s * 0.56f),
                    new PointF(s * 0.555f, s * 0.65f),
                    new PointF(s * 0.50f, s * 0.72f),
                    new PointF(s * 0.445f, s * 0.65f)
                };
                using (var bCenter = new SolidBrush(Color.FromArgb(52, 168, 108)))
                {
                    g.FillPolygon(bCenter, centerFacet);
                }

                // Subtle top-right color-calibration indicator bar (warm amber)
                using (var barPath = CreateRoundedRect(s * 0.65f, s * 0.21f, s * 0.13f, s * 0.045f, s * 0.02f))
                using (var bBar = new SolidBrush(Color.FromArgb(224, 128, 72)))
                {
                    g.FillPath(bBar, barPath);
                }

                var finalBmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (var gOut = Graphics.FromImage(finalBmp))
                {
                    gOut.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gOut.SmoothingMode = SmoothingMode.AntiAlias;
                    gOut.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    gOut.DrawImage(hiBmp, 0, 0, size, size);
                }
                return finalBmp;
            }
        }

        public static Icon CreateAppIcon(int size = 64)
        {
            using (var bmp = CreateAppBitmap(size))
            {
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    using (var tempIcon = Icon.FromHandle(hIcon))
                    {
                        return (Icon)tempIcon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        public static void SaveIcoFile(string path)
        {
            int[] sizes = new[] { 16, 24, 32, 48, 64, 128 };
            byte[][] pngData = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                using (var bmp = CreateAppBitmap(sizes[i]))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngData[i] = ms.ToArray();
                }
            }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);
                bw.Write((short)1);
                bw.Write((short)sizes.Length);

                int offset = 6 + (16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    bw.Write(dim);
                    bw.Write(dim);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(pngData[i].Length);
                    bw.Write(offset);
                    offset += pngData[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngData[i]);
                }
            }
        }

        private static GraphicsPath CreateRoundedRect(float x, float y, float w, float h, float r)
        {
            var path = new GraphicsPath();
            float d = r * 2f;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
