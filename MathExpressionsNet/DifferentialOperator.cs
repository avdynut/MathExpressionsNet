using System;
using System.Linq.Expressions;

namespace MathExpressionsNet
{
	public class DifferentialOperator
	{
		private Expression characteristic;

		#region init

		/// <summary>
		/// construct with parameter name.
		/// (new DiffentialOperator("x")).Apply(f) means f.Derive("x")
		/// </summary>
		/// <param name="paramName">parameter name</param>
		public DifferentialOperator(string paramName)
		{
			characteristic = Expression.Parameter(typeof(double), paramName);
		}

		/// <summary>
		/// construct with characteristic polynomial.
		/// new DiffentialOperator((x, y) => x*x + y*y) means
		/// Laplacian = (∂/∂x)^2 + (∂/∂y)^2
		/// </summary>
		/// <param name="characteristic"></param>
		private DifferentialOperator(Expression characteristic)
		{
			this.characteristic = characteristic;
		}

		/// <summary>
		/// construct with characteristic polynomial.
		/// </summary>
		/// <param name="e">characteristic polynomial</param>
		/// <returns>defferential operator</returns>
		/// <remarks>
		/// This method does not use generic type parameter,
		/// because type inference of Labmda Expression is failed if using with generics.
		/// </remarks>
		public DifferentialOperator(Expression<Func<double, double>> e)
			: this((Expression)e) { }

		public DifferentialOperator(
			Expression<Func<double, double, double>> e)
			: this((Expression)e) { }

		public DifferentialOperator(
			Expression<Func<double, double, double, double>> e)
			: this((Expression)e) { }

		public DifferentialOperator(
			Expression<Func<double, double, double, double, double>> e)
			: this((Expression)e) { }

		#endregion

		#region apply operator

		/// <summary>
		/// apply derivation to Expression e.
		/// </summary>
		/// <typeparam name="T">function type of e</typeparam>
		/// <param name="e">applicant</param>
		/// <returns>derivative</returns>
		public Expression<T> Apply<T>(Expression<T> e)
		{
			return Apply(characteristic, e);
		}

		/// <summary>
		/// apply defferential operator with a characteristic polynomial to Expression e.
		/// </summary>
		/// <typeparam name="T">function type of e</typeparam>
		/// <param name="e">applicant</param>
		/// <returns>derivative</returns>
		static public Expression<T> Apply<T>(Expression characteristic, Expression<T> e)
		{
			switch (characteristic.NodeType)
			{
				case ExpressionType.Constant:
					{
						double c = (double)((ConstantExpression)characteristic).Value;
						return ExpressionExtensions.Mul(c, e);
					}

				case ExpressionType.Parameter:
					{
						string name = ((ParameterExpression)characteristic).Name;
						return e.Derive(name).Simplify();
					}

				case ExpressionType.Add:
					{
						Expression l = ((BinaryExpression)characteristic).Left;
						Expression r = ((BinaryExpression)characteristic).Right;
						return ExpressionExtensions.Add(
							Apply(l, e),
							Apply(r, e));
					}

				case ExpressionType.Subtract:
					{
						Expression l = ((BinaryExpression)characteristic).Left;
						Expression r = ((BinaryExpression)characteristic).Right;
						return ExpressionExtensions.Sub(
							Apply(l, e),
							Apply(r, e));
					}

				case ExpressionType.Multiply:
					{
						Expression l = ((BinaryExpression)characteristic).Left;
						Expression r = ((BinaryExpression)characteristic).Right;

						var er = Apply(r, e);
						return Apply(l, er);
					}

				case ExpressionType.Lambda:
					{
						return Apply(((LambdaExpression)characteristic).Body, e);
					}

				default:
					throw new ExpressionExtensionsException("Functionality not supported");
			}
		}

		#endregion

		#region operator

		/// <summary>
		/// add for differential operator.
		/// </summary>
		/// <param name="o1">operand 1</param>
		/// <param name="o2">opearand 2</param>
		/// <returns>result</returns>
		public static DifferentialOperator operator +(
			DifferentialOperator o1, DifferentialOperator o2)
		{
			return new DifferentialOperator(
				Expression.Add(o1.characteristic, o2.characteristic));
		}

		/// <summary>
		/// subtract for differential operator.
		/// </summary>
		/// <param name="o1">operand 1</param>
		/// <param name="o2">opearand 2</param>
		/// <returns>result</returns>
		public static DifferentialOperator operator -(
			DifferentialOperator o1, DifferentialOperator o2)
		{
			return new DifferentialOperator(
				Expression.Subtract(o1.characteristic, o2.characteristic));
		}

		/// <summary>
		/// multiply for differential operator.
		/// </summary>
		/// <param name="o1">operand 1</param>
		/// <param name="o2">opearand 2</param>
		/// <returns>result</returns>
		public static DifferentialOperator operator *(
			DifferentialOperator o1, DifferentialOperator o2)
		{
			return new DifferentialOperator(
				Expression.Multiply(o1.characteristic, o2.characteristic));
		}

		#endregion
	}
}
