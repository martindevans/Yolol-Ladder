using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using YololCompetition.Extensions;
using YololCompetition.Services.Execute;

namespace YololCompetition.Services.Jupyter
{
    public class JupyterContext
    {
        private class MessageResponse
        {
            public SocketMessage Input { get; set; }
            public IUserMessage Response { get; set; }
            public IExecutionState? State { get; set; }

            public MessageResponse(SocketMessage input, IUserMessage response)
            {
                Input = input;
                Response = response;
                State = null;
            }
        }

        private readonly IYololExecutor _executor;

        private readonly ConcurrentDictionary<ulong, bool> _inputMessagesSet = new();
        private readonly ConcurrentQueue<SocketMessage> _edits = new();
        private readonly ConcurrentQueue<ulong> _deletions = new();

        private readonly List<MessageResponse> _messages = new();

        private CancellationTokenSource _cts = new();

        public JupyterContext(IYololExecutor executor)
        {
            _executor = executor;
        }

        public async Task Run(InteractiveBase context)
        {
            // Subscribe to edit/delete events for the duration of the run
            context.Interactive.Discord.MessageUpdated += OnMessageUpdated;
            context.Interactive.Discord.MessageDeleted += OnMessageDeleted;

            while (true)
            {
                // Wait for a new input from the user, or for some other event to interrupt
                var command = await context.NextMessageAsync(true, true, null, _cts.Token);
                _cts = new CancellationTokenSource();

                // Apply changes to inputs (edits/deletions)
                ApplyEdits();
                await ApplyDeletions();

                // Add new inputs and exit if necessary
                if (await ApplyNew(context, command))
                {
                    await context.Context.Channel.SendMessageAsync("Ended session.");
                    break;
                }

                // Re-Evaluate all of the messages from the top again and edit the responses into place
                await Evaluate();

                // Wait a short while to ratelimit jupyter session evaluation load
                await Task.Delay(1500);
            }
        }

        private async Task Evaluate()
        {
            IExecutionState? previous = null;
            foreach (var message in _messages)
            {
                // Skip forward to the first message with a null state (i.e. needs evaluating)
                if (message.State != null)
                {
                    previous = message.State;
                    continue;
                }

                previous = await EvaluateSingle(message.Input, message.Response, previous);
            }

            async Task<IExecutionState?> EvaluateSingle(IMessage input, IUserMessage output, IExecutionState? prevState)
            {
                // Try to get code from message
                var code = input.Content.ExtractYololCodeBlock();
                if (code == null)
                {
                    await output.ModifyAsync2(@"Failed to parse a yolol program from message - ensure you have enclosed your solution in triple backticks \`\`\`like this\`\`\`");
                    return prevState;
                }
            
                // Try to parse code as Yolol
                var result = Yolol.Grammar.Parser.ParseProgram(code);
                if (!result.IsOk)
                {
                    await output.ModifyAsync2(result.Err.ToString());
                    return prevState;
                }

                // Setup a new state
                var state = await _executor.Prepare(result.Ok);
                state.TerminateOnPcOverflow = true;
                prevState?.CopyTo(state);

                // Execute for 2000 ticks
                var err = await state.Run(2000, TimeSpan.FromMilliseconds(150));

                // Print out result
                if (err != null)
                    await output.ModifyAsync2(err);
                else
                    await output.ModifyAsync2(embed: state.ToEmbed().Build());

                return state;
            }
        }

        private async Task ApplyDeletions()
        {
            while (_deletions.TryDequeue(out var deleted))
            {
                _inputMessagesSet.Remove(deleted, out _);

                // Find index of deleted item
                var d = deleted;
                var i = _messages.FindIndex(a => a.Input.Id == d);

                // Clear the state of all messages after the deleted one and then delete it
                if (i != -1)
                {
                    for (var j = i; j < _messages.Count; j++)
                        _messages[j].State = null;
                    await _messages[i].Response.DeleteAsync();
                }
            }
        }

        private void ApplyEdits()
        {
            while (_edits.TryDequeue(out var edited))
            {
                // Find index of deleted item
                var e = edited;
                var i = _messages.FindIndex(a => a.Input.Id == e.Id);

                // Clear the state of all messages after the edited one and then replace edited message
                if (i != -1)
                {
                    for (var j = i; j < _messages.Count; j++)
                        _messages[j].State = null;
                    _messages[i].Input = e;
                }
            }
        }

        private async Task<bool> ApplyNew(InteractiveBase context, SocketMessage? command)
        {
            if (command != null)
            {
                if (command.Content == "exit")
                    return true;

                _inputMessagesSet.TryAdd(command.Id, true);
                var r = await context.Context.Channel.SendMessageAsync("Evaluating...");
                _messages.Add(new MessageResponse(command, r));
            }

            return false;
        }

        private async Task OnMessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel __)
        {
            await Task.CompletedTask;

            if (!_inputMessagesSet.ContainsKey(cache.Id))
                return;

            _deletions.Enqueue(cache.Id);
            _cts.Cancel();
            
        }

        private async Task OnMessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage msg, ISocketMessageChannel __)
        {
            await Task.CompletedTask;

            if (!_inputMessagesSet.ContainsKey(cache.Id))
                return;

            _edits.Enqueue(msg);
            _cts.Cancel();
        }
    }
}
