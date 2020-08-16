using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using Type = Yolol.Execution.Type;

namespace YololCompetition.Services.Execute
{
    public class YololInterpretExecutor
        : IYololExecutor
    {
        public IExecutionState Prepare(Yolol.Grammar.AST.Program program, string done)
        {
            return new InterpreterState(program, done);
        }

        private class InterpreterState
            : IExecutionState, IEnumerable<KeyValuePair<VariableName, Value>>
        {
            private readonly Yolol.Grammar.AST.Program _program;
            private readonly DefaultValueDeviceNetwork _network;
            private readonly MachineState _state;
            private readonly IVariable _done;

            private int _programCounter;
            public int ProgramCounter => _programCounter + 1;

            public bool Done
            {
                get => _done.Value.ToBool();
                set => _done.Value = (Number)value;
            }
            public ulong TotalLinesExecuted { get; private set; }

            public InterpreterState(Yolol.Grammar.AST.Program program, string done)
            {
                _program = program;
                _network = new DefaultValueDeviceNetwork();
                _state = new MachineState(_network);
                _done = _state.GetVariable(done);
            }

            public async Task<string?> Run(uint lineExecutionLimit, TimeSpan timeout)
            {
                var timer = new Stopwatch();
                timer.Start();

                // Run lines until completion indicator is set or execution time limit is exceeded
                var executed = 0;
                while (!Done && executed++ < lineExecutionLimit)
                {
                    try
                    {
                        TotalLinesExecuted++;

                        // If line if blank, just move to the next line
                        if (_programCounter >= _program.Lines.Count)
                            _programCounter++;
                        else
                            _programCounter = _program.Lines[_programCounter].Evaluate(_programCounter, _state);
                    }
                    catch (ExecutionException)
                    {
                        _programCounter++;
                    }

                    // loop around if program counter goes over max
                    if (_programCounter >= 20)
                        _programCounter = 0;

                    // Occasionally delay the task a little to make sure it can't dominate other work
                    if (executed % 100 == 10)
                        await Task.Delay(1);

                    // Execution timeout
                    if (timer.Elapsed > timeout)
                        return "Execution Timed Out!";

                    // Sanity check strings are not getting too long
                    var strings = (from v in _state
                                   where v.Value.Value.Type == Type.String
                                   select v.Value.Value)
                        .Concat(from v in _network
                                where v.Item2.Type == Type.String
                                select v.Item2);

                    foreach (var str in strings)
                    {
                        if (str.String.Length < 5000)
                            continue;
                        return "Max String Length Exceeded!";
                    }
                }

                return null;
            }

            public Value? TryGet(string name)
            {
                return _state.GetVariable(name)?.Value;
            }

            public bool TrySet(string name, Value value)
            {
                var v = _state.GetVariable(name);
                if (v == null)
                    return false;

                v.Value = value;
                return true;
            }

            public IEnumerator<KeyValuePair<VariableName, Value>> GetEnumerator()
            {
                return _state.Select(a => new KeyValuePair<VariableName, Value>(new VariableName(a.Key), a.Value.Value)).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
