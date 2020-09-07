using System;
using System.Text;
using Yolol.Analysis.TreeVisitor;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;

namespace YololCompetition.Extensions
{
    public static class ProgramExtensions
    {
        public static string PrintAst(this Yolol.Grammar.AST.Program program)
        {
            var b = new StringBuilder();
            var v = new PrintAstVisitor(b);
            v.Visit(program);

            return b.ToString();
        }

        private class PrintAstVisitor
            : BaseTreeVisitor
        {
            private readonly StringBuilder _builder;
            private int _indent;

            public PrintAstVisitor(StringBuilder builder)
            {
                _builder = builder;
            }

            private class Indenter
                : IDisposable
            {
                private readonly PrintAstVisitor _parent;

                public Indenter(PrintAstVisitor parent)
                {
                    _parent = parent;
                }

                public void Dispose()
                {
                    _parent._indent--;
                }
            }

            private IDisposable AppendLine(string txt)
            {
                for (var i = 0; i < _indent; i++)
                    _builder.Append(' ');

                _builder.AppendLine(txt);
                _indent++;

                return new Indenter(this);
            }

            public override Yolol.Grammar.AST.Program Visit(Yolol.Grammar.AST.Program program)
            {
                using (AppendLine("```Program:"))
                    base.Visit(program);
                _builder.AppendLine("```");

                return program;
            }

            protected override BaseExpression Visit(Add add)
            {
                using (AppendLine("+"))
                    return base.Visit(add);
            }

            protected override BaseExpression Visit(And and)
            {
                using (AppendLine("AND"))
                    return base.Visit(and);
            }

            protected override BaseExpression Visit(ConstantNumber con)
            {
                using (AppendLine(con.Value.ToString()))
                    return base.Visit(con);
            }

            protected override BaseExpression Visit(ConstantString con)
            {
                using (AppendLine("\"" + con.Value + "\""))
                    return base.Visit(con);
            }

            protected override BaseExpression Visit(Divide div)
            {
                using (AppendLine("/"))
                    return base.Visit(div);
            }

            protected override BaseExpression Visit(EqualTo eq)
            {
                using (AppendLine("=="))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(Exponent exp)
            {
                using (AppendLine("^"))
                    return base.Visit(exp);
            }

            protected override BaseExpression Visit(GreaterThan eq)
            {
                using (AppendLine(">"))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(GreaterThanEqualTo eq)
            {
                using (AppendLine(">="))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(LessThan eq)
            {
                using (AppendLine("<"))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(LessThanEqualTo eq)
            {
                using (AppendLine("<="))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(Modulo mod)
            {
                using (AppendLine("%"))
                    return base.Visit(mod);
            }

            protected override BaseExpression Visit(Multiply mul)
            {
                using (AppendLine("*"))
                    return base.Visit(mul);
            }

            protected override BaseExpression Visit(NotEqualTo eq)
            {
                using (AppendLine("!="))
                    return base.Visit(eq);
            }

            protected override BaseExpression Visit(Or or)
            {
                using (AppendLine("OR"))
                    return base.Visit(or);
            }

            protected override BaseExpression Visit(Subtract sub)
            {
                using (AppendLine("-"))
                    return base.Visit(sub);
            }

            protected override BaseExpression Visit(Abs app)
            {
                using (AppendLine("ABS"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(ArcCos app)
            {
                using (AppendLine("ACOS"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(ArcSine app)
            {
                using (AppendLine("ASIN"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(ArcTan app)
            {
                using (AppendLine("ATAN"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(Bracketed brk)
            {
                using (AppendLine("()"))
                    return base.Visit(brk);
            }

            protected override BaseExpression Visit(Cosine app)
            {
                using (AppendLine("COS"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(Negate neg)
            {
                using (AppendLine("-"))
                    return base.Visit(neg);
            }

            protected override BaseExpression Visit(Not not)
            {
                using (AppendLine("NOT"))
                    return base.Visit(not);
            }

            protected override BaseExpression Visit(PostDecrement dec)
            {
                using (AppendLine("POSTDEC"))
                    return base.Visit(dec);
            }

            protected override BaseExpression Visit(PostIncrement inc)
            {
                using (AppendLine("POSTINC"))
                    return base.Visit(inc);
            }

            protected override BaseExpression Visit(PreDecrement dec)
            {
                using (AppendLine("PREDEC"))
                    return base.Visit(dec);
            }

            protected override BaseExpression Visit(PreIncrement inc)
            {
                using (AppendLine("PREINC"))
                    return base.Visit(inc);
            }

            protected override BaseExpression Visit(Sine app)
            {
                using (AppendLine("SIN"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(Sqrt app)
            {
                using (AppendLine("SQRT"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(Tangent app)
            {
                using (AppendLine("TAN"))
                    return base.Visit(app);
            }

            protected override BaseExpression Visit(Variable var)
            {
                using (AppendLine("VAR(" + var.Name + ")"))
                    return base.Visit(var);
            }

            public override Line Visit(Line line)
            {
                using (AppendLine("Line"))
                    return base.Visit(line);
            }

            protected override BaseStatement Visit(Assignment ass)
            {
                using (AppendLine($"Assign({ass.Left.Name}) = "))
                    return base.Visit(ass);
            }

            protected override BaseStatement Visit(CompoundAssignment compAss)
            {
                using (AppendLine($"Assign({compAss.Left.Name}) {compAss.Op.String()}= "))
                    return base.Visit(compAss);
            }

            protected override BaseStatement Visit(ExpressionWrapper expr)
            {
                using (AppendLine("Expr"))
                    return base.Visit(expr);
            }

            protected override BaseStatement Visit(Goto @goto)
            {
                using (AppendLine("Goto"))
                    return base.Visit(@goto);
            }

            protected override BaseStatement Visit(If @if)
            {
                using (AppendLine("If"))
                    base.Visit(@if.Condition);

                using (AppendLine("then"))
                    base.Visit(@if.TrueBranch);

                using (AppendLine("else"))
                    base.Visit(@if.FalseBranch);

                return @if;
            }
        }
    }
}
