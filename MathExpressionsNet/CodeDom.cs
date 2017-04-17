using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MathExpressionsNet
{
	public class CodeDom
	{
		private const string TypeName = "TemporaryNamespace.Temporary";
		private const string MethodName = "Get";

		private const string CodeHeader = @"
namespace TemporaryNamespace
{
	using System;
	using System.Linq.Expressions;

	public class Temporary
	{
		public Expression Get()
		{
			return New(
";

		private const string CodeFooter = @"
				);
		}

		static Expression New(Expression<Func<double>> e) { return e; }
		static Expression New(Expression<Func<double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double, double, double>> e) { return e; }
	}
}
";

		private static CompilerResults Compile(string source)
		{
			CodeDomProvider provider = new CSharpCodeProvider(
				new Dictionary<string, string> { { "CompilerVersion", "v3.5" } });

			var cp = new CompilerParameters { GenerateInMemory = true };
			cp.ReferencedAssemblies.Add("System.Core.dll");

			CompilerResults cr = provider.CompileAssemblyFromSource(
				cp,
				CodeHeader + source + CodeFooter
			);

			return cr;
		}

		private static Expression Execute(CompilerResults cr)
		{
			Assembly asm = cr.CompiledAssembly;
			Type myClass = asm.GetType(TypeName);
			Object o = Activator.CreateInstance(myClass);
			MethodInfo mi = myClass.GetMethod(MethodName);
			return (Expression)mi.Invoke(o, null);
		}

		public static Expression GetExpressionFrom(string source)
		{
			CompilerResults cr = Compile(source);

			if (cr.Errors.HasErrors)
			{
				var sb = new StringBuilder();
				sb.Append("Compilation failed: ");
				var reg = new Regex(@":\serror\s(?<reason>.*)$");

				foreach (var error in cr.Errors)
				{
					Match m = reg.Match(error.ToString());
					if (m.Success)
					{
						sb.Append(m.Groups["reason"]);
						sb.Append('\n');
					}
				}
				throw new ExpressionCodeDomException(sb.ToString());
			}

			return Execute(cr);
		}

		public static Expression<T> ParseExpression<T>(string func, params string[] variables)
		{
			var expr = (Expression<T>)GetExpressionFrom($"({string.Join(",", variables)}) => {func}");
			return expr.Simplify();
		}
	}

	public class ExpressionCodeDomException : Exception
	{
		public ExpressionCodeDomException(string msg) : base(msg, null) { }
		public ExpressionCodeDomException(string msg, Exception innerException) :
			base(msg, innerException)
		{ }
	}
}
