using NUnit.Framework;
using System;
using System.Linq.Expressions;

namespace MathExpressionsNet.Tests
{
	public class CodeDomTests
	{
		private Functions functions = new Functions();
		private Random r = new Random();
		private double NextRandomDouble => r.Next(-10, 10) + r.NextDouble();

		[Test]
		public void StringsToExpressionsTest()
		{
			var f0 = CodeDom.ParseExpression<Func<double>>("2+5*4").Compile();
			Assert.That(f0(), Is.EqualTo(22));

			var f1 = ((Expression<Func<double, double>>)CodeDom.GetExpressionFrom("x=>0")).Compile();
			VerifyFunctions(f1, functions.LeftBoundCond);

			f1 = CodeDom.ParseExpression<Func<double, double>>("t+3*t*t", "t").Compile();
			VerifyFunctions(f1, functions.RightBoundCond);

			var f2 = ((Expression<Func<double, double, double>>)CodeDom.GetExpressionFrom("(x,t)=>x*x*t+3*x*t*t")).Compile();
			VerifyFunctions(f2, functions.u);

			f2 = ((Expression<Func<double, double, double>>)CodeDom.GetExpressionFrom("(x,t)=>2*x*t+3*t*t")).Compile();
			VerifyFunctions(f2, functions.du_dx);

			var f3 = ((Expression<Func<double, double, double, double>>)CodeDom.GetExpressionFrom("(x,t,u)=>x*x*t+u*u")).Compile();
			VerifyFunctions(f3, functions.K);
		}

		private void VerifyFunctions(Func<double, double> generatedFunc, Func<double, double> expectedFunc)
		{
			var a = NextRandomDouble;
			Assert.That(generatedFunc(a), Is.EqualTo(expectedFunc(a)));
		}

		private void VerifyFunctions(Func<double, double, double> generatedFunc, Func<double, double, double> expectedFunc)
		{
			var a = NextRandomDouble;
			var b = NextRandomDouble;
			Assert.That(generatedFunc(a, b), Is.EqualTo(expectedFunc(a, b)));
		}

		private void VerifyFunctions(Func<double, double, double, double> generatedFunc, Func<double, double, double, double> expectedFunc)
		{
			var a = NextRandomDouble;
			var b = NextRandomDouble;
			var c = NextRandomDouble;
			Assert.That(generatedFunc(a, b, c), Is.EqualTo(expectedFunc(a, b, c)));
		}

		[Test]
		public void MakeNewFunction()
		{
			var exprU = CodeDom.ParseExpression<Func<double, double, double>>("x * x * t + 3 * x * t * t", "x", "t");
			var exprK = CodeDom.ParseExpression<Func<double, double, double, double>>("x * x * t + u * u", "x", "t", "u");
			string du_dt = exprU.Derive("t").Simplify().Body.ToString();
			string dK_du = exprK.Derive("u").Simplify().Body.ToString();
			var dudx = exprU.Derive("x").Simplify();
			string du_dx = dudx.Body.ToString();
			string d2u_dx2 = dudx.Derive("x").Simplify().Body.ToString();
			string g = $"{du_dt}-{dK_du}*Math.Pow({du_dx},2)-{d2u_dx2}*{exprK.Simplify().Body}".Replace(" ", "");
			Assert.That(g, Is.EqualTo("((x*x)+(6*(x*t)))-(2*u)*Math.Pow(((2*(x*t))+(3*(t*t))),2)-(2*t)*(((x*x)*t)+(u*u))"));
			var gFunc = CodeDom.ParseExpression<Func<double, double, double, double>>(g, "x", "t", "u").Compile();
			VerifyFunctions(gFunc, functions.g);
		}

		[Test]
		public void ThrowingException()
		{
			var ecde = Assert.Throws<ExpressionCodeDomException>(() => ((Expression<Func<int>>)CodeDom.GetExpressionFrom("x")).Compile());
			Assert.That(ecde.Message, Does.StartWith("Compilation failed: "));
		}
	}
}
