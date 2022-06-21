using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Maths.Color
{
	public static class Temperature
	{
		const double kTintScale = -3000.0;

		struct ruvt
		{
			public ruvt(double r, double u, double v, double t)
            {
				this.r = r;
				this.u = u;
				this.v = v;
				this.t = t;
            }

			public double r;
			public double u;
			public double v;
			public double t;
		};

		static readonly ruvt[] kTempTable =
		{
			new ruvt(   0, 0.18006, 0.26352, -0.24341 ),
			new ruvt(  10, 0.18066, 0.26589, -0.25479 ),
			new ruvt(  20, 0.18133, 0.26846, -0.26876 ),
			new ruvt(  30, 0.18208, 0.27119, -0.28539 ),
			new ruvt(  40, 0.18293, 0.27407, -0.30470 ),
			new ruvt(  50, 0.18388, 0.27709, -0.32675 ),
			new ruvt(  60, 0.18494, 0.28021, -0.35156 ),
			new ruvt(  70, 0.18611, 0.28342, -0.37915 ),
			new ruvt(  80, 0.18740, 0.28668, -0.40955 ),
			new ruvt(  90, 0.18880, 0.28997, -0.44278 ),
			new ruvt( 100, 0.19032, 0.29326, -0.47888 ),
			new ruvt( 125, 0.19462, 0.30141, -0.58204 ),
			new ruvt( 150, 0.19962, 0.30921, -0.70471 ),
			new ruvt( 175, 0.20525, 0.31647, -0.84901 ),
			new ruvt( 200, 0.21142, 0.32312, -1.0182),
			new ruvt( 225, 0.21807, 0.32909, -1.2168),
			new ruvt( 250, 0.22511, 0.33439, -1.4512),
			new ruvt( 275, 0.23247, 0.33904, -1.7298),
			new ruvt( 300, 0.24010, 0.34308, -2.0637),
			new ruvt( 325, 0.24702, 0.34655, -2.4681),
			new ruvt( 350, 0.25591, 0.34951, -2.9641),
			new ruvt( 375, 0.26400, 0.35200, -3.5814),
			new ruvt( 400, 0.27218, 0.35407, -4.3633),
			new ruvt( 425, 0.28039, 0.35577, -5.3762),
			new ruvt( 450, 0.28863, 0.35714, -6.7262),
			new ruvt( 475, 0.29685, 0.35823, -8.5955),
			new ruvt( 500, 0.30505, 0.35907, -11.324),
			new ruvt( 525, 0.31320, 0.35968, -15.628),
			new ruvt( 550, 0.32129, 0.36011, -23.325),
			new ruvt( 575, 0.32931, 0.36038, -40.770),
			new ruvt( 600, 0.33724, 0.36051, -116.4),
		};
		public static Vector3 ChromaticityXYtoXYZ(Vector2 chromaticityXY)
		{
			// Restrict xy coord to someplace inside the range of real xy coordinates.
			// This prevents math from doing strange things when users specify
			// extreme temperature/tint coordinates.
			chromaticityXY.X = Math.Clamp(chromaticityXY.X, 0.000001f, 0.999999f);
			chromaticityXY.Y = Math.Clamp(chromaticityXY.Y, 0.000001f, 0.999999f);

			var Sum = chromaticityXY.X + chromaticityXY.Y;

			if (Sum > 0.999999f)
			{
				var Scale = 0.999999f / Sum;
				chromaticityXY.X *= Scale;
				chromaticityXY.Y *= Scale;
			}

			return new Vector3(chromaticityXY.X / chromaticityXY.Y, 1.0f, (1.0f - chromaticityXY.X - chromaticityXY.Y) / chromaticityXY.Y);
		}

		public static Vector2 D50ChromaticityXY()
		{
			return new Vector2(0.3457f, 0.3585f);
		}

		public static Vector3 D50ChromaticityXYZ()
		{
			return ChromaticityXYtoXYZ(D50ChromaticityXY());
		}

		public static Vector2 D55ChromaticityXY()
		{
			return new Vector2(0.3324f, 0.3474f);
		}

		public static Vector3 D65ChromaticityXYZ()
		{
			return new Vector3(0.31271f, 0.32902f, 0.35827f);
		}

		public static Vector2 PCStoXY()
		{
			return D50ChromaticityXY();
		}

		public static Vector3 PCStoXYZ()
		{
			return ChromaticityXYtoXYZ(PCStoXY());
		}

		public static Vector2 XYZtoChromaticityXY(Vector3 coord)
		{
			var X = coord[0];
			var Y = coord[1];
			var Z = coord[2];

			var total = X + Y + Z;

			if (total > 0.0)
				return new Vector2(X / total, Y / total);

			return D50ChromaticityXY();
		}

		public static uint ChromaticityToColourTemperature(Vector2 chromaticity)
		{
			const float x_e = 0.3366f;
			const float y_e = 0.1735f;
			const float A0 = -949.86315f;
			const float A1 = 6253.80338f;
			const float t1 = 0.92159f;
			const float A2 = 28.70599f;
			const float t2 = 0.20039f;
			const float A3 = 0.00004f;
			const float t3 = 0.07125f;

			float n = (chromaticity.X - x_e) / (chromaticity.Y - y_e);

			double CCT = A0 + A1 * Math.Exp(-n / t1) + A2 * Math.Exp(-n / t2) + A3 * Math.Exp(-n / t3);
			Debug.Assert(CCT > 3000 && CCT < 50000, "ChromaticityToColourTemperature Conversion only accurate within 3000 to 50000K");

			return (uint)Math.Floor(CCT + 0.5f);
		}

		public static Tuple<double,double> ChromaticityToTemperatureTint(Vector2 chromaticityXY)
		{
			ValueTuple<double, double> TemperatureTint = new ValueTuple<double, double>();

			// Convert to uv space.
			double u = 2.0 * chromaticityXY.X / (1.5 - chromaticityXY.X + 6.0 * chromaticityXY.Y);
			double v = 3.0 * chromaticityXY.Y / (1.5 - chromaticityXY.X + 6.0 * chromaticityXY.Y);

			// Search for line pair coordinate is between.

			double last_dt = 0.0;

			double last_dv = 0.0;
			double last_du = 0.0;

			for (uint index = 1; index <= 30; index++)
			{

				// Convert slope to delta-u and delta-v, with length 1.

				double du = 1.0;
				double dv = kTempTable[index].t;

				double len = Math.Sqrt(1.0 + dv * dv);

				du /= len;
				dv /= len;

				// Find delta from black body point to test coordinate.

				double uu = u - kTempTable[index].u;
				double vv = v - kTempTable[index].v;

				// Find distance above or below line.

				double dt = -uu * dv + vv * du;

				// If below line, we have found line pair.

				if (dt <= 0.0 || index == 30)
				{

					// Find fractional weight of two lines.

					if (dt > 0.0)
						dt = 0.0;

					dt = -dt;

					double f;

					if (index == 1)
					{
						f = 0.0;
					}
					else
					{
						f = dt / (last_dt + dt);
					}

					// Interpolate the temperature.

					TemperatureTint.Item1 = 1.0E6 / (kTempTable[index - 1].r * f +
						kTempTable[index].r * (1.0 - f));

					// Find delta from black body point to test coordinate.

					uu = u - (kTempTable[index - 1].u * f +
						kTempTable[index].u * (1.0 - f));

					vv = v - (kTempTable[index - 1].v * f +
						kTempTable[index].v * (1.0 - f));

					// Interpolate vectors along slope.

					du = du * (1.0 - f) + last_du * f;
					dv = dv * (1.0 - f) + last_dv * f;

					len = Math.Sqrt(du * du + dv * dv);

					du /= len;
					dv /= len;

					// Find distance along slope.
					TemperatureTint.Item2 = (uu * du + vv * dv) * kTintScale;

					break;

				}

				// Try next line pair.

				last_dt = dt;

				last_du = du;
				last_dv = dv;

			}

			return TemperatureTint.ToTuple();
		}

		public static Vector2 ColourTemperatureToChromaticity(double temperatureKelvin, double tint = 0.0)
		{
			Vector2 result = new Vector2();

			// Find inverse temperature to use as index.
			double r = 1.0E6 / temperatureKelvin;

			// Convert tint to offset is uv space.
			double offset = tint * (1.0 / kTintScale);

			// Search for line pair containing coordinate.
			for (uint index = 0; index <= 29; index++)
			{

				if (r < kTempTable[index + 1].r || index == 29)
				{

					// Find relative weight of first line.

					double f = (kTempTable[index + 1].r - r) /
						(kTempTable[index + 1].r - kTempTable[index].r);

					// Interpolate the black body coordinates.

					double u = kTempTable[index].u * f +
						kTempTable[index + 1].u * (1.0 - f);

					double v = kTempTable[index].v * f +
						kTempTable[index + 1].v * (1.0 - f);

					// Find vectors along slope for each line.

					double uu1 = 1.0;
					double vv1 = kTempTable[index].t;

					double uu2 = 1.0;
					double vv2 = kTempTable[index + 1].t;

					double len1 = Math.Sqrt(1.0 + vv1 * vv1);
					double len2 = Math.Sqrt(1.0 + vv2 * vv2);

					uu1 /= len1;
					vv1 /= len1;

					uu2 /= len2;
					vv2 /= len2;

					// Find vector from black body point.

					double uu3 = uu1 * f + uu2 * (1.0 - f);
					double vv3 = vv1 * f + vv2 * (1.0 - f);

					double len3 = Math.Sqrt(uu3 * uu3 + vv3 * vv3);

					uu3 /= len3;
					vv3 /= len3;

					// Adjust coordinate along this vector.

					u += uu3 * offset;
					v += vv3 * offset;

					// Convert to xy coordinates.
					result.X = (float)(1.5 * u / (u - 4.0 * v + 2.0));
					result.Y = (float)(v / (u - 4.0 * v + 2.0));
					break;
				}
			}

			return result;
		}
	}
}