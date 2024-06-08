#ifndef DEBAYER_CL_H
#define DEBAYER_CL_H

#include "ComputeDefines.cl.h"
#include "ComputeTypes.cl.h"
#include "ComputeMaths.cl.h"

#define DEBAYER_DIFF_EPSILON ((half)0.0001f)

PRIVATE half SynthesiseGreen(half4 greenAboveBelowLeftRight, half centre, half4 adjacentAboveBelowLeftRight)
{
	// Calculate adjcent diffs
	half4 adjacentDiff = make_half4(centre) - adjacentAboveBelowLeftRight;

	// Calculate horizontal and vertical gradients and outputs where horizontal gradient != vertical gradient
	half2 hvGradients = fabs(GET_ZX(half, greenAboveBelowLeftRight) - GET_WY(half, greenAboveBelowLeftRight) + fabs(GET_ZX(half, adjacentDiff) + GET_WY(half, adjacentDiff)));
	half2 possibleOutputs = (GET_XZ(half, greenAboveBelowLeftRight) + GET_YW(half, greenAboveBelowLeftRight)) * (half)0.5f + (GET_XZ(half, adjacentDiff) + GET_YW(half, adjacentDiff)) * (half)0.25f;

	if (hvGradients.x == hvGradients.y)
		return (greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.y + greenAboveBelowLeftRight.z + greenAboveBelowLeftRight.w) * (half)0.25f +
		(adjacentDiff.x + adjacentDiff.y + adjacentDiff.z + adjacentDiff.w) * (half)0.125f;
	else
		return (hvGradients.x > hvGradients.y) ? possibleOutputs.x : possibleOutputs.y;
}

PRIVATE half2 SynthesiseGR(half blue, half4 greenAboveBelowLeftRight, half4 blueAboveBelowLeftRight, half4 redTopLeftRightBottomLeftRight)
{
	// Synthesise GREEN by sampling the adjacent BLUEs
	half green = SynthesiseGreen(greenAboveBelowLeftRight, blue, blueAboveBelowLeftRight);

	// Synthesise RED by sampling adjacent GREENs and newly created green
	half4 greenTopLeftRightBottomLeftRight = make_half4(greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.w,
		greenAboveBelowLeftRight.y + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.w + greenAboveBelowLeftRight.y) * (half)0.5f;

	half4 greenCornerDiffs = fabs(make_half4(green) - greenTopLeftRightBottomLeftRight);
	half4 greenCornerWeights = make_half4((half)1.0f) / fmax(make_half4(DEBAYER_DIFF_EPSILON), greenCornerDiffs);
	half maxWeight = greenCornerWeights.x + greenCornerWeights.y + greenCornerWeights.z + greenCornerWeights.w;
	greenCornerWeights /= maxWeight;
	half4 redWeights = redTopLeftRightBottomLeftRight * greenCornerWeights;
	half red = redWeights.x + redWeights.y + redWeights.z + redWeights.w;

	return make_half2(green, red);
}

PRIVATE half2 SynthesiseGreenAndRed(half4 tile, half4 tileAboveLeft, half4 tileAbove, half4 tileLeft, half4 tileRight, half4 tileBelow)
{
	// Home colour is BLUE
	half blue = tile.x;

	// Synthesise GREEN by sampling the adjacent BLUEs
	half4 greenAboveBelowLeftRight = make_half4(tileAbove.z, tile.z, tileLeft.y, tile.y);
	half4 blueAboveBelowLeftRight = make_half4(tileAbove.x, tileBelow.x, tileLeft.x, tileRight.x);
	half green = SynthesiseGreen(greenAboveBelowLeftRight, blue, blueAboveBelowLeftRight);

	// Synthesise RED by sampling adjacent GREENs and newly created green
	half4 redTopLeftRightBottomLeftRight = make_half4(tileAboveLeft.w, tileAbove.w, tileLeft.w, tile.w);

	half4 greenTopLeftRightBottomLeftRight = make_half4(greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.w,
		greenAboveBelowLeftRight.y + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.w + greenAboveBelowLeftRight.y) * (half)0.5f;

	half4 greenCornerDiffs = fabs(make_half4(green) - greenTopLeftRightBottomLeftRight);
	half4 greenCornerWeights = make_half4((half)1.0f) / fmax(make_half4(DEBAYER_DIFF_EPSILON), greenCornerDiffs);
	half maxWeight = greenCornerWeights.x + greenCornerWeights.y + greenCornerWeights.z + greenCornerWeights.w;
	greenCornerWeights /= maxWeight;
	half4 redWeights = redTopLeftRightBottomLeftRight * greenCornerWeights;
	half red = redWeights.x + redWeights.y + redWeights.z + redWeights.w;

	return make_half2(green, red);
}

PRIVATE half2 SynthesiseGB(half red, half4 greenAboveBelowLeftRight, half4 redAboveBelowLeftRight, half4 blueTopLeftRightBottomLeftRight)
{
	// Synthesise GREEN by sampling the adjacent REDs
	half green = SynthesiseGreen(greenAboveBelowLeftRight, red, redAboveBelowLeftRight);

	// Synthesise BLUE by sampling adjacent GREENs and newly created green
	half4 greenTopLeftRightBottomLeftRight = make_half4(greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.w,
		greenAboveBelowLeftRight.y + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.w + greenAboveBelowLeftRight.y) * (half)0.5f;

	half4 greenCornerDiffs = fabs(make_half4(green) - greenTopLeftRightBottomLeftRight);
	half4 greenCornerWeights = make_half4((half)1.0f) / fmax(make_half4(DEBAYER_DIFF_EPSILON), greenCornerDiffs);
	half maxWeight = greenCornerWeights.x + greenCornerWeights.y + greenCornerWeights.z + greenCornerWeights.w;
	greenCornerWeights /= maxWeight;
	half4 blueWeights = blueTopLeftRightBottomLeftRight * greenCornerWeights;
	half blue = blueWeights.x + blueWeights.y + blueWeights.z + blueWeights.w;

	return make_half2(green, blue);
}

PRIVATE half2 SynthesiseGreenAndBlue(half4 tile, half4 tileAbove, half4 tileLeft, half4 tileRight, half4 tileBelow, half4 tileBelowRight)
{
	// Home colour is RED
	half red = tile.w;

	// Synthesise GREEN by sampling the adjacent REDs
	half4 greenAboveBelowLeftRight = make_half4(tile.y, tileBelow.y, tile.z, tileRight.z);
	half4 redAboveBelowLeftRight = make_half4(tileAbove.w, tileBelow.w, tileLeft.w, tileRight.w);
	half green = SynthesiseGreen(greenAboveBelowLeftRight, red, redAboveBelowLeftRight);

	// Synthesise BLUE by sampling adjacent GREENs and newly created green
	half4 blueTopLeftRightBottomLeftRight = make_half4(tile.x, tileRight.x, tileBelow.x, tileBelowRight.x);

	half4 greenTopLeftRightBottomLeftRight = make_half4(greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.x + greenAboveBelowLeftRight.w,
		greenAboveBelowLeftRight.y + greenAboveBelowLeftRight.z,
		greenAboveBelowLeftRight.w + greenAboveBelowLeftRight.y) * (half)0.5f;

	half4 greenCornerDiffs = fabs(make_half4(green) - greenTopLeftRightBottomLeftRight);
	half4 greenCornerWeights = make_half4((half)1.0f) / fmax(make_half4(DEBAYER_DIFF_EPSILON), greenCornerDiffs);
	half maxWeight = greenCornerWeights.x + greenCornerWeights.y + greenCornerWeights.z + greenCornerWeights.w;
	greenCornerWeights /= maxWeight;
	half4 blueWeights = blueTopLeftRightBottomLeftRight * greenCornerWeights;
	half blue = blueWeights.x + blueWeights.y + blueWeights.z + blueWeights.w;

	return make_half2(green, blue);
}

PRIVATE half2 SynthesiseNonGreen(half Green, half TopLeftGreen, half TopRightGreen, half BottomLeftGreen, half BottomRightGreen,
	half Above, half Below, half Left, half Right)
{
	half NorthGreen = (TopLeftGreen + TopRightGreen) * (half)0.5f;
	half SouthGreen = (BottomLeftGreen + BottomRightGreen) * (half)0.5f;
	half NorthGreenDiff = fabs(NorthGreen - Green);
	half SouthGreenDiff = fabs(SouthGreen - Green);
	half VerticalWeight = NorthGreenDiff / (NorthGreenDiff + SouthGreenDiff);
	half VerticalInterpolated = mix(Above, Below, clamp(VerticalWeight, (half)0.0f, (half)1.0f));

	half WestGreen = (TopLeftGreen + BottomLeftGreen) * (half)0.5f;
	half EastGreen = (TopRightGreen + BottomRightGreen) * (half)0.5f;
	half WestGreenDiff = fabs(WestGreen - Green);
	half EastGreenDiff = fabs(EastGreen - Green);
	half HorizontalWeight = WestGreenDiff / (WestGreenDiff + EastGreenDiff);
	half HorizontalInterpolated = mix(Left, Right, clamp(HorizontalWeight, (half)0.0f, (half)1.0f));

	return make_half2(HorizontalInterpolated, VerticalInterpolated);
}

PRIVATE half4 BayerTile(__read_only image2d_t rawImage, int2 inputCoord)
{
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	return make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x);
}

PRIVATE half4 LineariseBayerTile(__read_only image2d_t rawImage, int2 inputCoord, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	const sampler_t rawSampler = CLK_NORMALIZED_COORDS_FALSE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_NEAREST;
	half4 nonLinear = make_half4(read_imageh(rawImage, rawSampler, inputCoord).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 0)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(0, 1)).x,
		read_imageh(rawImage, rawSampler, inputCoord + make_int2(1, 1)).x) / (half)linearizeTableRange;

	const sampler_t lineariseSampler = CLK_NORMALIZED_COORDS_TRUE | CLK_ADDRESS_CLAMP_TO_EDGE | CLK_FILTER_LINEAR;
	return make_half4(read_imageh(linearizeTable, lineariseSampler, (float)nonLinear.x).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinear.y).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinear.z).x,
		read_imageh(linearizeTable, lineariseSampler, (float)nonLinear.w).x);
}

PRIVATE RGBHalf4 LineariseDebayerGBRG(__read_only image2d_t rawImage, int2 inputCoord, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	// Lookup and linearise bayer tiles
	half4 tile = LineariseBayerTile(rawImage, inputCoord, linearizeTable, linearizeTableRange);
	half4 tileAboveLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2, -2), linearizeTable, linearizeTableRange);
	half4 tileAbove = LineariseBayerTile(rawImage, inputCoord + make_int2(0, -2), linearizeTable, linearizeTableRange);
	half4 tileAboveRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, -2), linearizeTable, linearizeTableRange);
	half4 tileLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2, 0), linearizeTable, linearizeTableRange);
	half4 tileRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, 0), linearizeTable, linearizeTableRange);
	half4 tileBelowLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2, 2), linearizeTable, linearizeTableRange);
	half4 tileBelow = LineariseBayerTile(rawImage, inputCoord + make_int2(0, 2), linearizeTable, linearizeTableRange);
	half4 tileBelowRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, 2), linearizeTable, linearizeTableRange);

	// Top left (Green pixel)
	half2 topLeftBR = SynthesiseNonGreen(tile.x, tileAboveLeft.w, tileAbove.w, tileLeft.w, tile.w,
		tileAbove.z, tile.z, tileLeft.y, tile.y);
	half3 topLeft = make_half3(topLeftBR.y, tile.x, topLeftBR.x);

	// Top right (Blue pixel)
	half2 topRightGR = SynthesiseGR(tile.y, make_half4(tileAbove.w, tile.w, tile.x, tileRight.x), make_half4(tileAbove.y, tileBelow.y, tileLeft.y, tileRight.y),
		make_half4(tileAbove.z, tileAboveRight.z, tile.z, tileRight.z));
	half3 topRight = make_half3(topRightGR.y, topRightGR.x, tile.y);

	// Bottom left (Red pixel)
	half2 bottomLeftGB = SynthesiseGB(tile.z, make_half4(tile.x, tileBelow.x, tileLeft.w, tile.w), make_half4(tileAbove.z, tileBelow.z, tileLeft.z, tileRight.z),
		make_half4(tileLeft.y, tile.y, tileBelowLeft.y, tileBelow.y));
	half3 bottomLeft = make_half3(tile.z, bottomLeftGB.x, bottomLeftGB.y);

	// Bottom right (Green pixel)
	half2 bottomRightRB = SynthesiseNonGreen(tile.w, tile.x, tileRight.x, tileBelow.x, tileBelowRight.x,
		tile.y, tileBelow.y, tile.z, tileRight.z);
	half3 bottomRight = make_half3(bottomRightRB.x, tile.w, bottomRightRB.y);

	RGBHalf4 CameraRGB;
	CameraRGB.RGB[0] = topLeft;
	CameraRGB.RGB[1] = topRight;
	CameraRGB.RGB[2] = bottomLeft;
	CameraRGB.RGB[3] = bottomRight;
	return CameraRGB;
}

PRIVATE RGBHalf4 DebayerGBRG(__read_only image2d_t rawImage, int2 inputCoord)
{
	// Lookup bayer tiles
	half4 tile = BayerTile(rawImage, inputCoord);
	half4 tileAboveLeft = BayerTile(rawImage, inputCoord + make_int2(-2, -2));
	half4 tileAbove = BayerTile(rawImage, inputCoord + make_int2(0, -2));
	half4 tileAboveRight = BayerTile(rawImage, inputCoord + make_int2(2, -2));
	half4 tileLeft = BayerTile(rawImage, inputCoord + make_int2(-2, 0));
	half4 tileRight = BayerTile(rawImage, inputCoord + make_int2(2, 0));
	half4 tileBelowLeft = BayerTile(rawImage, inputCoord + make_int2(-2, 2));
	half4 tileBelow = BayerTile(rawImage, inputCoord + make_int2(0, 2));
	half4 tileBelowRight = BayerTile(rawImage, inputCoord + make_int2(2, 2));

	// Top left (Green pixel)
	half2 topLeftBR = SynthesiseNonGreen(tile.x, tileAboveLeft.w, tileAbove.w, tileLeft.w, tile.w,
		tileAbove.z, tile.z, tileLeft.y, tile.y);
	half3 topLeft = make_half3(topLeftBR.y, tile.x, topLeftBR.x);

	// Top right (Blue pixel)
	half2 topRightGR = SynthesiseGR(tile.y, make_half4(tileAbove.w, tile.w, tile.x, tileRight.x), make_half4(tileAbove.y, tileBelow.y, tileLeft.y, tileRight.y),
		make_half4(tileAbove.z, tileAboveRight.z, tile.z, tileRight.z));
	half3 topRight = make_half3(topRightGR.y, topRightGR.x, tile.y);

	// Bottom left (Red pixel)
	half2 bottomLeftGB = SynthesiseGB(tile.z, make_half4(tile.x, tileBelow.x, tileLeft.w, tile.w), make_half4(tileAbove.z, tileBelow.z, tileLeft.z, tileRight.z),
		make_half4(tileLeft.y, tile.y, tileBelowLeft.y, tileBelow.y));
	half3 bottomLeft = make_half3(tile.z, bottomLeftGB.x, bottomLeftGB.y);

	// Bottom right (Green pixel)
	half2 bottomRightRB = SynthesiseNonGreen(tile.w, tile.x, tileRight.x, tileBelow.x, tileBelowRight.x,
		tile.y, tileBelow.y, tile.z, tileRight.z);
	half3 bottomRight = make_half3(bottomRightRB.x, tile.w, bottomRightRB.y);

	RGBHalf4 CameraRGB;
	CameraRGB.RGB[0] = topLeft;
	CameraRGB.RGB[1] = topRight;
	CameraRGB.RGB[2] = bottomLeft;
	CameraRGB.RGB[3] = bottomRight;
	return CameraRGB;
}

PRIVATE RGBHalf4 LineariseDebayerBGGR(__read_only image2d_t rawImage, int2 inputCoord, __read_only image1d_t linearizeTable, float linearizeTableRange)
{
	// Lookup and linearise bayer tiles
	half4 tile = LineariseBayerTile(rawImage, inputCoord, linearizeTable, linearizeTableRange);
	half4 tileAboveLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2,-2), linearizeTable, linearizeTableRange);
	half4 tileAbove = LineariseBayerTile(rawImage, inputCoord + make_int2(0, -2), linearizeTable, linearizeTableRange);
	half4 tileAboveRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, -2), linearizeTable, linearizeTableRange);
	half4 tileLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2, 0), linearizeTable, linearizeTableRange);
	half4 tileRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, 0), linearizeTable, linearizeTableRange);
	half4 tileBelowLeft = LineariseBayerTile(rawImage, inputCoord + make_int2(-2, 2), linearizeTable, linearizeTableRange);
	half4 tileBelow = LineariseBayerTile(rawImage, inputCoord + make_int2(0, 2), linearizeTable, linearizeTableRange);
	half4 tileBelowRight = LineariseBayerTile(rawImage, inputCoord + make_int2(2, 2), linearizeTable, linearizeTableRange);

	// Top left (Blue pixel)
	half2 topLeftGR = SynthesiseGreenAndRed(tile, tileAboveLeft, tileAbove, tileLeft, tileRight, tileBelow);
	half3 topLeft = make_half3(topLeftGR.y, topLeftGR.x, tile.x);

	// Top right (Green pixel)
	half2 topRightBR = SynthesiseNonGreen(tile.y, tileAbove.z, tileAboveRight.z, tile.z, tileRight.z,
		tileAbove.w, tile.w, tile.x, tileRight.x);
	half3 topRight = make_half3(topRightBR.y, tile.y, topRightBR.x);

	// Bottom left (Green pixel)
	half2 bottomLeftRB = SynthesiseNonGreen(tile.z, tileLeft.y, tile.y, tileBelowLeft.y, tileBelow.y,
		tile.x, tileBelow.x, tileLeft.w, tile.w);
	half3 bottomLeft = make_half3(bottomLeftRB.x, tile.z, bottomLeftRB.y);

	// Bottom right (Red pixel)
	half2 bottomRightGB = SynthesiseGreenAndBlue(tile, tileAbove, tileLeft, tileRight, tileBelow, tileBelowRight);
	half3 bottomRight = make_half3(tile.w, bottomRightGB.x, bottomRightGB.y);

	RGBHalf4 CameraRGB;
	CameraRGB.RGB[0] = topLeft;
	CameraRGB.RGB[1] = topRight;
	CameraRGB.RGB[2] = bottomLeft;
	CameraRGB.RGB[3] = bottomRight;
	return CameraRGB;
}

PRIVATE RGBHalf4 DebayerBGGR(__read_only image2d_t rawImage, int2 inputCoord)
{
	// Lookup bayer tiles
	half4 tile = BayerTile(rawImage, inputCoord);
	half4 tileAboveLeft = BayerTile(rawImage, inputCoord + make_int2(-2, -2));
	half4 tileAbove = BayerTile(rawImage, inputCoord + make_int2(0, -2));
	half4 tileAboveRight = BayerTile(rawImage, inputCoord + make_int2(2, -2));
	half4 tileLeft = BayerTile(rawImage, inputCoord + make_int2(-2, 0));
	half4 tileRight = BayerTile(rawImage, inputCoord + make_int2(2, 0));
	half4 tileBelowLeft = BayerTile(rawImage, inputCoord + make_int2(-2, 2));
	half4 tileBelow = BayerTile(rawImage, inputCoord + make_int2(0, 2));
	half4 tileBelowRight = BayerTile(rawImage, inputCoord + make_int2(2, 2));

	// Top left (Blue pixel)
	half2 topLeftGR = SynthesiseGreenAndRed(tile, tileAboveLeft, tileAbove, tileLeft, tileRight, tileBelow);
	half3 topLeft = make_half3(topLeftGR.y, topLeftGR.x, tile.x);

	// Top right (Green pixel)
	half2 topRightBR = SynthesiseNonGreen(tile.y, tileAbove.z, tileAboveRight.z, tile.z, tileRight.z,
		tileAbove.w, tile.w, tile.x, tileRight.x);
	half3 topRight = make_half3(topRightBR.y, tile.y, topRightBR.x);

	// Bottom left (Green pixel)
	half2 bottomLeftRB = SynthesiseNonGreen(tile.z, tileLeft.y, tile.y, tileBelowLeft.y, tileBelow.y,
		tile.x, tileBelow.x, tileLeft.w, tile.w);
	half3 bottomLeft = make_half3(bottomLeftRB.x, tile.z, bottomLeftRB.y);

	// Bottom right (Red pixel)
	half2 bottomRightGB = SynthesiseGreenAndBlue(tile, tileAbove, tileLeft, tileRight, tileBelow, tileBelowRight);
	half3 bottomRight = make_half3(tile.w, bottomRightGB.x, bottomRightGB.y);

	RGBHalf4 CameraRGB;
	CameraRGB.RGB[0] = topLeft;
	CameraRGB.RGB[1] = topRight;
	CameraRGB.RGB[2] = bottomLeft;
	CameraRGB.RGB[3] = bottomRight;
	return CameraRGB;
}

#endif
