using System;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using YololCompetition.Services.Rates;

namespace YololCompetition.Attributes
{
    public class RateLimitAttribute
        : PreconditionAttribute
    {
        private readonly string _message;
        private readonly Guid _id;
        private readonly TimeSpan _cooldown;

        public RateLimitAttribute(string id, uint cooldownSeconds, string message)
        {
            _message = message;
            _id = Guid.Parse(id);
            _cooldown = TimeSpan.FromSeconds(cooldownSeconds);
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // If there's no rate limit service always succeed
            var limit = (IRateLimit?)services.GetService(typeof(IRateLimit));
            if (limit == null)
                return PreconditionResult.FromSuccess();

            // Get when this was last used
            var previous = (await limit.TryGetLastUsed(_id, context.User.Id)) ?? DateTime.MinValue;

            // Set the last-used time to now
            await limit.Use(_id, context.User.Id);

            // Return an error if it was last used too recently
            var elapsed = DateTime.UtcNow - previous;
            if (elapsed < _cooldown)
                return PreconditionResult.FromError(_message);

            // Success!
            return PreconditionResult.FromSuccess();
        }
    }
}
