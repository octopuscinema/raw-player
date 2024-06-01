using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Octopus.Player.Core.Maths.Color
{
    public struct Profile
    {
        public static Vector3 Rec709LuminanceWeights { get { return new Vector3(0.2126f, 0.7152f, 0.0722f); } }

        public readonly Vector2? asShotWhiteXY;
        public readonly Illuminant calibrationIlluminant1;
        public readonly Illuminant calibrationIlluminant2;
        public readonly Matrix3 colorMatrix1;
        public readonly Matrix3 colorMatrix2;
        public readonly Matrix3 forwardMatrix1;
        public readonly Matrix3 forwardMatrix2;
        public readonly bool isDualIlluminant;
        public readonly bool hasForwardMatrix;

        public Profile(IO.DNG.Reader reader) : this()
        {
            isDualIlluminant = reader.IsDualIlluminant;
            hasForwardMatrix = reader.HasForwardMatrix;
            colorMatrix1 = reader.ColorMatrix1;
            calibrationIlluminant1 = reader.CalibrationIlluminant1;
            if (reader.IsDualIlluminant)
            {
                colorMatrix2 = reader.ColorMatrix2;
                calibrationIlluminant2 = reader.CalibrationIlluminant2;
                if (reader.HasForwardMatrix)
                    forwardMatrix2 = reader.ForwardMatrix2;
            }
            if (reader.HasForwardMatrix)
                forwardMatrix1 = reader.ForwardMatrix1;

            // Swap color matrix 1/2 if dual illuminant is out of order
            if ( isDualIlluminant && calibrationIlluminant1.ColorTemperature() > calibrationIlluminant2.ColorTemperature()) 
            {
                colorMatrix1 = reader.ColorMatrix2;
                colorMatrix2 = reader.ColorMatrix1;
                calibrationIlluminant2 = reader.CalibrationIlluminant2;
                calibrationIlluminant1 = reader.CalibrationIlluminant1;
                if (reader.HasForwardMatrix)
                {
                    forwardMatrix1 = reader.ForwardMatrix2;
                    forwardMatrix2 = reader.ForwardMatrix1;
                }
            }

            if (reader.HasAsShotNeutral)
                asShotWhiteXY = NeutralToXY(reader.AsShotNeutral);
            else if (reader.HasAsShotWhiteXY)
                asShotWhiteXY = reader.AsShotWhiteXY;
        }

        public Tuple<double,double> AsShotWhiteBalance()
        {
            Debug.Assert(asShotWhiteXY.HasValue);
            return Temperature.ChromaticityToTemperatureTint(asShotWhiteXY.Value);
        }

        public Matrix3 XYZToCamera(in Vector2 whiteXY)
        {
            if (!isDualIlluminant)
                return colorMatrix1;

            var WhiteTemperature = Temperature.ChromaticityToTemperatureTint(whiteXY).Item1;
            var ColourTemp1 = calibrationIlluminant1.ColorTemperature();
            var ColourTemp2 = calibrationIlluminant2.ColorTemperature();

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

            // Interpolate the color matrix.
            if (g >= 1.0)
                return colorMatrix1;
            else if (g <= 0.0)
                return colorMatrix2;
            else
                return Matrix.InterpolateColourMatrix(colorMatrix2, colorMatrix1, (float)g);
        }

        public Vector2 NeutralToXY(in Vector3 neutral)
        {
            const uint kMaxPasses = 30;

            Vector2 last = Temperature.D50ChromaticityXY();

            for (uint pass = 0; pass < kMaxPasses; pass++)
            {
                var XYZToCameraMatrix = XYZToCamera(last);

                Vector2 next = Temperature.XYZtoChromaticityXY(Matrix3.Invert(XYZToCameraMatrix) * neutral);

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

        public Matrix3 CameraToXYZ(uint colorTemperature)
        {
            Debug.Assert(hasForwardMatrix, "Warning, using CameraToXYZ for colour profile without Forward Matrices, please use XYZToCamera");

            if (!isDualIlluminant)
            {
                if (hasForwardMatrix)
                    return forwardMatrix1;
                else
                {
                    var XYZToCamera = colorMatrix1;
                    var CameraToXYZ = Matrix3.Invert(XYZToCamera);
                    Matrix3 ChromaticAdaptationMatrix = Matrix3.Identity;
                    return ChromaticAdaptationMatrix * CameraToXYZ;
                }
            }

            // Clamp colour temperature to ranges in profile
            var ColourTemp1 = calibrationIlluminant1.ColorTemperature();
            var ColourTemp2 = calibrationIlluminant2.ColorTemperature();
            var ColourTempMin = Math.Min(ColourTemp1, ColourTemp2);
            var ColourTempMax = Math.Max(ColourTemp1, ColourTemp2);

            var ColourTempMinRecip = 1.0f / ColourTempMin;
            var ColourTempMaxRecip = 1.0f / ColourTempMax;

            var ClampedColourTemperature = Math.Clamp(1.0f / (float)colorTemperature,
                Math.Min(ColourTempMinRecip, ColourTempMaxRecip), Math.Max(ColourTempMinRecip, ColourTempMaxRecip));
            var Interpolate = (ClampedColourTemperature - 1.0f / ColourTempMin) / (1.0f / ColourTempMax - 1.0f / ColourTempMin);

            // Prefer forward matrix
            if (hasForwardMatrix)
            {
                // Interpolate forward matrix
                var MinForwardMatrix = ColourTempMin == ColourTemp1 ? forwardMatrix1 : forwardMatrix2;
                var MaxForwardMatrix = ColourTempMax == ColourTemp2 ? forwardMatrix2 : forwardMatrix1;
                var ForwardMatrix = Matrix.InterpolateColourMatrix(MinForwardMatrix, MaxForwardMatrix, Interpolate);
                return ForwardMatrix;
            }
            else
            {
                // Interpolate colour matrix
                var MinColourMatrix = ColourTempMin == ColourTemp1 ? colorMatrix1 : colorMatrix2;
                var MaxColourMatrix = ColourTempMax == ColourTemp2 ? colorMatrix2 : colorMatrix1;
                var ColourMatrix = Matrix.InterpolateColourMatrix(MinColourMatrix, MaxColourMatrix, Interpolate);
                var XYZToCamera = ColourMatrix;
                var CameraToXYZ = Matrix3.Invert(XYZToCamera);
                Matrix3 ChromaticAdaptationMatrix = Matrix3.Identity;
                return ChromaticAdaptationMatrix * CameraToXYZ;
            }
        }

        public Matrix3 CalculateCameraToXYZD50(Tuple<float,float> whiteBalance)
        {
            return (whiteBalance != null) ? CalculateCameraToXYZD50(Temperature.ColourTemperatureToChromaticity(whiteBalance.Item1, whiteBalance.Item2)) :
                CalculateCameraToXYZD50();
        }

        public Matrix3 CalculateCameraToXYZD65(Tuple<float, float> whiteBalance)
        {
            return (whiteBalance != null) ? CalculateCameraToXYZD65(Temperature.ColourTemperatureToChromaticity(whiteBalance.Item1, whiteBalance.Item2)) :
                CalculateCameraToXYZD65();
        }

        public Matrix3 CalculateCameraToXYZD50(Vector2? whiteXY = null)
        {
            Debug.Assert(whiteXY != null || asShotWhiteXY.HasValue);
            if (!whiteXY.HasValue)
                whiteXY = asShotWhiteXY.Value;

            // If there are forward matrices use camera to xyz based approach (forward matrices)
            var AsShotWhiteXYZ = Temperature.ChromaticityXYtoXYZ(whiteXY.Value);
            if (hasForwardMatrix)
            {
                var ColourTemperature = Temperature.ChromaticityToColourTemperature(whiteXY.Value);
                var CameraToXYZMatrix = CameraToXYZ(ColourTemperature);

                // Calculate camera neutral
                var CameraNeutral = XYZToCamera(whiteXY.Value) * AsShotWhiteXYZ;
                CameraNeutral = CameraNeutral / Math.Max(Math.Max(CameraNeutral.X, CameraNeutral.Y), CameraNeutral.Z);

                // Update Camera to XYZD50 Matrix
                var ReferenceNeutral = CameraNeutral;
                var ReferenceNeutralDiagonalMatrix = new Matrix3(
                    new Vector3(ReferenceNeutral.X, 0, 0),
                    new Vector3(0, ReferenceNeutral.Y, 0),
                    new Vector3(0, 0, ReferenceNeutral.Z));
                var D50Matrix = Matrix3.Invert(ReferenceNeutralDiagonalMatrix);
                return CameraToXYZMatrix * D50Matrix;
            }
            else
            {
                // Get color matrix (nor forward)
                var XYZToCameraMatrix = XYZToCamera(whiteXY.Value);

                // Create final camera to xyz50 matrix via D50 mapping
                var PCSToCamera = XYZToCameraMatrix * Matrix.MapWhiteMatrix(Temperature.D50ChromaticityXYZ(), AsShotWhiteXYZ);
                float Scale = (PCSToCamera * Temperature.PCStoXYZ()).MaxEntry();
                PCSToCamera.Scale(Scale);
                return Matrix3.Invert(PCSToCamera);
            }
        }

        public Matrix3 CalculateCameraToXYZD65(Vector2? whiteXY = null)
        {
            Debug.Assert(asShotWhiteXY.HasValue);
            if (!whiteXY.HasValue)
                whiteXY = asShotWhiteXY.Value;

            // If there are forward matrices use camera to xyz based approach (forward matrices)
            var AsShotWhiteXYZ = Temperature.ChromaticityXYtoXYZ(asShotWhiteXY.Value);
            if (hasForwardMatrix)
            {
                var ColourTemperature = Temperature.ChromaticityToColourTemperature(asShotWhiteXY.Value);
                var CameraToXYZMatrix = CameraToXYZ(ColourTemperature);

                // Calculate camera neutral
                var XYZToCameraMatrix = Matrix3.Invert(CameraToXYZMatrix);
                var CameraNeutral = XYZToCameraMatrix * AsShotWhiteXYZ;
                CameraNeutral = CameraNeutral / Math.Max(Math.Max(CameraNeutral.X, CameraNeutral.Y), CameraNeutral.Z);

                // Update Camera to XYZD50 Matrix
                var ReferenceNeutral = CameraNeutral;
                var ReferenceNeutralDiagonalMatrix = new Matrix3(
                    new Vector3(ReferenceNeutral.X, 0, 0),
                    new Vector3(0, ReferenceNeutral.Y, 0),
                    new Vector3(0, 0, ReferenceNeutral.Z));
                var D50Matrix = Matrix3.Invert(ReferenceNeutralDiagonalMatrix);
                return Matrix.BradfordChromaticAdaptationD50toD65(CameraToXYZMatrix * D50Matrix);
            }
            else
            {
                // Get color matrix (not forward)
                var XYZToCameraMatrix = XYZToCamera(asShotWhiteXY.Value);

                // Create final camera to xyz50 matrix via D50 mapping
                return Matrix.BradfordChromaticAdaptationD50toD65(Matrix3.Invert(XYZToCameraMatrix * Matrix.MapWhiteMatrix(Temperature.D50ChromaticityXYZ(), AsShotWhiteXYZ)));
            }
        }

        public override string ToString()
        {
            string text = "";
            text += "\nAs Shot XY: " + (asShotWhiteXY.HasValue ? asShotWhiteXY.Value.ToString() : "Unknown");
            text += "\nCalibration Illuminant 1: " + calibrationIlluminant1;
            text += "\nColor Matrix 1: " + colorMatrix1;
            if (hasForwardMatrix)
                text += "\nForward Matrix 1: " + forwardMatrix1;

            if ( isDualIlluminant )
            {
                text += "\nCalibration Illuminant 2: " + calibrationIlluminant2;
                text += "\nColor Matrix 2: " + colorMatrix2;
                if (hasForwardMatrix)
                    text += "\nForward Matrix 2: " + forwardMatrix2;
            }

            return text + "\n";
        }
    }
}
