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
			var f1 = ((Expression<Func<double, double>>)CodeDom.GetExpressionFrom("x=>0")).Compile();
			VerifyFunctions(f1, functions.LeftBoundCond);

			f1 = ((Expression<Func<double, double>>)CodeDom.GetExpressionFrom("t=>t+3*t*t")).Compile();
			VerifyFunctions(f1, functions.RightBoundCond);

			var f2 = ((Expression<Func<double, double, double>>)CodeDom.GetExpressionFrom("(x,t)=>x*x*t+3*x*t*t")).Compile();
			VerifyFunctions(f2, functions.u);

			f2 = ((Expression<Func<double, double, double>>)CodeDom.GetExpressionFrom("(x,t)=>2*x*t+3*t*t")).Compile();
			VerifyFunctions(f2, functions.du_dx);

			var f3 = ((Expression<Func<double, double, double, double>>)CodeDom.GetExpressionFrom("(x,t,u)=>x*x*t+u*u")).Compile();
			VerifyFunctions(f3, functions.K);

			f3 = ((Expression<Func<double, double, double, double>>)CodeDom.GetExpressionFrom("(x,t,u)=>x*x+6*x*t-2*u*Math.Pow(2*x*t+3*t*t,2)-2*t*(x*x*t+u*u)")).Compile();
			VerifyFunctions(f3, functions.g);
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
		public void ThrowingException()
		{
			var ecde = Assert.Throws<ExpressionCodeDomException>(() => ((Expression<Func<int>>)CodeDom.GetExpressionFrom("x")).Compile());
			Assert.That(ecde.Message, Does.StartWith("Compilation failed: "));
		}
	}
}
