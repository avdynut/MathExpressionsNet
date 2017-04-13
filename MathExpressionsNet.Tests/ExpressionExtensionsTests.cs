using NUnit.Framework;
using System;
using System.Linq.Expressions;

namespace MathExpressionsNet.Tests
{
	public class ExpressionExtensionsTests
	{
		[Test]
		public void NumericDeriveCheck()
		{
			const int NUM = 5000;
			const double EPSILON = 1e-12;

			NumericDeriveCheck(
				"x sin x^2 + e^x / x",
				x => x * Math.Sin(x * x) + Math.Exp(x) / x,
				x => Math.Sin(x * x) + 2 * x * x * Math.Cos(x * x) + Math.Exp(x) / x - Math.Exp(x) / (x * x),
				1, 100, NUM, EPSILON);

			NumericDeriveCheck(
				"sin^2 + cos^2",
				x => x * x * x * x * x * x + Math.Cos(x) * Math.Cos(x) + Math.Sin(x) * Math.Sin(x),
				x => 6 * x * x * x * x * x,
				-500, 500, NUM, EPSILON);

			NumericDeriveCheck(
				"composition",
				x => Math.Exp(Math.Log(Math.Sin(Math.Cos(x * x * x)))),
				x => -3 * x * x * Math.Sin(x * x * x) * Math.Cos(Math.Cos(x * x * x)),
				-500, 500, NUM, EPSILON);

			NumericDeriveCheck(
				"reduce",
				x => x * 2 / x * 3 / x * x * 4 * x / 48 / x * x * x,
				x => x,
				-1000, 1000, NUM, 0);

			NumericDeriveCheck(
				"neg",
				x => -(-x) + (x + (-x * -x) - x * -x) + -2 * (x),
				x => 4 * x,
				-1000, 1000, NUM, 0);

			NumericDeriveCheck(
				"neg2",
				x => 1 + x + (x - 1 - x + 1) - x,
				x => 0,
				-1000, 1000, NUM, 0);
		}

		private void NumericDeriveCheck(
			string debugID,
			Expression<Func<double, double>> f,
			Func<double, double> df,
			double min, double max, int num, double epsilon)
		{
			var df_e = f.Simplify().Derive().Simplify();
			var df_c = df_e.Compile();

#if Verbose
			Console.Write("{0}\n",df_e);
#endif
			NumericCheck(debugID, df_c, df, min, max, num, epsilon);
		}

		public void NumericCheck(
			string debugID,
			Func<double, double> f, Func<double, double> g,
			double min, double max, int num, double epsilon)
		{
			Random rnd = new Random();

			for (; num > 0; --num)
			{
				double x = (max - min) * rnd.NextDouble() + min;

				double fx = f(x);
				double gx = g(x);
				double scale = Math.Abs(fx);
				double delta = Math.Abs(fx - gx);
				if (scale > 1e-7)
					delta /= scale;

				if (delta > epsilon)
				{
					Console.Write("{0} does not match at x = {1}, f({1}) = {2}, g({1}) = {3}, (δ = {4})\n", debugID, x, fx, gx, delta);
				}
			}
		}

		[Test]
		public void TestPartial()
		{
			TestPartial((x, y) => x * x * y + 2 * x * y);
		}

		public void TestPartial(Expression<Func<double, double, double>> f)
		{
			var dx = new DifferentialOperator("x");
			var dy = new DifferentialOperator("y");
			var laplacian = dx * dx + dy * dy;

			Console.Write("f     = {0}\n", f.Simplify());
			Console.Write("df/dx = {0}\n", dx.Apply(f).Simplify());
			Console.Write("df/dy = {0}\n", dy.Apply(f).Simplify());
			Console.Write("Δf   = {0}\n", laplacian.Apply(f));
		}

		[Test]
		public void TestCompile()
		{
			Expression<Func<double, double>> e1 = x => x * Math.Log(x);
			TestCompile(e1);
		}

		public void TestCompile(Expression<Func<double, double>> e)
		{
			Func<double, double> f = e.Compile();
			Console.Write("f = {0}\n", e);

			for (int i = 1; i <= 3; ++i)
			{
				Console.Write("f({0}) = {1}\n", i, f(i));
			}

			Expression<Func<double, double>> de = e.Derive().Simplify();
			Func<double, double> df = de.Compile();
			Console.Write("df = {0}\n", de);

			for (int i = 1; i <= 3; ++i)
			{
				Console.Write("df({0}) = {1}\n", i, df(i));
			}
		}

		[Test]
		public void TestDifferentialOperator()
		{
			//TestDifferentialOperator((x, y) => x * x + y * x + 2 * y * y);
			//TestDifferentialOperator((x, y) => x * x * y + 2 * x * y * y * y);
			TestDifferentialOperator((x, y) => x * Math.Log(y) + y * Math.Exp(x));
		}

		public void TestDifferentialOperator(
			Expression<Func<double, double, double>> f)
		{
			DifferentialOperator dx = new DifferentialOperator("x");
			DifferentialOperator dy = new DifferentialOperator("y");

			Console.Write("f = {0}\n", f);
			Console.Write("df/dx = {0}\n", dx.Apply(f));
			Console.Write("df/dy = {0}\n", dy.Apply(f));

			var laplacian = new DifferentialOperator((x, y) => x * x + y * y);
			Console.Write("Δf = {0}\n", laplacian.Apply(f));

			laplacian = dx * dx + dy * dy;
			Console.Write("Δf = {0}\n", laplacian.Apply(f));

			DifferentialOperator communicator = dx * dy - dy * dx;
			Console.Write("[d/dx, d/dy]f = {0}\n", communicator.Apply(f));
		}

		[Test]
		public void TestPolynomial()
		{
			Expression<Func<double, double>> f = x => 4 * x * -x * x * -x + 5 * -x * x * x + 3 * x * x + 2 * x + 1;

			Console.Write(f);
			Console.Write('\n');

			Console.Write(f.Derive());
			Console.Write('\n');
		}

		[Test]
		public void TestMultiVariable()
		{
			Expression<Func<double, double, double>> f =
				(x, y) => x * x * x + 3 * x * x * y + 2 * x * y * y + y * y * y;

			Console.Write("f = {0}\n", f);
			Console.Write("df/dx = {0}\n", f.Derive("x").Simplify());
			Console.Write("df/dy = {0}\n", f.Derive("y").Simplify());
		}

		[Test]
		public void TestReduce()
		{
			//TestReduce(x => -x + x * 3 / x * 2 / x * 4 * x);
			TestReduce(x => -Math.Cos(x) + Math.Cos(x) * 3 / x * 2 / Math.Cos(x) * 4 * x / 24 * Math.Sin(x));
			TestReduce(
				(x, y) => 3 * Math.Sin(x) + 2 * x + 3 * x * x - x + x * y - x * x - 4 * x * y + Math.Sin(x),
				(x, y) => x + y);
		}

		public void TestReduce(Expression<Func<double, double>> f)
		{
			Console.Write("f           = {0}\n", f);
			Console.Write("f.reduce()  = {0}\n", f.Simplify());
			Console.Write("f'          = {0}\n", f.Derive());
			Console.Write("f'.reduce() = {0}\n", f.Derive().Simplify());
			Console.Write("f.reduce()' = {0}\n", f.Simplify().Derive());
		}

		public void TestReduce(
			Expression<Func<double, double, double>> f,
			Expression<Func<double, double, double>> dc)
		{
			var d = new DifferentialOperator(dc);
			Console.Write("f           = {0}\n", f);
			Console.Write("f.reduce()  = {0}\n", f.Simplify());
			Console.Write("f'          = {0}\n", d.Apply(f));
			Console.Write("f'.reduce() = {0}\n", d.Apply(f).Simplify());
			Console.Write("f.reduce()' = {0}\n", d.Apply(f.Simplify()));
		}

		[Test]
		public void TestIdentical()
		{
			Expression<Func<double, double>> f1 = x => 3 - 2 * x + 1 / x;
			Expression<Func<double, double>> f2 = x => 3 - x * 2 + 1 / x;

			Console.Write("{0}\n", f1.IsIdenticalTo(f2));
			Console.Write("{0}\n", f1.Derive().IsIdenticalTo(f2.Derive()));

			Expression<Func<double, double>> f3 = x => Math.Exp(Math.Sin(x));
			Expression<Func<double, double>> f4 = x => Math.Exp(Math.Cos(x));

			Console.Write("{0}\n", f3.IsIdenticalTo(f4));

			Expression<Func<double, double, double>> f5 = (x, y) => x + 1 + y;
			Expression<Func<double, double, double>> f6 = (x, y) => y + 1 + x;

			Console.Write("{0}\n", f5.IsIdenticalTo(f6));
		}

		[Test]
		public void TestCall()
		{
			ShowDerivative<Func<double, double>>(x => Math.Tan(x) * Math.Cos(x));
			ShowDerivative<Func<double, double>>(x => Math.Cos(Math.Sin(x)));
			ShowDerivative<Func<double, double>>(x => Math.Exp(Math.Sin(x)));
			ShowDerivative<Func<double, double>>(x => Math.Log(Math.Sin(x)));
			ShowDerivative<Func<double, double>>(x => Math.Log(Math.Exp(x)));
		}

		[Test]
		public void TestPower()
		{
			ParameterExpression pX = Expression.Parameter(typeof(double), "x");
			ParameterExpression pY = Expression.Parameter(typeof(double), "y");
			Expression<Func<double, double, double>> le = Expression.Lambda<Func<double, double, double>>(
				Expression.Power(pX, pY),
				pX, pY);
			Func<double, double, double> fle = le.Compile();
			Console.Write(le);
			Console.Write('\n');
			Console.Write(fle(2, 3));
			Console.Write('\n');
		}

		public void ShowDerivative<T>(Expression<T> e)
		{
			Console.Write(" f = {0}\n", e);
			Console.Write("df = {0}\n", e.Derive().Simplify());
		}
	}
}
