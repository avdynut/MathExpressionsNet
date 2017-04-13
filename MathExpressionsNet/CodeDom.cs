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
		const string TYPE_NAME = "TemporaryNamespace.Temporary";
		const string METHOD_NAME = "Get";
		const string CODE_HEADER = @"
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
		const string CODE_FOOTER = @"
				);
		}

		static Expression New(Expression<Func<double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double, double>> e) { return e; }
		static Expression New(Expression<Func<double, double, double, double, double>> e) { return e; }
	}
}
";

		static CompilerResults Compile(string source)
		{
			CodeDomProvider provider = new CSharpCodeProvider(
				new Dictionary<string, string> { { "CompilerVersion", "v3.5" } });

			CompilerParameters cp = new CompilerParameters();
			cp.GenerateInMemory = true;
			cp.ReferencedAssemblies.Add("System.Core.dll");

			CompilerResults cr = provider.CompileAssemblyFromSource(
				cp,
				CODE_HEADER + source + CODE_FOOTER
			);

			return cr;
		}

		static Expression Execute(CompilerResults cr)
		{
			Assembly asm = cr.CompiledAssembly;
			Type myClass = asm.GetType(TYPE_NAME);
			Object o = Activator.CreateInstance(myClass);
			MethodInfo mi = myClass.GetMethod(METHOD_NAME);
			return (Expression)mi.Invoke(o, null);
		}

		public static Expression GetExpressionFrom(string source)
		{
			CompilerResults cr = Compile(source);

			if (cr.Errors.HasErrors)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("Compilation failed: ");
				Regex reg = new Regex(@":\serror\s(?<reason>.*)$");

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
	}

	public class ExpressionCodeDomException : Exception
	{
		public ExpressionCodeDomException(string msg) : base(msg, null) { }
		public ExpressionCodeDomException(string msg, Exception innerException) :
			base(msg, innerException)
		{ }
	}
}
