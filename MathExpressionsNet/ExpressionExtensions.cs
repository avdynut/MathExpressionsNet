using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MathExpressionsNet
{
	public static class ExpressionExtensions
	{
		#region inner type

		/// <summary>
		/// express a term as style such like { constant, body }.
		/// 2 * x * 3 * y -> { 6, x * y }.
		/// </summary>
		private class Term
		{
			public double Constant { get; set; }
			public Expression Body { get; set; }

			public Term(double c)
			{
				Constant = c;
				Body = null;
			}

			public Term(Expression b)
			{
				Constant = 1.0;
				Body = b;
			}

			public Term(double c, Expression b)
			{
				Constant = c;
				Body = b;
			}

			public Expression ToExpression()
			{
				if (Constant == 0)
					return Expression.Constant(0.0);
				if (Body == null)
					return Expression.Constant(Constant);
				if (Constant == 1)
					return Body;

				return Expression.Multiply(
					Expression.Constant(Constant),
					Body);
			}
		}

		#endregion

		#region Derive

		/// <summary>
		/// calculate symbolically a total derivative of an Expression e.
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="e">expression to be differentiated</param>
		/// <returns>total derivative of e</returns>
		public static Expression<T> Derive<T>(this Expression<T> e)
		{
			// check not null expression
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");

			// check just one param (variable)
			if (e.Parameters.Count != 1)
				throw new ExpressionExtensionsException("Incorrect number of parameters");

			// check right node type (maybe not necessary)
			if (e.NodeType != ExpressionType.Lambda)
				throw new ExpressionExtensionsException("Functionality not supported");

			// calc derivative
			return Expression.Lambda<T>(
				e.Body.Simplify().Derive(e.Parameters[0].Name).Simplify(),
				e.Parameters);
		}

		/// <summary>
		/// calculate symbolically a total derivative of an Expression e.
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="e">expression to be differentiated</param>
		/// <returns>total derivative of e</returns>
		public static Expression<T> Derive<T>(this Expression<T> e, string paramName)
		{
			// check not null expression
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");

			// check right node type (maybe not necessary)
			if (e.NodeType != ExpressionType.Lambda)
				throw new ExpressionExtensionsException("Functionality not supported");

			// check params (variables)
			if (e.Parameters.Count(p => p.Name == paramName) == 0)
				return Expression.Lambda<T>(Expression.Constant(0.0),
					e.Parameters);

			// calc derivative
			return Expression.Lambda<T>(
				e.Body.Simplify().Derive(paramName).Simplify(),
				e.Parameters);
		}

		/// <summary>
		/// calculate symbolically an partial derivative of an Expression e with respect to paramName.
		/// </summary>
		/// <param name="e">expression to be differentiated</param>
		/// <param name="paramName">parameter name for partial derivation</param>
		/// <returns>partial derivative of e</returns>
		private static Expression Derive(this Expression e, string paramName)
		{
			switch (e.NodeType)
			{
				case ExpressionType.Constant:
					return Expression.Constant(0.0);

				case ExpressionType.Parameter:
					if (((ParameterExpression)e).Name == paramName)
						return Expression.Constant(1.0);
					else
						return Expression.Constant(0.0);

				case ExpressionType.Negate:
					{
						Expression op = ((UnaryExpression)e).Operand;
						Expression d = op.Derive(paramName);
						return OptimizedNegate(d);
					}

				case ExpressionType.Add:
					{
						Expression dleft =
							((BinaryExpression)e).Left.Derive(paramName);
						Expression dright =
							((BinaryExpression)e).Right.Derive(paramName);

						return OptimizedAdd(dleft, dright);
					}

				case ExpressionType.Subtract:
					{
						Expression dleft =
							((BinaryExpression)e).Left.Derive(paramName);
						Expression dright =
							((BinaryExpression)e).Right.Derive(paramName);

						return OptimizedSub(dleft, dright);
					}

				case ExpressionType.Multiply:
					{
						Expression left = ((BinaryExpression)e).Left;
						Expression right = ((BinaryExpression)e).Right;
						Expression dleft = left.Derive(paramName);
						Expression dright = right.Derive(paramName);
						return OptimizedAdd(
							OptimizedMul(left, dright),
							OptimizedMul(dleft, right));
					}

				case ExpressionType.Divide:
					{
						Expression left = ((BinaryExpression)e).Left;
						Expression right = ((BinaryExpression)e).Right;
						Expression dleft = left.Derive(paramName);
						Expression dright = right.Derive(paramName);
						return OptimizedDiv(
							OptimizedSub(
								OptimizedMul(dleft, right),
								OptimizedMul(left, dright)),
							OptimizedMul(right, right));
					}

				case ExpressionType.Call:
					{
						MethodCallExpression me = (MethodCallExpression)e;
						return me.Derive(paramName);
					}

				//! case ExpressionType.Power:
				//! case ExpressionType.Conditional:

				default:
					throw new ExpressionExtensionsException(
						"Not implemented expression type: " + e.NodeType.ToString());
			}
		}

		#region derive for System.Math methods

		/// <summary>
		/// partial derivation for a System.Math method call.
		/// </summary>
		/// <param name="e">expression to be differentiated</param>
		/// <param name="paramName">parameter name for partial derivation</param>
		/// <returns>partial derivative of e</returns>
		private static Expression Derive(this MethodCallExpression me, string paramName)
		{
			MethodInfo mi = me.Method;

			if (!mi.IsStatic || mi.DeclaringType.FullName != "System.Math")
				throw new ExpressionExtensionsException("Not implemented function: " +
														mi.DeclaringType + "/" + mi.Name);

			Expression d = me.Arguments[0].Derive(paramName).Reduce();

			switch (mi.Name)
			{
				case "Sin":
					return OptimizedMul(d, MathCall("Cos", me.Arguments));
				case "Cos":
					return OptimizedMul(d,
						OptimizedNegate(MathCall("Sin", me.Arguments)));
				case "Tan":
					{
						Expression cos = MathCall("Cos", me.Arguments);

						return OptimizedDiv(
							d,
							Expression.Multiply(cos, cos));
					}
				case "Exp":
					return OptimizedMul(d, me);
				case "Log":
					if (me.Arguments.Count != 1)
						throw new ExpressionExtensionsException("Not implemented function: " +
																mi.Name);
					return OptimizedDiv(d, me.Arguments[0]);

				//! so far, log_x(y) is not supported,
				//  but, log_x(y) could be supported as log_x(y) = log(y)/log(x)

				case "Pow":
					{
						Expression dx = me.Arguments[0].Derive(paramName).Reduce();
						Expression dy = me.Arguments[1].Derive(paramName).Reduce();

						// a^f(x) (a does not contain x)
						if (dx.IsZero())
						{
							return OptimizedMul(
								OptimizedMul(
									MathCall("Log", me.Arguments[0]),
									d), me);
						}
						// f(x)^a (a does not contain x)
						if (dy.IsZero())
						{
							return OptimizedMul(
								d,
								MathCall("Pow", me.Arguments[0],
									OptimizedSub(me.Arguments[1], Expression.Constant(1.0))));
						}

						throw new ExpressionExtensionsException("Not implemented function: " +
																mi.Name);
						//! so far, f(x)^g(x) is not supported.
						// Its derivative is so complex.
					}

				default:
					throw new ExpressionExtensionsException("Not implemented function: " +
															mi.Name);
			}
		}

		/// <summary>
		/// create an expression which contains System.Math method call.
		/// </summary>
		/// <param name="methodName">method name</param>
		/// <param name="arguments">arguments of the method</param>
		/// <returns>expression</returns>
		private static Expression MathCall(string methodName, IEnumerable<Expression> arguments)
		{
			return Expression.Call(null, typeof(Math).GetMethod(methodName), arguments);
		}

		private static Expression MathCall(string methodName, params Expression[] arguments)
		{
			return MathCall(methodName, arguments);
		}

		#endregion

		#endregion

		#region optimized arithmetic

		/// <summary>
		/// negate an expressions with optimization such as -(-x) -> x.
		/// </summary>
		/// <param name="e">operand</param>
		/// <returns>result</returns>
		internal static Expression OptimizedNegate(Expression e)
		{
			if (e.IsConstant())
			{
				return Expression.Constant(-(double)((ConstantExpression)e).Value);
			}

			if (e.NodeType == ExpressionType.Negate)
			{
				return ((UnaryExpression)e).Operand;
			}

			Term t = FoldConstants(e);
			t.Constant = -t.Constant;
			return t.ToExpression();
		}

		/// <summary>
		/// add two expressions with optimization such as x + 0 -> x.
		/// </summary>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		private static Expression OptimizedAdd(Expression e1, Expression e2)
		{
			// 0 + x -> x
			if (e1.IsConstant())
			{
				double x = (double)((ConstantExpression)e1).Value;
				if (x == 0)
					return e2;

				// constant + constant
				if (e2.IsConstant())
				{
					double y = (double)((ConstantExpression)e2).Value;
					return Expression.Constant(x + y);
				}
			}

			// x + 0 -> x
			if (e2.IsConstant())
			{
				double x = (double)((ConstantExpression)e2).Value;
				if (x == 0)
					return e1;
			}

			// x + x -> 2 * x
			if (e1.IsIdenticalTo(e2))
			{
				return OptimizedMul(
					Expression.Constant(2.0),
					e1);
			}

			// a x + b x -> (a + b) x
			Term t1 = FoldConstants(e1);
			Term t2 = FoldConstants(e2);

			if (t1.Body.IsIdenticalTo(t2.Body))
			{
				return Expression.Multiply(
					Expression.Constant(t1.Constant + t2.Constant),
					t1.Body);
			}

			// otherwise
			return Expression.Add(
				t1.ToExpression(),
				t2.ToExpression());
		}

		/// <summary>
		/// subtract two expressions with optimization such as x - 0 -> x.
		/// </summary>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		private static Expression OptimizedSub(Expression e1, Expression e2)
		{
			// 0 - x -> -x
			if (e1.IsConstant())
			{
				double x = (double)((ConstantExpression)e1).Value;
				if (x == 0)
					return OptimizedNegate(e2);

				// constant - constant
				if (e2.IsConstant())
				{
					double y = (double)((ConstantExpression)e2).Value;
					return Expression.Constant(x - y);
				}
			}

			// x - 0 -> x
			if (e2.IsConstant())
			{
				double x = (double)((ConstantExpression)e2).Value;
				if (x == 0)
					return e1;
			}

			// x - x -> 0
			if (e1.IsIdenticalTo(e2))
			{
				return Expression.Constant(0.0);
			}

			// a x - b x -> (a - b) x
			Term t1 = FoldConstants(e1);
			Term t2 = FoldConstants(e2);

			if (t1.Body.IsIdenticalTo(t2.Body))
			{
				return Expression.Multiply(
					Expression.Constant(t1.Constant - t2.Constant),
					t1.Body);
			}

			// otherwise
			return Expression.Subtract(
				t1.ToExpression(),
				t2.ToExpression());
		}

		/// <summary>
		/// multiply two expressions with optimization such as x * 3 * x * 2 -> 6 * x * x.
		/// </summary>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		private static Expression OptimizedMul(Expression e1, Expression e2)
		{
			Expression mul = Expression.Multiply(e1, e2);

			Term t = FoldConstants(mul);
			return t.ToExpression();
		}

		/// <summary>
		/// multiply two expressions with optimization such as x / x -> 1.
		/// </summary>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		private static Expression OptimizedDiv(Expression e1, Expression e2)
		{
			Expression div = Expression.Divide(e1, e2);

			Term t = FoldConstants(div);
			return t.ToExpression();
		}

		#endregion
		#region public interface for optimized arithmetic

		/// <summary>
		/// public interface for Optimized Add.
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		public static Expression<T> Add<T>(Expression<T> e1, Expression<T> e2)
		{
			if (!e1.Parameters.IsIdenticalTo(e2.Parameters))
				throw new ExpressionExtensionsException("Incorrect parameters");

			return Expression.Lambda<T>(
				OptimizedAdd(e1.Body, e2.Body),
				e1.Parameters);
		}

		/// <summary>
		/// public interface for Optimized Subtract.
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>result</returns>
		public static Expression<T> Sub<T>(Expression<T> e1, Expression<T> e2)
		{
			if (!e1.Parameters.IsIdenticalTo(e2.Parameters))
				throw new ExpressionExtensionsException("Incorrect parameters");

			return Expression.Lambda<T>(
				OptimizedSub(e1.Body, e2.Body),
				e1.Parameters);
		}

		/// <summary>
		/// public interface for Optimized Multiply.
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="c">operand 1 (constant factor)</param>
		/// <param name="e">operand 2</param>
		/// <returns>result</returns>
		public static Expression<T> Mul<T>(double c, Expression<T> e)
		{
			return Expression.Lambda<T>(
				OptimizedMul(Expression.Constant(c), e),
				e.Parameters);
		}

		#endregion
		#region simplyfy

		/// <summary>
		/// simplify an Expression
		/// by reducing a common denominator and etc..
		/// </summary>
		/// <typeparam name="T">type of Function</typeparam>
		/// <param name="e">expression to be reduced</param>
		/// <returns>reduced result</returns>
		public static Expression<T> Simplify<T>(this Expression<T> e)
		{
			// check not null expression
			if (e == null)
				throw new ExpressionExtensionsException("Expression must be non-null");

			// check right node type (maybe not necessary)
			if (e.NodeType != ExpressionType.Lambda)
				throw new ExpressionExtensionsException("Functionality not supported");

			// reduce
			return Expression.Lambda<T>(
				e.Body.Simplify(),
				e.Parameters);

			//! I'm thinking about implementing spetial optimization such as follows:
			// Exp(Log(x)) -> x
			// Log(Exp(x)) -> x
			// Tan(x) * Cos(x) -> Sin(x)
		}

		/// <summary>
		/// simplify an Expression
		/// by reducing a common denominator and etc..
		/// </summary>
		/// <param name="e">expression to be reduced</param>
		/// <returns>reduced result</returns>
		private static Expression Simplify(this Expression e)
		{
			switch (e.NodeType)
			{
				case ExpressionType.Add:
				case ExpressionType.Subtract:
				case ExpressionType.Negate:
					return e.Cancel();

				case ExpressionType.Multiply:
				case ExpressionType.Divide:
					return e.Reduce();

				case ExpressionType.Call:
					{
						MethodCallExpression me = (MethodCallExpression)e;
						List<Expression> args = new List<Expression>();

						foreach (var arg in me.Arguments)
						{
							args.Add(arg.Simplify());
						}

						return Expression.Call(null, me.Method, args);
					}

				default:
					return e;
			}
		}

		#endregion
		#region cancel common terms

		/// <summary>
		/// cancel common terms in sum.
		/// </summary>
		/// <param name="e">terget</param>
		/// <returns>result</returns>
		private static Expression Cancel(this Expression e)
		{
			List<Term> terms = new List<Term>();

			DeconstructSum(e, false, terms);
			return ConstructSum(terms);
		}

		/// <summary>
		/// deconstruct sum into list.
		/// 4 * x + y - 2 * x -> { {2, x}, {1, y} }.
		/// </summary>
		/// <param name="e">expression to be deconstructed</param>
		/// <param name="minus">negate sign of e if minus == true</param>
		/// <param name="terms">list into which deconstructed terms are stored</param>
		private static void DeconstructSum(Expression e, bool minus, List<Term> terms)
		{
			if (e.NodeType == ExpressionType.Negate)
			{
				DeconstructSum(
					((UnaryExpression)e).Operand, !minus, terms);
				return;
			}
			if (e.NodeType == ExpressionType.Add)
			{
				Expression l = ((BinaryExpression)e).Left;
				Expression r = ((BinaryExpression)e).Right;
				DeconstructSum(l, minus, terms);
				DeconstructSum(r, minus, terms);
				return;
			}
			if (e.NodeType == ExpressionType.Subtract)
			{
				Expression l = ((BinaryExpression)e).Left;
				Expression r = ((BinaryExpression)e).Right;
				DeconstructSum(l, minus, terms);
				DeconstructSum(r, !minus, terms);
				return;
			}

			Term t = FoldConstants(e);
			if (minus)
				t.Constant = -t.Constant;

			int i = terms.FindIndex(t1 => t.Body.IsIdenticalTo(t1.Body));
			if (i < 0)
				terms.Add(t);
			else
				terms[i].Constant += t.Constant;
		}

		/// <summary>
		/// construct sum from term list.
		/// { { 1, x }, { 2, y }, { 3, z } } -> x + 2 * y + 3 * z.
		/// </summary>
		/// <param name="terms">list in which expressions are stored</param>
		/// <returns>sum of terms</returns>
		private static Expression ConstructSum(List<Term> terms)
		{
			Expression sum = Expression.Constant(0.0);
			foreach (var term in terms)
			{
				sum = OptimizedAdd(sum, term.ToExpression());
			}
			return sum;
		}

		#endregion
		#region reduce common denominator

		/// <summary>
		/// optimize a term by folding constants.
		/// for example, 2 * x * 3 * x * 4 -> 24 * x * x.
		/// </summary>
		/// <param name="e">Expression to be optimized</param>
		private static Term FoldConstants(Expression e)
		{
			List<Expression> n = new List<Expression>();
			List<Expression> d = new List<Expression>();
			DeconstructProduct(e, n, d);
			return ConstructProduct(n, d);
		}

		/// <summary>
		/// reduce a common denominator.
		/// </summary>
		/// <param name="e">terget</param>
		/// <returns>result</returns>
		private static Expression Reduce(this Expression e)
		{
			return FoldConstants(e).ToExpression();
		}

		/// <summary>
		/// deconstruct product into list.
		/// x / a * y * z / b / c -> num = {x, y, z}, denom = {a, b, c}.
		/// </summary>
		/// <param name="e">expression to be deconstructed</param>
		/// <param name="num">list into which deconstructed expressions are stored</param>
		private static void DeconstructProduct(Expression e, List<Expression> num, List<Expression> denom)
		{
			if (e.NodeType == ExpressionType.Multiply)
			{
				Expression left = ((BinaryExpression)e).Left;
				Expression right = ((BinaryExpression)e).Right;

				DeconstructProduct(left, num, denom);
				DeconstructProduct(right, num, denom);
				return;
			}

			if (e.NodeType == ExpressionType.Divide)
			{
				Expression left = ((BinaryExpression)e).Left;
				Expression right = ((BinaryExpression)e).Right;

				DeconstructProduct(left, num, denom);
				DeconstructProduct(right, denom, num);
				return;
			}

			Expression simplified = e.Simplify();

			// result of e.Simplify() could be a form of
			// a * x, a / x or a * x / y where a is constant.
			if (simplified.NodeType == ExpressionType.Multiply)
			{
				Expression left = ((BinaryExpression)simplified).Left;
				Expression right = ((BinaryExpression)simplified).Right;
				num.Add(left);
				if (right.NodeType == ExpressionType.Divide)
				{
					left = ((BinaryExpression)right).Left;
					right = ((BinaryExpression)right).Right;
					num.Add(left);
					denom.Add(right);
				}
				else
					num.Add(right);
			}
			else if (simplified.NodeType == ExpressionType.Divide)
			{
				Expression left = ((BinaryExpression)simplified).Left;
				Expression right = ((BinaryExpression)simplified).Right;
				num.Add(left);
				denom.Add(right);
			}
			else
				num.Add(simplified);
		}

		/// <summary>
		/// construct product from list.
		/// {x, y, z} -> x * y * z.
		/// </summary>
		/// <param name="list">list in which expressions are stored</param>
		private static Term ConstructProduct(IEnumerable<Expression> list)
		{
			double c = 1;
			Expression prod = null;
			foreach (var e in list)
			{
				if (e == null) continue;

				if (e.IsConstant())
					c *= (double)((ConstantExpression)e).Value;
				else if (prod == null)
					prod = e;
				else
					prod = Expression.Multiply(prod, e);
			}
			return new Term(c, prod);
		}

		/// <summary>
		/// construct fraction from list.
		/// num = {x, y, z}, denom = {a, b, c} -> (x * y * z) / (a * b * c).
		/// </summary>
		/// <param name="num">list in which expressions of numerator are stored</param>
		/// <param name="denom">list in which expressions of denominator are stored</param>
		private static Term ConstructProduct(List<Expression> num, List<Expression> denom)
		{
			double c = 1;

			for (int i = 0; i < num.Count; ++i)
			{
				if (num[i] == null) continue;

				// fold constant
				if (num[i].IsConstant())
				{
					c *= (double)((ConstantExpression)num[i]).Value;
					num[i] = null;
				}

				for (int j = 0; j < denom.Count; ++j)
				{
					if (denom[j] == null) continue;

					// fold constant
					if (denom[j].IsConstant())
					{
						c /= (double)((ConstantExpression)denom[j]).Value;
						denom[j] = null;
					}

					// reduce common denominator
					if (num[i].IsIdenticalTo(denom[j]))
					{
						num[i] = null;
						denom[j] = null;
					}
				}
			}

			Term n = ConstructProduct(num);
			Term d = ConstructProduct(denom);
			n.Constant *= c;

			return Div(n, d);
		}

		/// <summary>
		/// divide terms with optimization such as x / x -> 1.
		/// </summary>
		/// <param name="t1">operand 1</param>
		/// <param name="t2">operand 2</param>
		/// <returns>result</returns>
		private static Term Div(Term t1, Term t2)
		{
			double c1 = t1.Constant;
			double c2 = t2.Constant;
			Expression b1 = t1.Body;
			Expression b2 = t2.Body;

			if (c1 == 0)
				return new Term(0);

			double c = c1 / c2;

			if (b1 == null)
			{
				if (b2 == null)
					return new Term(c);
				return new Term(Expression.Divide(Expression.Constant(c), b2));
			}
			if (b2 == null)
				return new Term(c, b1);

			if (b1.IsIdenticalTo(b2))
			{
				return new Term(c);
			}

			return new Term(c,
				Expression.Divide(b1, b2));
		}

		#endregion
		#region identical

		/// <summary>
		/// check whether e1 is identical to e2 or not.
		/// </summary>
		/// <param name="e1">operand 1</param>
		/// <param name="e2">operand 2</param>
		/// <returns>true if identical</returns>
		/// <remarks>
		/// This method is not enough to check the identity completelly.
		/// The identity check could be failed when e1 and e2 are complex.
		/// For instance, so far, even in the case that
		/// e1 = x + 1 + y and e2 = y + 1 + x,
		/// the check is failed.
		/// </remarks>
		public static bool IsIdenticalTo(this Expression e1, Expression e2)
		{
			if (e1 == null)
				return (e2 == null);
			else if (e2 == null)
				return false;

			if (e1.NodeType != e2.NodeType)
				return false;

			switch (e1.NodeType)
			{
				case ExpressionType.Lambda:
					{
						LambdaExpression le1 = (LambdaExpression)e1;
						LambdaExpression le2 = (LambdaExpression)e2;
						if (!le1.Parameters.IsIdenticalTo(le2.Parameters))
							return false;

						return le1.Body.IsIdenticalTo(le2.Body);
					}
				case ExpressionType.Parameter:
					{
						string n1 = ((ParameterExpression)e1).Name;
						string n2 = ((ParameterExpression)e2).Name;
						return n1.Equals(n2);
					}
				case ExpressionType.Constant:
					{
						object o1 = ((ConstantExpression)e1).Value;
						object o2 = ((ConstantExpression)e2).Value;
						return o1.Equals(o2);
					}
				case ExpressionType.Negate:
					{
						Expression o1 = ((UnaryExpression)e1).Operand;
						Expression o2 = ((UnaryExpression)e2).Operand;
						return o1.IsIdenticalTo(o2);
					}
				case ExpressionType.Add:
				case ExpressionType.Multiply:
					{
						Expression o1l = ((BinaryExpression)e1).Left;
						Expression o1r = ((BinaryExpression)e1).Right;
						Expression o2l = ((BinaryExpression)e2).Left;
						Expression o2r = ((BinaryExpression)e2).Right;

						return
							(o1l.IsIdenticalTo(o2l) && o1r.IsIdenticalTo(o2r))
							||
							(o1l.IsIdenticalTo(o2r) && o1r.IsIdenticalTo(o2l))
							;
					}
				case ExpressionType.Subtract:
				case ExpressionType.Divide:
					{
						Expression o1l = ((BinaryExpression)e1).Left;
						Expression o1r = ((BinaryExpression)e1).Right;
						Expression o2l = ((BinaryExpression)e2).Left;
						Expression o2r = ((BinaryExpression)e2).Right;

						return (o1l.IsIdenticalTo(o2l) && o1r.IsIdenticalTo(o2r));
					}
				case ExpressionType.Call:
					{
						MethodCallExpression me1 = (MethodCallExpression)e1;
						MethodCallExpression me2 = (MethodCallExpression)e2;

						if (me1.Arguments.Count != me2.Arguments.Count)
							return false;

						for (int i = 0; i < me1.Arguments.Count; ++i)
							if (!me1.Arguments[i].IsIdenticalTo(me2.Arguments[i]))
								return false;

						MethodInfo mi1 = me1.Method;
						MethodInfo mi2 = me2.Method;

						if (!mi1.IsStatic || mi1.DeclaringType.FullName != "System.Math"
							|| !mi2.IsStatic || mi2.DeclaringType.FullName != "System.Math"
						)
							return false;

						if (mi1.Name != mi2.Name)
							return false;

						return me1.Arguments.IsIdenticalTo(me2.Arguments);
					}
			}

			return false;
		}

		/// <summary>
		/// check if e == 0
		/// </summary>
		/// <param name="e">operand</param>
		/// <returns>true if 0</returns>
		public static bool IsZero(this Expression e)
		{
			return e.IsIdenticalTo(Expression.Constant(0.0));
		}

		/// <summary>
		/// check if e is a constant of Double.
		/// </summary>
		/// <param name="e">operand</param>
		/// <returns>true if e is a constant</returns>
		private static bool IsConstant(this Expression e)
		{
			return e.NodeType == ExpressionType.Constant
				   && e.Type.Name == "Double";
		}

		#endregion
		#region identical for parameters/arguments

		private static bool IsIdenticalTo(
			this ICollection<ParameterExpression> args1,
			ICollection<ParameterExpression> args2)
		{
			if (args1.Count != args2.Count) return false;

			var enum1 = args1.GetEnumerator();
			var enum2 = args2.GetEnumerator();

			while (enum1.MoveNext() && enum2.MoveNext())
			{
				if (enum1.Current.Name != enum2.Current.Name)
					return false;
			}

			return true;
		}

		private static bool IsIdenticalTo(
			this ICollection<Expression> args1,
			ICollection<Expression> args2)
		{
			if (args1.Count != args2.Count) return false;

			var enum1 = args1.GetEnumerator();
			var enum2 = args2.GetEnumerator();

			while (enum1.MoveNext() && enum2.MoveNext())
			{
				if (!enum1.Current.IsIdenticalTo(enum2.Current))
					return false;
			}

			return true;
		}

		#endregion
	}

	public class ExpressionExtensionsException : Exception
	{
		public ExpressionExtensionsException(string msg) : base(msg, null) { }
		public ExpressionExtensionsException(string msg, Exception innerException) :
			base(msg, innerException)
		{ }
	}
}
