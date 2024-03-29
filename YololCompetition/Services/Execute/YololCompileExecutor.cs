﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace YololCompetition.Services.Execute
{
    public class YololCompileExecutor
        : IYololExecutor
    {
        private const int MaxStringLength = 1024;

        public async Task<IEnumerable<IExecutionState>> Prepare(IEnumerable<Yolol.Grammar.AST.Program> programs, string done = ":done")
        {
            var externalsMap = new ExternalsMap();

            // Compile all programs first
            var compiled = (from program in programs
                let c = program.Compile(externalsMap, Math.Max(20, program.Lines.Count), MaxStringLength, null, true)
                select (c, program)
            ).ToList();

            // Create array to hold externals (common to all contexts)
            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, new Value((Number)0));

            // Build an execution state for all programs
            return from item in compiled
                   select new ExecutionState(item.c, item.program, externalsMap, done, externals);
        }

        private class ExecutionState
            : IExecutionState
        {
            private readonly CompiledProgram _program;
            private readonly Yolol.Grammar.AST.Program _ast;
            private readonly ExternalsMap _externalsMap;
            private readonly VariableName _done;

            private readonly Value[] _externals;
            private readonly Value[] _internals;

            public string Code => _ast.ToString();

            public bool Done
            {
                get => TryGet(_done)?.ToBool() ?? false;
                set => Set(_done, (Number)value);
            }

            public int ProgramCounter => _program.ProgramCounter;

            public ulong TotalLinesExecuted { get; private set; }

            public bool TerminateOnPcOverflow { get; set; }

            public ExecutionState(CompiledProgram program, Yolol.Grammar.AST.Program ast, ExternalsMap externalsMap, string done, Value[] externals)
            {
                _program = program;
                _ast = ast;
                _externalsMap = externalsMap;
                _done = new VariableName(done);

                _internals = new Value[_program.InternalsMap.Count];
                Array.Fill(_internals, new Value((Number)0));
                
                if (externals.Length != externalsMap.Count)
                    throw new ArgumentException("Incorrect number of externals", nameof(externals));
                _externals = externals;
            }

            public async Task<string?> Run(uint lineExecutionLimit, TimeSpan timeout)
            {
                var timer = new Stopwatch();
                timer.Start();

                // Run lines in chunks of 10k, checking the timeout between each chunk
                var limit = Math.Min(1000, (int)lineExecutionLimit);
                var doneKey = _externalsMap.ChangeSetKey(new VariableName(":done"));

                // Run lines until completion indicator is set or execution time limit is exceeded
                var executed = 0;
                try
                {
                    while (!Done && executed < lineExecutionLimit - 1)
                    {
                        executed += _program.Run(_internals, _externals, Math.Min(limit, (int)(lineExecutionLimit - executed)), doneKey);

                        // Execution timeout
                        if (timer.Elapsed > timeout)
                            return $"Execution Timed Out (executed {executed} ticks in {timer.Elapsed.TotalMilliseconds}ms)";
                    }
                }
                finally
                {
                    TotalLinesExecuted += (ulong)executed;
                }

                return null;
            }

            public Value? TryGet(VariableName vName)
            {
                if (vName.IsExternal)
                {
                    if (!_externalsMap.TryGetValue(vName, out var v))
                        return null;
                    return _externals[v];
                }
                else
                {
                    if (!_program.InternalsMap.TryGetValue(vName, out var v))
                        return null;
                    return _internals[v];
                }
            }

            public void Set(VariableName vName, Value value)
            {
                if (vName.IsExternal)
                {
                    if (!_externalsMap.TryGetValue(vName, out var v))
                        return;
                    _externals[v] = value;
                }
                else
                {
                    if (!_program.InternalsMap.TryGetValue(vName, out var v))
                        return;
                    _internals[v] = value;
                }
            }

            public IEnumerator<KeyValuePair<VariableName, Value>> GetEnumerator()
            {
                foreach (var (key, value) in _program.InternalsMap)
                    yield return new KeyValuePair<VariableName, Value>(key, _internals[value]);
                foreach (var (key, value) in _externalsMap)
                    yield return new KeyValuePair<VariableName, Value>(key, _externals[value]);
            }
        }
    }
}
