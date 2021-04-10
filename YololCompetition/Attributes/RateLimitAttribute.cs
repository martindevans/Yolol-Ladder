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
        private readonly TimeSpan _reset;

        public RateLimitAttribute(string id, uint cooldownSeconds, string message)
        {
            _message = message;
            _id = Guid.Parse(id);
            _cooldown = TimeSpan.FromSeconds(cooldownSeconds);
            _reset = _cooldown * 20;
        }

        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // If there's no rate limit service always succeed
            var limit = (IRateLimit?)services.GetService(typeof(IRateLimit));
            if (limit == null)
                return PreconditionResult.FromSuccess();

            // Get when this was last used
            var state = await limit.TryGetLastUsed(_id, context.User.Id);

            // Set the last-used time to now
            await limit.Use(_id, context.User.Id);

            // Check if rate limit needs to be applied
            // - First 50 uses have normal cooldown
            // - After that each use increases cooldown by 100ms
            // - Any usage > _reset time after the last use resets usage count
            if (state.HasValue)
            {
                // Calculate time elapsed since last usage
                var elapsed = DateTime.UtcNow - state.Value.LastUsed;

                if (elapsed > _reset)
                {
                    // Last use was a long time ago, reset cooldown
                    await limit.Reset(_id, context.User.Id);
                }
                else
                {
                    var extra = Math.Max(0, (long)state.Value.UseCount - 50) * 100;
                    var cooldown = _cooldown + TimeSpan.FromMilliseconds(extra);
                    if (elapsed < cooldown)
                        return PreconditionResult.FromError(_message);
                }
            }

            // Success!
            return PreconditionResult.FromSuccess();
        }
    }
}
