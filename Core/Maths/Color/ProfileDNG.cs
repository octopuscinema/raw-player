using Octopus.Player.Core.IO.DNG;
using Octopus.Player.Core.Maths.Color;
using OpenTK.Mathematics;
using System;
using System.Diagnostics;

namespace Octopus.Player.Core.Maths.Color
{
    public class ProfileDNG : IProfile
    {
        public WhitePoint WhitePoint { get { return Color.WhitePoint.D50; } }

        public Tuple<float, float> AsShotWhiteBalance { get; private set; }

        public bool HasAsShotMetadata { get; private set; }

        Vector2? AsShotWhiteXY { get; set; }
        Illuminant CalibrationIlluminant1 { get; set; }
        Illuminant CalibrationIlluminant2 { get; set; }
        Matrix3 ColorMatrix1 { get; set; }
        Matrix3 ColorMatrix2 { get; set; }
        Matrix3 ForwardMatrix1 { get; set; }
        Matrix3 ForwardMatrix2 { get; set; }
        bool IsDualIlluminant { get; set; }
        bool HasForwardMatrix { get; set; }


        public ProfileDNG(IO.DNG.Reader reader, Tuple<float, float> defaultWhiteBalance = null)
        {
            if (defaultWhiteBalance == null)
                defaultWhiteBalance = WhiteBalance.Default;

            CalibrationIlluminant2 = Illuminant.Unknown;
            ColorMatrix2 = Matrix3.Identity;
            ForwardMatrix1 = Matrix3.Identity;
            ForwardMatrix2 = Matrix3.Identity;
            IsDualIlluminant = reader.IsDualIlluminant;
            HasForwardMatrix = reader.HasForwardMatrix;
            ColorMatrix1 = reader.ColorMatrix1;
            CalibrationIlluminant1 = reader.CalibrationIlluminant1;
            if (reader.IsDualIlluminant)
            {
                ColorMatrix2 = reader.ColorMatrix2;
                CalibrationIlluminant2 = reader.CalibrationIlluminant2;
                if (reader.HasForwardMatrix)
                    ForwardMatrix2 = reader.ForwardMatrix2;
            }
            if (reader.HasForwardMatrix)
                ForwardMatrix1 = reader.ForwardMatrix1;

            // Swap color matrix 1/2 if dual illuminant is out of order
            if (IsDualIlluminant && CalibrationIlluminant1.ColorTemperature() > CalibrationIlluminant2.ColorTemperature())
            {
                ColorMatrix1 = reader.ColorMatrix2;
                ColorMatrix2 = reader.ColorMatrix1;
                CalibrationIlluminant2 = reader.CalibrationIlluminant2;
                CalibrationIlluminant1 = reader.CalibrationIlluminant1;
                if (reader.HasForwardMatrix)
                {
                    ForwardMatrix1 = reader.ForwardMatrix2;
                    ForwardMatrix2 = reader.ForwardMatrix1;
                }
            }

            if (reader.HasAsShotNeutral)
                AsShotWhiteXY = NeutralToXY(reader.AsShotNeutral);
            else if (reader.HasAsShotWhiteXY)
                AsShotWhiteXY = reader.AsShotWhiteXY;
            HasAsShotMetadata = AsShotWhiteXY.HasValue;

            if (AsShotWhiteXY.HasValue)
                AsShotWhiteBalance = Temperature.ChromaticityToTemperatureTint(AsShotWhiteXY.Value);
            else
            {
                AsShotWhiteXY = Temperature.ColourTemperatureToChromaticity(defaultWhiteBalance.Item1, defaultWhiteBalance.Item2);
                AsShotWhiteBalance = defaultWhiteBalance;
            }
        }

        ProfileDNG(Matrix3 colorMatrix, Illuminant illuminant, uint colorTemperature)
        {
            AsShotWhiteXY = Temperature.ColourTemperatureToChromaticity(colorTemperature);
            AsShotWhiteBalance = new Tuple<float, float>((float)colorTemperature, 0.0f);
            ColorMatrix1 = colorMatrix;
            ColorMatrix2 = Matrix3.Identity;
            CalibrationIlluminant1 = illuminant;
            CalibrationIlluminant2 = Illuminant.Unknown;
            IsDualIlluminant = false;
            HasForwardMatrix = false;
            ForwardMatrix1 = Matrix3.Identity;
            ForwardMatrix2 = Matrix3.Identity;
        }

        ProfileDNG(Matrix3 colorMatrix1, Illuminant illuminant1, Matrix3 colorMatrix2, Illuminant illuminant2, uint colorTemperature)
        {
            AsShotWhiteXY = Temperature.ColourTemperatureToChromaticity(colorTemperature);
            AsShotWhiteBalance = new Tuple<float, float>((float)colorTemperature, 0.0f);
            ColorMatrix1 = colorMatrix1;
            CalibrationIlluminant1 = illuminant1;
            ColorMatrix2 = colorMatrix2;
            CalibrationIlluminant2 = illuminant2;
            IsDualIlluminant = true;
            HasForwardMatrix = false;
            ForwardMatrix1 = Matrix3.Identity;
            ForwardMatrix2 = Matrix3.Identity;

            // Swap color matrix 1/2 if dual illuminant is out of order
            if (CalibrationIlluminant1.ColorTemperature() > CalibrationIlluminant2.ColorTemperature())
            {
                ColorMatrix1 = colorMatrix2;
                ColorMatrix2 = colorMatrix1;
                CalibrationIlluminant2 = illuminant2;
                CalibrationIlluminant1 = illuminant1;
            }
        }

        Matrix3 XYZToCamera(Vector2 whiteXY)
        {
            if (!IsDualIlluminant)
                return ColorMatrix1;

            var whiteTemperature = Temperature.ChromaticityToTemperatureTint(whiteXY).Item1;
            var colorTemp1 = CalibrationIlluminant1.ColorTemperature();
            var colorTemp2 = CalibrationIlluminant2.ColorTemperature();

            double g;

            if (whiteTemperature <= colorTemp1)
                g = 1.0;
            else if (whiteTemperature >= colorTemp2)
                g = 0.0;
            else
            {
                double invT = 1.0 / whiteTemperature;
                g = (invT - (1.0 / colorTemp2)) /
                    ((1.0 / colorTemp1) - (1.0 / colorTemp2));
            }

            // Interpolate the color matrix.
            if (g >= 1.0)
                return ColorMatrix1;
            else if (g <= 0.0)
                return ColorMatrix2;
            else
                return ColorMatrix2.LinearInterpolate(ColorMatrix1, (float)g);
        }

        Vector2 NeutralToXY(Vector3 neutral)
        {
            var kMaxPasses = 30u;

            Vector2 last = Temperature.D50ChromaticityXY();

            for (var pass = 0u; pass < kMaxPasses; pass++)
            {
                var XYZToCameraMatrix = XYZToCamera(last);

                Vector2 next = Temperature.XYZtoChromaticityXYD50(XYZToCameraMatrix.Inverted() * neutral);

                if (Math.Abs(next.X - last.X) +
                    Math.Abs(next.Y - last.Y) < 0.0000001f)
                {
                    return next;
                }

                // If we reach the limit without converging, we are most likely
                // in a two value oscillation.  So take the average of the last
                // two estimates and give up.
                if (pass == kMaxPasses - 1)
                {
                    next.X = (last.X + next.X) * 0.5f;
                    next.Y = (last.Y + next.Y) * 0.5f;
                }

                last = next;
            }

            return last;
        }

        Matrix3 CameraToXYZ(uint colorTemperature)
        {
            // Warning, using CameraToXYZ for colour profile without Forward Matrices, please use XYZToCamera
            Debug.Assert(HasForwardMatrix);

            if (!IsDualIlluminant)
            {
                if (HasForwardMatrix)
                    return ForwardMatrix1;
                else
                {
                    var xyzToCamera = ColorMatrix1;
                    var cameraToXYZ = xyzToCamera.Inverted();
                    var chromaticAdaptationMatrix = Matrix3.Identity;
                    return chromaticAdaptationMatrix * cameraToXYZ;
                }
            }

            // Clamp color temperature to ranges in profile
            var colorTemp1 = CalibrationIlluminant1.ColorTemperature();
            var colorTemp2 = CalibrationIlluminant2.ColorTemperature();
            var colorTempMin = Math.Min(colorTemp1, colorTemp2);
            var colorTempMax = Math.Max(colorTemp1, colorTemp2);

            var colorTempMinRecip = 1.0f / colorTempMin;
            var colorTempMaxRecip = 1.0f / colorTempMax;

            var clampedColorTemperature = Math.Clamp(1.0f / (float)colorTemperature,
                Math.Min(colorTempMinRecip, colorTempMaxRecip), Math.Max(colorTempMinRecip, colorTempMaxRecip));
            var interpolate = (clampedColorTemperature - 1.0f / colorTempMin) / (1.0f / colorTempMax - 1.0f / colorTempMin);

            // Prefer forward matrix
            if (HasForwardMatrix)
            {
                // Interpolate forward matrix
                var minForwardMatrix = colorTempMin == colorTemp1 ? ForwardMatrix1 : ForwardMatrix2;
                var maxForwardMatrix = colorTempMax == colorTemp2 ? ForwardMatrix2 : ForwardMatrix1;
                var forwardMatrix = minForwardMatrix.InterpolateColor(maxForwardMatrix, interpolate);
                return forwardMatrix;
            }
            else
            {
                // Interpolate color matrix
                var minColorMatrix = colorTempMin == colorTemp1 ? ColorMatrix1 : ColorMatrix2;
                var maxColorMatrix = colorTempMax == colorTemp2 ? ColorMatrix2 : ColorMatrix1;
                var colorMatrix = minColorMatrix.InterpolateColor(maxColorMatrix, interpolate);
                var xyzToCamera = colorMatrix;
                var cameraToXYZ = xyzToCamera.Inverted();
                var chromaticAdaptationMatrix = Matrix3.Identity;
                return chromaticAdaptationMatrix * cameraToXYZ;
            }
        }

        public Matrix3 CalculateCameraToXYZ(Tuple<float, float>? whiteBalance)
        {
            return (whiteBalance != null) ? CalculateCameraToXYZ(Temperature.ColourTemperatureToChromaticity(whiteBalance.Item1, whiteBalance.Item2)) :
                CalculateCameraToXYZ();
        }

        public Matrix3 CalculateCameraToXYZ(Vector2? whiteXY = null)
        {
            Debug.Assert(whiteXY != null || AsShotWhiteXY.HasValue);
            if (!whiteXY.HasValue)
                whiteXY = AsShotWhiteXY.Value;

            // If there are forward matrices use camera to xyz based approach (forward matrices)
            var asShotWhiteXYZ = Temperature.ChromaticityXYtoXYZ(whiteXY.Value);
            if (HasForwardMatrix)
            {
                var colorTemperature = Temperature.ChromaticityToColourTemperature(whiteXY.Value);
                var cameraToXYZMatrix = CameraToXYZ(colorTemperature);

                // Calculate camera neutral
                var cameraNeutral = XYZToCamera(whiteXY.Value) * asShotWhiteXYZ;
                cameraNeutral = cameraNeutral / cameraNeutral.MaxEntry();

                // Update Camera to XYZD50 Matrix
                var referenceNeutral = cameraNeutral;
                var referenceNeutralDiagonalMatrix = new Matrix3(
                    new Vector3(referenceNeutral.X, 0, 0),
                    new Vector3(0, referenceNeutral.Y, 0),
                    new Vector3(0, 0, referenceNeutral.Z));
                var D50Matrix = referenceNeutralDiagonalMatrix.Inverted();
                return cameraToXYZMatrix * D50Matrix;
            }
            else
            {
                // Get color matrix (nor forward)
                var xyzToCameraMatrix = XYZToCamera(whiteXY.Value);

                // Create final camera to xyz50 matrix via D50 mapping
                var PCSToCamera = xyzToCameraMatrix * Matrix.MapWhiteMatrix(Temperature.D50ChromaticityXYZ(), asShotWhiteXYZ);
                var scale = (PCSToCamera * Temperature.PCStoXYZD50()).MaxEntry();
                PCSToCamera.Scale(scale);
                return PCSToCamera.Inverted();
            }
        }
    }
}
