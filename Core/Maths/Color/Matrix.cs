using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using OpenTK.Mathematics;

namespace Octopus.Player.Core.Maths.Color
{
    public static class Matrix
    {
		public static Matrix3 NormalizeForwardMatrix(in Matrix3 ForwardMatrix)
		{
			var CameraOne = new Vector3(1, 1, 1);
			var XYZ = ForwardMatrix * CameraOne;

			return CreateMatrix3.Diagonal(Temperature.PCStoXYZ()) *
				Matrix3.Invert(CreateMatrix3.Diagonal(XYZ)) * ForwardMatrix;
		}

		public static Matrix3 NormalizeColourMatrix(in Matrix3 ColourMatrix)
		{
			// Find scale factor to normalize the matrix.
			var Coord = ColourMatrix * Temperature.PCStoXYZ();

			var MaxCoord = Coord.MaxEntry();

			if (MaxCoord > 0.0f && (MaxCoord < 0.99f || MaxCoord > 1.01f))
			{
				Matrix3 Scaled = ColourMatrix;
				Scaled.Scale(1.0f / MaxCoord);
				return Scaled;
			}

			return ColourMatrix;
		}

		public static Matrix3 InterpolateColourMatrix(in Matrix3 From, in Matrix3 To, float lerp)
		{
			// Check for identity interpolations
			if (lerp <= 0.0f)
				return From;
			if (lerp >= 1.0f)
				return To;

			// Just do a linear interpolation between two known settings
			return From.LinearInterpolate(To, lerp);
		}
	
		public static Matrix3 BradfordChromaticAdaptationD65toD50(in Matrix3 D65)
		{
			// From: http://www.brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html
			Matrix3 AdaptationMatrix = new Matrix3(
				new Vector3(1.0478112f, 0.0228866f, -0.0501270f),
				new Vector3(0.0295424f, 0.9904844f, -0.0170491f),
				new Vector3(-0.0092345f, 0.0150436f, 0.7521316f));

			return D65 * AdaptationMatrix;
		}

		public static Matrix3 BradfordChromaticAdaptationD50toD65(in Matrix3 D50)
		{
			// From: http://www.brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html
			var AdaptationMatrix = new Matrix3(
				new Vector3(0.9555766f, -0.0230393f, 0.0631636f),
				new Vector3(-0.0282895f, 1.0099416f, 0.0210077f),
				new Vector3(0.0122982f, -0.0204830f, 1.3299098f));

			return D50 * AdaptationMatrix;
		}

		public static Vector3 Rec709LuminanceWeights()
		{
			return new Vector3(0.2126f, 0.7152f, 0.0722f);
		}

		public static Matrix3 XYZToRec2020D65()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(1.7166634f, -0.3556733f, -0.2533681f),
				new Vector3(-0.6666738f, 1.6164557f, 0.0157683f),
				new Vector3(0.0176425f, -0.0427770f, 0.9422433f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZToRec2020D50()
		{
			// TODO: Check this is correct
			var ConversionMatrix = new Matrix3(
				new Vector3(1.5742306f, -0.3261628f, -0.2323459f),
				new Vector3(-0.6756925f, 1.6383230f, 0.0159816f),
				new Vector3(0.0234682f, -0.0569023f, 1.2533794f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZTosRGBD65()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(3.2404542f, -1.5371385f, -0.4985314f),
				new Vector3(-0.9692660f, 1.8760108f, 0.0415560f),
				new Vector3(0.0556434f, -0.2040259f, 1.0572252f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZToRec709D65()
		{
			return XYZTosRGBD65();
		}

		public static Matrix3 XYZTosRGBD50()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(3.1338561f, -1.6168667f, -0.4906146f),
				new Vector3(-0.9787684f, 1.9161415f, 0.0334540f),
				new Vector3(0.0719453f, -0.2289914f, 1.4052427f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZToRec709D50()
		{
			return XYZTosRGBD50();
		}

		public static Matrix3 XYZtoBMDFilmD50()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(1.693614f, -0.459157f, -0.138632f),
				new Vector3(-0.489970f, 1.344410f, 0.111740f),
				new Vector3(-0.074796f, 0.385269f, 0.629528f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZtoBMDFilmD65()
		{
			return BradfordChromaticAdaptationD50toD65(XYZtoBMDFilmD50());
		}

		public static Matrix3 XYZtoAlexaWideGamutD50()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(1.789066f, -0.482534f, -0.200076f),
				new Vector3(-0.639849f, 1.396400f, 0.194432f),
				new Vector3(-0.041532f, 0.082335f, 0.878868f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZtoAlexaWideGamutD65()
		{
			return BradfordChromaticAdaptationD50toD65(XYZtoAlexaWideGamutD50());
		}

		public static Matrix3 XYZtoWideGamutD50()
		{
			var ConversionMatrix = new Matrix3(
				new Vector3(1.4628067f, -0.1840623f, -0.2743606f),
				new Vector3(-0.5217933f, 1.4472381f, 0.0677227f),
				new Vector3(0.0349342f, -0.0968930f, 1.2884099f));

			return ConversionMatrix;
		}

		public static Matrix3 XYZtoWideGamutD65()
		{
			return BradfordChromaticAdaptationD50toD65(XYZtoWideGamutD50());
        }

        struct CameraColorProfile
        {
			Illuminant illuminant1;
			Illuminant illuminant2;
			Matrix3 colorMatrix1;
			Matrix3 colorMatrix2;
			Matrix3 forwardMatrix1;
			Matrix3 forwardMatrix2;
			bool isDualIlluminant;
			bool hasForwardMatrix;
		}

        /*
                public static Matrix3 XYZToCamera(in Vector2 WhiteXY, const sColourCorrectionProfile& Profile)
                {
                    var WhiteTemperature = ChromaticityToTemperatureTint(WhiteXY).first;
                    var ColourTemp1 = LightSourceColourTemperature(Profile.CalibrationIlluminant1);
                    var ColourTemp2 = LightSourceColourTemperature(Profile.CalibrationIlluminant2);

                    double g;

                    if (WhiteTemperature <= ColourTemp1)
                        g = 1.0;

                    else if (WhiteTemperature >= ColourTemp2)
                        g = 0.0;
                    else
                    {
                        double invT = 1.0 / WhiteTemperature;
                        g = (invT - (1.0 / ColourTemp2)) /
                            ((1.0 / ColourTemp1) - (1.0 / ColourTemp2));
                    }

                    if (Profile.ForwardMatrix1.has_value() && Profile.ForwardMatrix2.has_value())
                    {
                        if (g >= 1.0)
                            return Profile.ForwardMatrix1.value();
                        else if (g <= 0.0)
                            return Profile.ForwardMatrix2.value();
                        else
                            return InterpolateColourMatrix(Profile.ForwardMatrix2.value(), Profile.ForwardMatrix1.value(), (float)g);
                    }

                    // Interpolate the color matrix.
                    if (g >= 1.0)
                        return Profile.ColorMatrix1;
                    else if (g <= 0.0)
                        return Profile.ColorMatrix2;
                    else
                        return InterpolateColourMatrix(Profile.ColorMatrix2, Profile.ColorMatrix1, (float)g);
                }

                public static Vector2 NeutralToXY(const Vector3& Neutral, const sColourCorrectionProfile& Profile)
                {
                    const uint32 kMaxPasses = 30;

                    Vector2 last = D50ChromaticityXY();

                    for (uint32 pass = 0; pass < kMaxPasses; pass++)
                    {
                        const auto&XYZToCameraMatrix = XYZToCamera(last, Profile);

                        Vector2 next = XYZtoChromaticityXY(Matrix3.Invert(XYZToCameraMatrix) * Neutral);

                        if (std::fabs(next.m_X - last.m_X) +
                            std::fabs(next.m_Y - last.m_Y) < 0.0000001f)
                        {
                            return next;
                        }

                        // If we reach the limit without converging, we are most likely
                        // in a two value oscillation.  So take the average of the last
                        // two estimates and give up.
                        if (pass == kMaxPasses - 1)
                        {
                            next.m_X = (last.m_X + next.m_X) * 0.5f;
                            next.m_Y = (last.m_Y + next.m_Y) * 0.5f;
                        }

                        last = next;

                    }

                    return last;
                }

                public static Matrix3 CameraToXYZ(u32 ColourTemperature, const sColourCorrectionProfile& Profile)
                {
                    // Clamp colour temperature to ranges in profile
                    var ColourTemp1 = LightSourceColourTemperature(Profile.CalibrationIlluminant1);
                    var ColourTemp2 = LightSourceColourTemperature(Profile.CalibrationIlluminant2);
                    var ColourTempMin = std::min(ColourTemp1, ColourTemp2);
                    var ColourTempMax = std::max(ColourTemp1, ColourTemp2);

                    var ColourTempMinRecip = 1.0f / ColourTempMin;
                    var ColourTempMaxRecip = 1.0f / ColourTempMax;

                    var ClampedColourTemperature = std::clamp(1.0f / static_cast<float>(ColourTemperature),
                        std::min(ColourTempMinRecip, ColourTempMaxRecip), std::max(ColourTempMinRecip, ColourTempMaxRecip));
                    var Interpolate = (ClampedColourTemperature - 1.0f / ColourTempMin) / (1.0f / ColourTempMax - 1.0f / ColourTempMin);

                    // Prefer forward matrix
                    if (Profile.ForwardMatrix1.has_value() && Profile.ForwardMatrix2.has_value())
                    {

                        // Interpolate forward matrix
                        const auto&MinForwardMatrix = ColourTempMin == ColourTemp1 ? Profile.ForwardMatrix1.value() : Profile.ForwardMatrix2.value();
                        const auto&MaxForwardMatrix = ColourTempMax == ColourTemp2 ? Profile.ForwardMatrix2.value() : Profile.ForwardMatrix1.value();
                        const auto&ForwardMatrix = InterpolateColourMatrix(MinForwardMatrix, MaxForwardMatrix, Interpolate);
                        return ForwardMatrix;

                    }
                    else
                    {

                        // Interpolate colour matrix
                        assert(false); // Warning, using CameraToXYZ for colour profile without Forward Matrices, please use XYZToCamera;
                        const auto&MinColourMatrix = ColourTempMin == ColourTemp1 ? Profile.ColorMatrix1 : Profile.ColorMatrix2;
                        const auto&MaxColourMatrix = ColourTempMax == ColourTemp2 ? Profile.ColorMatrix2 : Profile.ColorMatrix1;
                        const auto&ColourMatrix = InterpolateColourMatrix(MinColourMatrix, MaxColourMatrix, Interpolate);
                        const auto&XYZToCamera = ColourMatrix;
                        const auto&CameraToXYZ = Matrix3.Invert(XYZToCamera);
                        const Matrix3 ChromaticAdaptationMatrix;
                        return ChromaticAdaptationMatrix * CameraToXYZ;
                    }
                }
        */

        public static Matrix3 MapWhiteMatrix(in Vector3 white1, in Vector3 white2)
		{
			var mb = new Matrix3(new Vector3(0.8951f, 0.2664f, -0.1614f),
				new Vector3(-0.7502f, 1.7135f, 0.0367f),
				new Vector3(0.0389f, -0.0685f, 1.0296f));

			Vector3 w1 = mb * white1;
			Vector3 w2 = mb * white2;

			// Negative white coordinates are kind of meaningless.
			w1[0] = Math.Max(w1[0], 0.0f);
			w1[1] = Math.Max(w1[1], 0.0f);
			w1[2] = Math.Max(w1[2], 0.0f);
			w2[0] = Math.Max(w2[0], 0.0f);
			w2[1] = Math.Max(w2[1], 0.0f);
			w2[2] = Math.Max(w2[2], 0.0f);

			// Limit scaling to something reasonable.
			Matrix3 a = new Matrix3();
			a[0,0] = Math.Max(0.1f, Math.Min(w1[0] > 0.0f ? w2[0] / w1[0] : 10.0f, 10.0f));
			a[1,1] = Math.Max(0.1f, Math.Min(w1[1] > 0.0f ? w2[1] / w1[1] : 10.0f, 10.0f));
			a[2,2] = Math.Max(0.1f, Math.Min(w1[2] > 0.0f ? w2[2] / w1[2] : 10.0f, 10.0f));

			return (Matrix3.Invert(mb) * a) * mb;
		}
	/*
		public static Matrix3 CalculateCameraToXYZD50(const sColourCorrectionProfile& Profile)
		{
			// If there are forward matrices use camera to xyz based approach (forward matrices)
			const auto&AsShotWhiteXYZ = ChromaticityXYtoXYZ(Profile.AsShotWhiteXY);
			if (Profile.ForwardMatrix1.has_value())
			{

				var ColourTemperature = ChromaticityToColourTemperature(Profile.AsShotWhiteXY);
				const auto&CameraToXYZMatrix = CameraToXYZ(ColourTemperature, Profile);

				// Calculate camera neutral
				const auto&XYZToCameraMatrix = Matrix3.Invert(CameraToXYZMatrix);
				var CameraNeutral = XYZToCameraMatrix * AsShotWhiteXYZ;
				CameraNeutral = CameraNeutral / std::max(std::max(CameraNeutral.X(), CameraNeutral.Y()), CameraNeutral.Z());

				// Update Camera to XYZD50 Matrix
				const auto&ReferenceNeutral = CameraNeutral;
				const auto&ReferenceNeutralDiagonalMatrix = Matrix3(
					Vector3(ReferenceNeutral.X(), 0, 0),
					Vector3(0, ReferenceNeutral.Y(), 0),
					Vector3(0, 0, ReferenceNeutral.Z()));
				const auto&D50Matrix = Matrix3.Invert(ReferenceNeutralDiagonalMatrix);
				return CameraToXYZMatrix * D50Matrix;

				// No forward matrices
			}
			else
			{

				// Get color matrix (nor forward)
				const auto&XYZToCameraMatrix = XYZToCamera(Profile.AsShotWhiteXY, Profile);

				// Create final camera to xyz50 matrix via D50 mapping
				var PCSToCamera = XYZToCameraMatrix * MapWhiteMatrix(D50ChromaticityXYZ(), AsShotWhiteXYZ);
				const float Scale = (PCSToCamera * PCStoXYZ()).MaxEntry();
				PCSToCamera.Scale(Scale);
				return Matrix3.Invert(PCSToCamera);
			}
		}

		public static Matrix3 CalculateCameraToXYZD65(const sColourCorrectionProfile& Profile)
		{
			// If there are forward matrices use camera to xyz based approach (forward matrices)
			const auto&AsShotWhiteXYZ = ChromaticityXYtoXYZ(Profile.AsShotWhiteXY);
			if (Profile.ForwardMatrix1.has_value())
			{

				var ColourTemperature = ChromaticityToColourTemperature(Profile.AsShotWhiteXY);
				const auto&CameraToXYZMatrix = CameraToXYZ(ColourTemperature, Profile);

				// Calculate camera neutral
				const auto&XYZToCameraMatrix = Matrix3.Invert(CameraToXYZMatrix);
				var CameraNeutral = XYZToCameraMatrix * AsShotWhiteXYZ;
				CameraNeutral = CameraNeutral / std::max(std::max(CameraNeutral.X(), CameraNeutral.Y()), CameraNeutral.Z());

				// Update Camera to XYZD50 Matrix
				const auto&ReferenceNeutral = CameraNeutral;
				const auto&ReferenceNeutralDiagonalMatrix = Matrix3(
					Vector3(ReferenceNeutral.X(), 0, 0),
					Vector3(0, ReferenceNeutral.Y(), 0),
					Vector3(0, 0, ReferenceNeutral.Z()));
				const auto&D50Matrix = Matrix3.Invert(ReferenceNeutralDiagonalMatrix);
				return BradfordChromaticAdaptationD50toD65(CameraToXYZMatrix * D50Matrix);

				// No forward matrices
			}
			else
			{

				// Get color matrix (nor forward)
				const auto&XYZToCameraMatrix = XYZToCamera(Profile.AsShotWhiteXY, Profile);

				// Create final camera to xyz50 matrix via D50 mapping
				return BradfordChromaticAdaptationD50toD65(Matrix3.Invert(XYZToCameraMatrix * MapWhiteMatrix(D50ChromaticityXYZ(), AsShotWhiteXYZ)));
			}
		}
*/
		public static Matrix3 ApplyTemperatureTintOffset(in Matrix3 ColorMatrix, Illuminant illuminant, float TemperatureOffset, float TintOffset)
		{
			var IlluminantTemperature = illuminant.ColorTemperature();

			var ChromaticityTarget = Temperature.ColourTemperatureToChromaticity(IlluminantTemperature, 0.0f);
			var ChromaticityTargetXYZ = Temperature.ChromaticityXYtoXYZ(ChromaticityTarget);

			var ChromaticityOffset = Temperature.ColourTemperatureToChromaticity(IlluminantTemperature + TemperatureOffset, 0.0f + TintOffset);
			var ChromaticityOffsetXYZ = Temperature.ChromaticityXYtoXYZ(ChromaticityOffset);

			return ColorMatrix * MapWhiteMatrix(ChromaticityOffsetXYZ, ChromaticityTargetXYZ);
		}
	}
}