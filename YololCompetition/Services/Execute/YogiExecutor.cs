using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using Yolol.Execution;
using Yolol.Grammar;
using Newtonsoft.Json;
using System.Linq;

namespace YololCompetition.Services.Execute
{
    /// <summary>
    /// Yolol executor which calls out to an external process
    /// </summary>
    internal class YogiExecutor
        : IYololExecutor
    {
        private readonly string _path;

        public YogiExecutor(string path)
        {
            _path = path;
        }

        public async Task<IEnumerable<IExecutionState>> Prepare(IEnumerable<Yolol.Grammar.AST.Program> programs, string done = ":done")
        {
            return from program in programs
                   select new YogiExecutionState(_path, program, done);
        }

        private class YogiExecutionState
            : IExecutionState
        {
            private Dictionary<VariableName, Value> _state = new();

            private readonly Yolol.Grammar.AST.Program _program;
            private readonly VariableName _done;
            private readonly string _exePath;

            public bool Done { get => _state[_done].ToBool(); set => Set(_done, (Number)value); }
            public int ProgramCounter { get; private set; }
            public ulong TotalLinesExecuted { get; private set; }
            public bool TerminateOnPcOverflow { get; set; }

            public YogiExecutionState(string exePath, Yolol.Grammar.AST.Program program, string done)
            {
                _program = program;
                _done = new VariableName(done);
                _exePath = exePath;

                Set(_done, Number.Zero);
            }

            public async Task<string?> Run(uint lineExecutionLimit, TimeSpan timeout)
            {
                //todo: pass in:
                // - Yolol code
                // - variable values
                // - program counter
                // - terminate on PC overflow

                var stdOut = new StringBuilder();
                var result = await Cli.Wrap(_exePath)
                    .WithArguments($"--stop-flag {_done} --max-steps {lineExecutionLimit} --max-sec {timeout.TotalSeconds}")
                    .WithStandardInputPipe(PipeSource.FromString(_program.ToString()))
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                    .ExecuteAsync();

                //todo: get back:
                // - parser error
                // - compile error
                // - program counter
                // - lines executed

                var parsed = JsonConvert.DeserializeObject<YogiExecutionResult>(stdOut.ToString());

                // TotalLinesExecuted += ???

                throw new NotImplementedException();
            }

            public void Set(VariableName name, Value value)
            {
                _state[name] = value;
            }

            public Value? TryGet(VariableName name)
            {
                if (_state.TryGetValue(name, out var value))
                    return value;
                return null;
            }

            public IEnumerator<KeyValuePair<VariableName, Value>> GetEnumerator()
            {
                return _state.GetEnumerator();
            }
        }

        private class YogiExecutionResult
        {

        }
    }
}
