using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CliWrap;
using Yolol.Execution;
using Yolol.Grammar;
using Newtonsoft.Json;
using System.Linq;
using JetBrains.Annotations;

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
            private readonly Dictionary<VariableName, Value> _state = new();

            private readonly Yolol.Grammar.AST.Program _program;
            private readonly VariableName _done;
            private readonly string _exePath;

            public bool Done { get => _state[_done].ToBool(); set => Set(_done, (Number)value); }
            public int ProgramCounter { get; private set; }
            public ulong TotalLinesExecuted { get; private set; }
            public bool TerminateOnPcOverflow { get; set; }

            public string Code => _program.ToString();

            public YogiExecutionState(string exePath, Yolol.Grammar.AST.Program program, string done)
            {
                _done = new VariableName(done);
                _exePath = exePath;
                _program = program;

                Set(_done, Number.Zero);
            }

            public async Task<string?> Run(uint lineExecutionLimit, TimeSpan timeout)
            {
                var terminate = TerminateOnPcOverflow ? "--term-pc-of" : "";
                var stdOut = new StringBuilder();

                try
                {
                    await Cli.Wrap(_exePath)
                        .WithArguments($"--stop-flag \"{_done}\" --max-steps {lineExecutionLimit} --max-sec {timeout.TotalSeconds} --start-pc {ProgramCounter} {terminate}")
                        .WithStandardInputPipe(PipeSource.FromString(CreateStdIn()))
                        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                        .ExecuteAsync();
                }
                catch (Exception ex)
                {
                    return ex.ToString();
                }

                var parsed = JsonConvert.DeserializeObject<YogiExecutionResult>(stdOut.ToString());
                if (parsed == null)
                    return "Deserialised null execution state";
                if (parsed.Error != null)
                    return parsed.Error;
                if (parsed.Vars == null)
                    return "Execution state contains no Vars";

                TotalLinesExecuted += parsed.ElapsedLines;
                ProgramCounter = parsed.CurrentLine;
                foreach (var item in parsed.Vars)
                    Set(item.VariableName, item.Value);

                return null;
            }

            private string CreateStdIn()
            {
                var vars = (from v in _state
                            select new VarData(v.Key, v.Value)).ToArray();

                var json = JsonConvert.SerializeObject(vars);

                return $"{json}\0{Code}";
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

#pragma warning disable 0649

        private class YogiExecutionResult
        {
            [JsonProperty(PropertyName = "error")]
            [UsedImplicitly]
            public string? Error { get; set; }

            [JsonProperty(PropertyName = "vars")]
            [UsedImplicitly]
            public List<VarData> Vars;

            [JsonProperty(PropertyName = "elapsed_lines")]
            [UsedImplicitly]
            public uint ElapsedLines;

            [JsonProperty(PropertyName = "current_line")]
            [UsedImplicitly]
            public int CurrentLine;

            //pub elapsed_s: f32,
            //pub mean_lps: f32,
            //pub stddev_lps: f32,
            //pub current_line: usize,

            public YogiExecutionResult()
            {
                Vars = new List<VarData>();
                ElapsedLines = 0;
                CurrentLine = 0;
            }
        }

        private class VarData
        {
            [JsonProperty(PropertyName = "name")]
            [UsedImplicitly]
            public string? Name;

            [JsonProperty(PropertyName = "global")]
            [UsedImplicitly]
            public bool Global;

            [JsonProperty(PropertyName = "value")]
            [UsedImplicitly]
            public DataValue? DataValue;

            [JsonIgnore] public VariableName VariableName => new((Global ? ":" : "") + Name);
            [JsonIgnore] public Value Value => DataValue?.Value ?? Number.Zero;

            public VarData()
            {
            }

            public VarData(VariableName name, Value value)
            {
                Name = name.Name.TrimStart(':');
                Global = name.IsExternal;
                DataValue = new DataValue(value);
            }
        }

        private class DataValue
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            [UsedImplicitly]
            public string? Number;

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            [UsedImplicitly]
            public string? String;

            [JsonIgnore] public Value Value => Number != null ? (Number)decimal.Parse(Number) : new Value(new YString(String ?? ""));

            public DataValue()
            {
            }

            public DataValue(Value value)
            {
                if (value.Type == Yolol.Execution.Type.Number)
                    Number = value.ToString();
                else
                    String = value.ToString();
            }
        }
#pragma warning restore 0649
    }
}
