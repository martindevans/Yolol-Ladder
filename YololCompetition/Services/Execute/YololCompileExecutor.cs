using System;

namespace YololCompetition.Services.Execute
{
    public class YololCompileExecutor
        : IYololExecutor
    {
        public IExecutionState Prepare(Program program1)
        {
            throw new NotImplementedException();
        }

        public IExecutionState Prepare(Yolol.Grammar.AST.Program program)
        {
            throw new NotImplementedException();
        }
    }
}
