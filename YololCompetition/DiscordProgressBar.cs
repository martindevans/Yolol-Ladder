using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace YololCompetition
{
    public class DiscordProgressBar
        : IAsyncDisposable
    {
        private readonly string _prefix;
        private readonly IUserMessage _message;

        public DiscordProgressBar(string prefix, IUserMessage message)
        {
            _prefix = prefix;
            _message = message;
        }

        public async Task SetProgress(float progress)
        {
            var fillCount = (int)(Math.Clamp(progress, 0, 1) * 50);
            var filler = string.Join("", Enumerable.Repeat('#', fillCount));
            var spaces = string.Join("", Enumerable.Repeat('-', 50 - fillCount));

            await _message.ModifyAsync(a => a.Content = $"{_prefix} |{filler}{spaces}| ({progress * 100:F1}%)");
        }

        public async ValueTask DisposeAsync()
        {
            await _message.ModifyAsync(a => a.Content = _prefix);
        }
    }
}
