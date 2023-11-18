using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.IO.DNG
{
    public enum TiffTagDNG
    {
		ForwardMatrix2 = 50965,
		ForwardMatrix1 = 50964,
		CalibrationIlluminant2 = 50779,
		CalibrationIlluminant1 = 50778,
		BaselineExposure = 50730,
		AsShotWhiteXY = 50729,
		AsShotNeutral = 50728,
		CameraCalibration2 = 50724,
		CameraCalibration1 = 50723,
		ColorMatrix2 = 50722,
		ColorMatrix1 = 50721,
        DefaultCropSize = 50720,
        DefaultCropOrigin = 50719,
        DefaultScale = 50718,
        WhiteLevel = 50717,
		BlackLevel = 50714,
		BlackLevelRepeatDim = 50713,
		LinearizationTable = 50712,
		CFAPlaneColor = 50710,
		UniqueCameraModel = 50708,
		DNGBackwardVersion = 50707,
		DNGVersion = 50706,
		ISO = 34855,
        CFAPattern = 33422,
        CFARepeatPatternDim = 33421,
		/*DateTime = 306,
		Software = 305,
		PlanarConfiguration = 284,
		StripByteCounts = 279,
		RowsPerStrip = 278,
		SamplesPerPixel = 277,
		Orientation = 274,
		StripOffsets = 273*/
	}

    public enum TiffTagCinemaDNG
    {
        FrameRate = 51044,
		TimeCodes = 51043
	}
}
