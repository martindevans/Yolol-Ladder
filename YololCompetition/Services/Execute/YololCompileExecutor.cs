using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Extensions;

namespace YololCompetition.Services.Execute
{
    public class YololCompileExecutor
        : IYololExecutor
    {
        public IExecutionState Prepare(Yolol.Grammar.AST.Program program, string done)
        {
            var internalsMap = new Dictionary<string, int>();
            var externalsMap = new Dictionary<string, int>();
            var lines = new List<Func<ArraySegment<Value>, ArraySegment<Value>, int>>();
            for (var i = 0; i < program.Lines.Count; i++)
            {
                lines.Add(program.Lines[i].Compile(
                    i + 1,
                    Math.Max(20, program.Lines.Count),
                    internalsMap,
                    externalsMap
                ));
            }

            return new ExecutionState(lines, internalsMap, externalsMap, done);
        }

        private class ExecutionState
            : IExecutionState
        {
            private readonly List<Func<ArraySegment<Value>, ArraySegment<Value>, int>> _lines;
            private readonly Dictionary<string, int> _internalsMap;
            private readonly Dictionary<string, int> _externalsMap;
            private readonly string _done;
            private readonly Value[] _externals;
            private readonly Value[] _internals;

            public bool Done
            {
                get => TryGet(_done)?.ToBool() ?? false;
                set => TrySet(_done, (Number)value);
            }

            private int _programCounter;
            public int ProgramCounter => _programCounter + 1;

            public ulong TotalLinesExecuted { get; private set; }

            public ExecutionState(List<Func<ArraySegment<Value>, ArraySegment<Value>, int>> funcs, Dictionary<string, int> internalsMap, Dictionary<string, int> externalsMap, string done)
            {
                _lines = funcs;
                _internalsMap = internalsMap;
                _externalsMap = externalsMap;
                _done = done;

                _internals = new Value[internalsMap.Count];
                Array.Fill(_internals, new Value(0));
                _externals = new Value[externalsMap.Count];
                Array.Fill(_externals, new Value(0));
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
                        if (_programCounter >= _lines.Count)
                            _programCounter++;
                        else
                            _programCounter = _lines[_programCounter](_internals, _externals) - 1;
                    }
                    catch (ExecutionException)
                    {
                        _programCounter++;
                    }

                    // loop around if program counter goes over max
                    if (_programCounter >= 20)
                        _programCounter = 0;

                    // Execution timeout
                    if (timer.Elapsed > timeout)
                        return "Execution Timed Out!";

                    // Sanity check strings are not getting too long
                    for (var i = 0; i < _internals.Length; i++)
                    {
                        if (_internals[i].Type != Yolol.Execution.Type.String)
                            continue;
                        if (_internals[i].String.Length > 5000)
                            return "Max String Length Exceeded!";
                    }
                    for (var i = 0; i < _externals.Length; i++)
                    {
                        if (_externals[i].Type != Yolol.Execution.Type.String)
                            continue;
                        if (_externals[i].String.Length > 5000)
                            return "Max String Length Exceeded!";
                    }
                }

                return null;
            }

            public Value? TryGet(string name)
            {
                var vName = new VariableName(name);
                if (vName.IsExternal)
                {
                    if (!_externalsMap.TryGetValue(vName.Name, out var v))
                        return null;
                    return _externals[v];
                }
                else
                {
                    if (!_internalsMap.TryGetValue(vName.Name, out var v))
                        return null;
                    return _internals[v];
                }
            }

            public bool TrySet(string name, Value value)
            {
                var vName = new VariableName(name);
                if (vName.IsExternal)
                {
                    if (!_externalsMap.TryGetValue(vName.Name, out var v))
                        return false;
                    _externals[v] = value;
                }
                else
                {
                    if (!_internalsMap.TryGetValue(vName.Name, out var v))
                        return false;
                    _internals[v] = value;
                }

                return true;
            }

            public IEnumerator<KeyValuePair<VariableName, Value>> GetEnumerator()
            {
                foreach (var (key, value) in _internalsMap)
                    yield return new KeyValuePair<VariableName, Value>(new VariableName(key), _internals[value]);
                foreach (var (key, value) in _externalsMap)
                    yield return new KeyValuePair<VariableName, Value>(new VariableName(key), _externals[value]);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
