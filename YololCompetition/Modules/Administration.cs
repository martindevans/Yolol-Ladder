using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Fonts;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class Administration
        : ModuleBase
    {
        [Command("kill"), RequireOwner, Summary("Immediately kill the bot")]
        public async Task Kill(int exitCode = 1)
        {
            await Task.CompletedTask;
            Environment.Exit(exitCode);
        }

        [Command("test-png"), RequireOwner, Summary("Render a PNG image")]
        public async Task PngTest()
        {
            using var image = new Image<Rgba32>(128, 128);
            var pen = new Pen(Color.RebeccaPurple, 3);
            image.Mutate(x => x.DrawLines(pen, new PointF(0, 0), new PointF(128, 128)));

            await using var memory = new MemoryStream(image.PixelType.BitsPerPixel / 8 * image.Width * image.Height);
            image.SaveAsPng(memory);
            memory.Seek(0, SeekOrigin.Begin);

            await Context.Channel.SendFileAsync(memory, "TestImage.png");
        }

        [Command("test-gif"), RequireOwner, Summary("Render a GIF image")]
        public async Task GifTest()
        {
            var pen = new Pen(Color.RebeccaPurple, 3);

            using var f1 = new Image<Rgba32>(128, 128);
            f1.Mutate(x => x.DrawLines(pen, new PointF(0, 0), new PointF(128, 128)));

            using var f2 = new Image<Rgba32>(128, 128);
            f2.Mutate(x => x.DrawLines(pen, new PointF(128, 0), new PointF(0, 128)));

            using var final = new Image<Rgba32>(128, 128);
            var ff1 = final.Frames.AddFrame(f1.Frames[0]);
            var md1 = ff1.Metadata.GetFormatMetadata(GifFormat.Instance);
            md1.FrameDelay = 50;
            var ff2 = final.Frames.AddFrame(f2.Frames[0]);
            var md2 = ff2.Metadata.GetFormatMetadata(GifFormat.Instance);
            md2.FrameDelay = 0;
            final.Frames.RemoveFrame(0);

            await using var memory = new MemoryStream(final.PixelType.BitsPerPixel / 8 * final.Width * final.Height);
            final.SaveAsGif(memory);
            memory.Seek(0, SeekOrigin.Begin);

            await Context.Channel.SendFileAsync(memory, "TestImage.gif");
        }

        [Command("test-graph"), RequireOwner, Summary("Render a graph")]
        public async Task GifTest2()
        {
            var font = GetFont("sans-serif", "monospace", "Arial");

            const int axisSize = 24;
            const int axisFontDifference = 2;
            const int axisFontSize = axisSize - axisFontDifference;
            const int width = 512;
            const int height = 512;

            var axisPen = new Pen(Color.Black, 3);
            var axisFont = new Font(font, axisFontSize);

            using var final = new Image<Rgba32>(width, height);
            final.Mutate(a => a.Fill(Color.AntiqueWhite));
            final.Mutate(a => a.DrawLines(axisPen, new PointF(axisSize, 0), new PointF(axisSize, height)));
            final.Mutate(a => a.DrawLines(axisPen, new PointF(0, height - axisSize), new PointF(width, height - axisSize)));
            final.Mutate(a => a.DrawText("Chars -->", axisFont, Color.Black, new PointF(axisSize + axisFontSize, height - axisFontSize + axisFontDifference / 2f)));
            final.Mutate(a => a.Rotate(90));
            final.Mutate(a => a.DrawText("Ticks -->", axisFont, Color.Black, new PointF(axisSize + axisFontSize, axisFontDifference)));
            final.Mutate(a => a.Rotate(-90));

            var outputFont = new Font(font, 13);
            var r = new Random();
            for (var i = 0; i < 30; i++)
            {
                var x = (float)r.NextDouble() * (width - axisSize);
                var y = (float)r.NextDouble() * (height - axisSize);

                var i1 = i;
                final.Mutate(a => a.DrawText(i1.ToString(), outputFont, Color.Red, new PointF(x + axisSize, height - y - axisSize)));
            }

            await using var memory = new MemoryStream(final.PixelType.BitsPerPixel / 8 * final.Width * final.Height);
            final.SaveAsPng(memory);
            memory.Seek(0, SeekOrigin.Begin);


            await Context.Channel.SendFileAsync(memory, "TestImage.png");
        }

        private static FontFamily GetFont(params string[] names)
        {
            foreach (var name in names)
                if (SystemFonts.TryFind(name, out var font))
                     return font;

            return SystemFonts.Families.First();
        }
    }
}
