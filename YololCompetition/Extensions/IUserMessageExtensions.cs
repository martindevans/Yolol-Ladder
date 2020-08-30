using System.Threading.Tasks;
using Discord;

namespace YololCompetition.Extensions
{
    public static class IUserMessageExtensions
    {
        public static async Task ModifyAsync2(this IUserMessage message, string? content = null, Embed? embed = null)
        {
            if (content == null && embed == null)
            {
                if (!string.IsNullOrWhiteSpace(message.Content))
                    await message.ModifyAsync(a => a.Content = "");
            }
            else  if (content != null && message.Content != content)
            {
                await message.ModifyAsync(a => {
                    a.Embed = null;
                    a.Content = content;
                });
            }
            else if (embed != null)
            {
                await message.ModifyAsync(a => {
                    a.Embed = embed;
                    a.Content = "";
                });
            }
        }
    }
}
