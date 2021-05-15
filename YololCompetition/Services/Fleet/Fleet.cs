using System.Data.Common;
using System.Threading.Tasks;
using Discord.WebSocket;
using YololCompetition.Extensions;

namespace YololCompetition.Services.Fleet
{
    public readonly struct Fleet
    {
        public ulong Id { get; }

        public string Name { get; }

        public ulong OwnerId { get; }

        public Fleet(ulong id, string name, ulong ownerId)
        {
            Id = id;
            Name = name;
            OwnerId = ownerId;
        }

        public async Task<string> FormattedName(DiscordSocketClient client) => $"{Name} ({await client.GetUserName(OwnerId)})";

        public static Fleet Read(DbDataReader row)
        {
            return new Fleet(
                ulong.Parse(row["FleetId"].ToString()!),
                row["Name"].ToString()!,
                ulong.Parse(row["UserId"].ToString()!)
            );
        }
    }
}
