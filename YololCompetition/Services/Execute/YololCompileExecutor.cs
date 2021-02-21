using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace YololCompetition.Services.Execute
{
    public class YololCompileExecutor
        : IYololExecutor
    {
        private const int MaxStringLength = 25000;

        public IExecutionState Prepare(Yolol.Grammar.AST.Program program, string done)
        {
            var internalsMap = new InternalsMap();
            var externalsMap = new ExternalsMap();
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

            private Value[] _externals;
            private Value[] _internals;

            public bool Done
            {
                get => TryGet(_done)?.ToBool() ?? false;
                set => Set(_done, (Number)value);
            }

            private int _programCounter;
            public int ProgramCounter => _programCounter + 1;

            public ulong TotalLinesExecuted { get; private set; }

            public bool TerminateOnPcOverflow { get; set; }

            public ExecutionState(List<Func<ArraySegment<Value>, ArraySegment<Value>, int>> funcs, Dictionary<string, int> internalsMap, Dictionary<string, int> externalsMap, string done)
            {
                _lines = funcs;
                _internalsMap = internalsMap;
                _externalsMap = externalsMap;
                _done = done;

                _internals = new Value[internalsMap.Count];
                Array.Fill(_internals, new Value((Number)0));
                _externals = new Value[externalsMap.Count];
                Array.Fill(_externals, new Value((Number)0));
            }

            public string? Run(uint lineExecutionLimit, TimeSpan timeout)
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
                    {
                        _programCounter = 0;
                        if (TerminateOnPcOverflow)
                            return null;
                    }

                    // Execution timeout
                    if (timer.Elapsed > timeout)
                        return $"Execution Timed Out (executed {executed} ticks in {timer.Elapsed.TotalMilliseconds}ms)";

                    // Sanity check strings are not getting too long
                    for (var i = 0; i < _internals.Length; i++)
                    {
                        if (_internals[i].Type != Yolol.Execution.Type.String)
                            continue;
                        if (_internals[i].String.Length > MaxStringLength)
                            return "Max String Length Exceeded";
                    }
                    for (var i = 0; i < _externals.Length; i++)
                    {
                        if (_externals[i].Type != Yolol.Execution.Type.String)
                            continue;
                        if (_externals[i].String.Length > MaxStringLength)
                            return "Max String Length Exceeded";
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

            public void Set(string name, Value value)
            {
                var vName = new VariableName(name);
                if (vName.IsExternal)
                {
                    if (!_externalsMap.TryGetValue(vName.Name, out var v))
                    {
                        v = _externals.Length;
                        _externalsMap.Add(vName.Name, v);
                        Array.Resize(ref _externals, _externals.Length + 1);
                    }
                    _externals[v] = value;
                }
                else
                {
                    if (!_internalsMap.TryGetValue(vName.Name, out var v))
                    {
                        v = _internals.Length;
                        _internalsMap.Add(vName.Name, v);
                        Array.Resize(ref _internals, _internals.Length + 1);
                    }
                    _internals[v] = value;
                }
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

            public void CopyTo(IExecutionState other, bool externalsOnly = false)
            {
                if (!externalsOnly)
                    foreach (var (name, index) in _internalsMap)
                        other.Set(name, _internals[index]);

                foreach (var (name, index) in _externalsMap)
                    other.Set(name, _externals[index]);
            }
        }
    }
}
