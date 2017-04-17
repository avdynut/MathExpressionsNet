using System;

namespace MathExpressionsNet.Tests
{
	public class Functions
	{
		public Func<double, double, double, double> K => (x, t, u) => x * x * t + u * u;
		public Func<double, double, double, double> g => (x, t, u) => x * x + 6 * x * t - 2 * u * Math.Pow(2 * x * t + 3 * t * t, 2) - 2 * t * (x * x * t + u * u);
		public Func<double, double> LeftBoundCond => t => 0;
		public Func<double, double> RightBoundCond => t => t + 3 * t * t;
		public Func<double, double, double> u => (x, t) => x * x * t + 3 * x * t * t;
		public Func<double, double, double> du_dx => (x, t) => 2 * x * t + 3 * t * t;
	}
}
