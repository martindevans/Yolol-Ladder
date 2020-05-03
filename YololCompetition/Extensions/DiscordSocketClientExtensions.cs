using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace YololCompetition.Extensions
{
    public static class DiscordSocketClientExtensions
    {
        public static async Task<string> GetUserName(this DiscordSocketClient client, ulong id)
        {
            var user = (IUser)client.GetUser(id) ?? await client.Rest.GetUserAsync(id);
            return user?.Username ?? $"U{id}?";
        }
    }
}
