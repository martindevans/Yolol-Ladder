using Yolol.Analysis.ControlFlowGraph.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;
using YololCompetition.Services.Challenge;

namespace YololCompetition.Services.Verification
{
    internal class ChipLevelChecker
        : Yolol.Analysis.TreeVisitor.BaseStatementVisitor<bool>
    {
        private readonly ChipLevelCheckerExpr _check;

        public ChipLevelChecker(YololChip level)
        {
            _check = new ChipLevelCheckerExpr(level);
        }

        protected override bool Visit(ErrorStatement err)
        {
            return true;
        }

        protected override bool Visit(Conditional con)
        {
            return _check.Visit(con.Condition);
        }

        protected override bool Visit(TypedAssignment ass)
        {
            return _check.Visit(ass.Right);
        }

        protected override bool Visit(EmptyStatement empty)
        {
            return true;
        }

        protected override bool Visit(StatementList list)
        {
            foreach (var stmt in list.Statements)
            {
                if (!Visit(stmt))
                    return false;
            }

            return true;
        }

        protected override bool Visit(CompoundAssignment compAss)
        {
            return _check.Visit(compAss.Right);
        }

        protected override bool Visit(Assignment ass)
        {
            return _check.Visit(ass.Right);
        }

        protected override bool Visit(ExpressionWrapper expr)
        {
            return _check.Visit(expr.Expression);
        }

        protected override bool Visit(Goto @goto)
        {
            return _check.Visit(@goto.Destination);
        }

        protected override bool Visit(If @if)
        {
            if (!_check.Visit(@if.Condition))
                return false;
            if (!Visit(@if.TrueBranch))
                return false;
            if (!Visit(@if.FalseBranch))
                return false;

            return true;
        }
    }

    internal class ChipLevelCheckerExpr
        : Yolol.Analysis.TreeVisitor.BaseExpressionVisitor<bool>
    {
        private readonly YololChip _level;

        public ChipLevelCheckerExpr(YololChip level)
        {
            _level = level;
        }

        protected override bool Visit(Or or) => Visit(or.Left) && Visit(or.Right);

        protected override bool Visit(And and) => Visit(and.Left) && Visit(and.Right);

        protected override bool Visit(Not not) => Visit(not.Parameter);

        protected override bool Visit(ErrorExpression err) => true;

        protected override bool Visit(Increment inc) => true;

        protected override bool Visit(Decrement dec) => true;

        protected override bool Visit(Phi phi) => true;

        protected override bool Visit(LessThanEqualTo eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(LessThan eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(GreaterThanEqualTo eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(GreaterThan eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(NotEqualTo eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(EqualTo eq) => Visit(eq.Left) && Visit(eq.Right);

        protected override bool Visit(Variable var) => true;

        protected override bool Visit(Modulo mod) => Visit(mod.Left) && Visit(mod.Right) && _level >= YololChip.Advanced;

        protected override bool Visit(PreDecrement dec) => true;

        protected override bool Visit(PostDecrement dec) => true;

        protected override bool Visit(PreIncrement inc) => true;

        protected override bool Visit(PostIncrement inc) => true;

        protected override bool Visit(Abs abs) => Visit(abs.Parameter) && _level >= YololChip.Advanced;

        protected override bool Visit(Sqrt sqrt) => Visit(sqrt.Parameter) && _level >= YololChip.Advanced;

        protected override bool Visit(Sine app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(Cosine app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(Tangent app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(ArcSine app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(ArcCos app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(ArcTan app) => Visit(app.Parameter) && _level >= YololChip.Professional;

        protected override bool Visit(Bracketed brk) => Visit(brk.Parameter);

        protected override bool Visit(Add add) => Visit(add.Left) && Visit(add.Right);

        protected override bool Visit(Subtract sub) => Visit(sub.Left) && Visit(sub.Right);

        protected override bool Visit(Multiply mul) => Visit(mul.Left) && Visit(mul.Right);

        protected override bool Visit(Divide div) => Visit(div.Left) && Visit(div.Right);

        protected override bool Visit(Exponent exp) => Visit(exp.Left) && Visit(exp.Right) && _level >= YololChip.Advanced;

        protected override bool Visit(Negate neg) => Visit(neg.Parameter);

        protected override bool Visit(ConstantNumber con) => true;

        protected override bool Visit(ConstantString con) => true;
    }
}
