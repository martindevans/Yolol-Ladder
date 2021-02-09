using System;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace YololCompetition.Modules
{
    [RequireOwner]
    public class Administration
        : ModuleBase
    {
        [Command("kill"), RequireOwner, Summary("Immediately kill the bot")]
        public async Task Kill(int exitCode = 1)
        {
            await ReplyAsync("x_x");
            Environment.Exit(exitCode);
        }

        [Command("simd"), RequireOwner]
        public async Task Simd()
        {
            var embed = new EmbedBuilder().WithTitle("SIMD Support").WithDescription(
                $" - AVX:  {Avx.IsSupported}\n" +
                $" - AVX2: {Avx2.IsSupported}\n" + 
                $" - BMI1: {Bmi1.IsSupported}\n" + 
                $" - BMI2: {Bmi2.IsSupported}\n" + 
                $" - FMA:  {Fma.IsSupported}\n" + 
                $" - LZCNT:{Lzcnt.IsSupported}\n" + 
                $" - PCLMULQDQ:{Pclmulqdq.IsSupported} (wtf?)\n" + 
                $" - POPCNT:{Popcnt.IsSupported}\n" + 
                $" - POPCNT:{Popcnt.IsSupported}\n" + 
                $" - SSE:{Sse.IsSupported}\n" + 
                $" - SSE2:{Sse2.IsSupported}\n" + 
                $" - SSE3:{Sse3.IsSupported}\n" + 
                $" - SSSE3:{Ssse3.IsSupported} (seriously?)\n" + 
                $" - SSE41:{Sse41.IsSupported}\n" + 
                $" - SSE42:{Sse42.IsSupported}\n" + 
                $" - SSE42:{Sse42.IsSupported}\n"
            ).Build();

            await ReplyAsync(embed: embed);
        }
    }
}
